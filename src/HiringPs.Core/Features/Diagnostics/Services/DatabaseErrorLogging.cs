using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>يسجل أخطاء الويب والعامل عبر طابور محدود واتصال مستقل عن معاملات العمل.</summary>
public sealed class DatabaseErrorLogging : BackgroundService, ILoggerProvider
{
    private readonly Channel<ApplicationError> queue = Channel.CreateBounded<ApplicationError>(
        new BoundedChannelOptions(2048) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly IConfiguration configuration;
    private readonly IHttpContextAccessor http;
    private readonly ErrorLogSanitizer sanitizer;
    private readonly string application;

    public DatabaseErrorLogging(IConfiguration configuration, IHttpContextAccessor http,
        ErrorLogSanitizer sanitizer, string application)
        => (this.configuration, this.http, this.sanitizer, this.application) = (configuration, http, sanitizer, application);

    /// <summary>ينشئ مسجلًا يلتقط Error وCritical والاستثناءات المسجلة بمستوى Warning.</summary>
    public ILogger CreateLogger(string categoryName) => new ErrorLogger(this, categoryName);

    private sealed class ErrorLogger(DatabaseErrorLogging owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (level < LogLevel.Error && (level != LogLevel.Warning || exception is null)) return;
            try
            {
                var context = owner.http.HttpContext;
                var entry = new ApplicationError
                {
                    At = DateTimeOffset.UtcNow, Application = owner.application, Level = level.ToString(),
                    Category = owner.sanitizer.Clean(category, 512),
                    TraceId = owner.sanitizer.Clean(Activity.Current?.Id ?? context?.TraceIdentifier, 200),
                    Path = owner.sanitizer.Clean(context is null ? "" : context.Request.PathBase + context.Request.Path, 2048),
                    ExceptionType = owner.sanitizer.Clean(exception?.GetType().FullName, 512),
                    Message = owner.sanitizer.Clean(formatter(state, exception), 8000),
                    Details = owner.sanitizer.Clean(exception?.ToString(), 64000)
                };
                if (!owner.queue.Writer.TryWrite(entry))
                    Console.Error.WriteLine("Database error queue full; consult the primary application log.");
            }
            catch { Console.Error.WriteLine("Unable to prepare database error record; consult the primary application log."); }
        }
    }

    /// <summary>يحفظ الأخطاء دون EF لتجنب إعادة تسجيل أخطاء مسجل الأخطاء نفسه.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in queue.Reader.ReadAllAsync())
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await using var connection = new SqlConnection(configuration.GetConnectionString("Hiring"));
                await connection.OpenAsync(timeout.Token);
                await using var command = connection.CreateCommand();
                command.CommandTimeout = 5;
                command.CommandText = """
                    INSERT INTO dbo.ApplicationErrors ([At], Application, Level, Category, TraceId, Path, ExceptionType, Message, Details)
                    VALUES (@at,@app,@level,@category,@trace,@path,@type,@message,@details)
                    """;
                command.Parameters.AddWithValue("@at", entry.At);
                command.Parameters.AddWithValue("@app", entry.Application);
                command.Parameters.AddWithValue("@level", entry.Level);
                command.Parameters.AddWithValue("@category", entry.Category);
                command.Parameters.AddWithValue("@trace", entry.TraceId);
                command.Parameters.AddWithValue("@path", entry.Path);
                command.Parameters.AddWithValue("@type", entry.ExceptionType);
                command.Parameters.AddWithValue("@message", entry.Message);
                command.Parameters.AddWithValue("@details", entry.Details);
                await command.ExecuteNonQueryAsync(timeout.Token);
            }
            catch { Console.Error.WriteLine("Database error record could not be saved; consult the primary application log."); }
        }
    }

    /// <summary>يغلق الطابور ويمنح السجلات المتبقية فرصة الحفظ عند الإيقاف المنظم.</summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}
