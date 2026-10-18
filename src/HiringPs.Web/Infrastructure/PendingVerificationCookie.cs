using System.Text.Json;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.DataProtection;

namespace HiringPs.Web.Infrastructure;

public sealed class PendingVerificationCookie(IDataProtectionProvider protection)
{
    private readonly IDataProtector protector = protection.CreateProtector(
        "HiringPs.MemberVerification.v1"
    );
    private const string Name = "HiringPs.Verification";

    public void Save(HttpContext context, PendingVerification value)
    {
        context.Response.Cookies.Append(
            Name,
            protector.Protect(JsonSerializer.Serialize(value)),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = value.ExpiresAt,
                Path = "/",
            }
        );
    }

    public PendingVerification? Read(HttpContext context)
    {
        try
        {
            var value = context.Request.Cookies[Name];
            var pending = value is null
                ? null
                : JsonSerializer.Deserialize<PendingVerification>(protector.Unprotect(value));
            return pending?.ExpiresAt > DateTimeOffset.UtcNow ? pending : null;
        }
        catch (Exception ex)
            when (ex is System.Security.Cryptography.CryptographicException or JsonException)
        {
            return null;
        }
    }

    public void Clear(HttpContext context) =>
        context.Response.Cookies.Delete(Name, new CookieOptions { Path = "/" });
}
