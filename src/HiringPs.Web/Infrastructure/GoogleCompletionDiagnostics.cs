using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace HiringPs.Web.Infrastructure;

// Observe the existing CSRF decision; this filter never skips or overrides validation.
public sealed class GoogleCompletionDiagnostics(
    IAntiforgery antiforgery,
    ILogger<GoogleCompletionDiagnostics> logger,
    IOptions<AntiforgeryOptions> options
) : IAsyncActionFilter, IAsyncAlwaysRunResultFilter
{
    public const string RejectedItem = "GoogleCompletion.AntiforgeryRejected";

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        logger.LogInformation(
            "GoogleCompletionRequest Build google-completion-trace-v1 Stage ActionEntered TraceId {TraceId}",
            Reference(context.HttpContext)
        );
        await next();
    }

    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next
    )
    {
        if (context.Result is IAntiforgeryValidationFailedResult)
        {
            context.HttpContext.Items[RejectedItem] = true;
            try
            {
                // Retrieve the framework's rejection reason, normally hidden by warning-only logs.
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException ex)
            {
                var request = context.HttpContext.Request;
                var tokenCount = 0;
                try
                {
                    if (request.HasFormContentType)
                        tokenCount = (await request.ReadFormAsync())[
                            options.Value.FormFieldName
                        ].Count;
                }
                catch (Exception readError)
                    when (readError
                            is InvalidDataException
                                or IOException
                                or BadHttpRequestException
                    )
                {
                    tokenCount = -1;
                }
                logger.LogWarning(
                    "GoogleCompletionRequest Build google-completion-trace-v1 Stage Rejected Code Antiforgery TraceId {TraceId} Reason {Reason} Scheme {Scheme} HasCookie {HasCookie} FormTokenCount {FormTokenCount}",
                    Reference(context.HttpContext),
                    ex.Message,
                    request.Scheme,
                    options.Value.Cookie.Name is { } name && request.Cookies.ContainsKey(name),
                    tokenCount
                );
            }
        }
        await next();
    }

    private static string Reference(HttpContext context) =>
        Activity.Current?.Id ?? context.TraceIdentifier;
}
