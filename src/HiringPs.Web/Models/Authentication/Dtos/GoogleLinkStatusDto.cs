namespace HiringPs.Web.Models;

/// <summary>حالة زر ربط جوجل فقط؛ لا تعرض مستخدم Identity أو معرف الدخول الخارجي.</summary>
public sealed record GoogleLinkStatusDto(bool Available, bool Linked, bool HasPassword);
