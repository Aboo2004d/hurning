using HiringPs.Core;
namespace HiringPs.Web.Models;
/// <summary>إعدادات الوجهات وسجل آخر المواعيد دون أي بيانات ربط سرية.</summary>
public sealed record RandomPublishingPage(List<RandomPublicationSettingsDto> Settings, AdminPage<RandomPublicationRunDto> History);
