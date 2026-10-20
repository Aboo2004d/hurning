// اكتشاف روابط الفرص وجلب صفحاتها ومرفقاتها، مع فحص النطاقات وتسجيل التشخيص.
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>قفل SQL يملك اتصالًا مستقلًا لحماية تنفيذ عمليات العامل المتنافسة.</summary>
public sealed class SqlRunLock : IAsyncDisposable
{
    private readonly SqlConnection connection;
    public bool Acquired { get; private set; }

    private readonly string resource;

    public SqlRunLock(string connectionString, string resource = "HiringPs.Pipeline")
    {
        connection = new(connectionString);
        this.resource = resource;
    }

    /// <summary>يحاول امتلاك قفل SQL المخصص للعملية لمنع تشغيلها بالتوازي.</summary>
    public async Task<bool> AcquireAsync(CancellationToken ct)
    {
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "DECLARE @r int; EXEC @r=sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r;";
        cmd.Parameters.AddWithValue("@resource", resource);
        Acquired = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) >= 0;
        return Acquired;
    }

    /// <summary>يتحقق من بقاء قفل SQL مملوكًا قبل تنفيذ خطوة حساسة.</summary>
    public async Task VerifyAsync(CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT APPLOCK_MODE('public',@resource,'Session')";
        cmd.Parameters.AddWithValue("@resource", resource);
        if (
            !Acquired
            || !string.Equals(
                Convert.ToString(await cmd.ExecuteScalarAsync(ct)),
                "Exclusive",
                StringComparison.Ordinal
            )
        )
            throw new InvalidOperationException("Pipeline lock lost; stop work.");
    }

    /// <summary>يحرر موارد الخدمة أو قفلها أو يحفظ جلسة التشخيص عند انتهاء استخدامها.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Acquired && connection.State == System.Data.ConnectionState.Open)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    "EXEC sp_releaseapplock @Resource=@resource, @LockOwner='Session';";
                cmd.Parameters.AddWithValue("@resource", resource);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            Acquired = false;
            await connection.DisposeAsync();
        }
    }
}
