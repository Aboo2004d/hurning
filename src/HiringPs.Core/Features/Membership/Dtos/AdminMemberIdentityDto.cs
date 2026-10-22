namespace HiringPs.Core;

/// <summary>هوية عضو للعرض والتأكيد؛ يبقى المعرّف للاستخدام الداخلي فقط.</summary>
public sealed record AdminMemberIdentityDto(string UserId, string Name, string Email);
