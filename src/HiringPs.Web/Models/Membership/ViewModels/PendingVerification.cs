using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed record PendingVerification(
    VerificationRequest Request,
    string UserId,
    DateTimeOffset ExpiresAt,
    bool Verified = false
);
