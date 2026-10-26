namespace HiringPs.Core;

/// <summary>المحرك التنفيذي وملف تشغيل Playwright، وفق مسار الحزمة وتجاوزات البيئة.</summary>
public sealed record BrowserDriverPathsDto(string Node, string EntryPoint);
