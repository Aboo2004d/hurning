using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace HiringPs.Core;

/// <summary>Singleton settings for member job queries; opt-in from administration.</summary>
public sealed class TelegramMemberSetting
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int Id { get; set; } = 1;
    public bool SearchEnabled { get; set; }
    public bool PaidOnly { get; set; } = true;
    public bool CityEnabled { get; set; } = true;
    public bool OrganizationEnabled { get; set; } = true;
    public bool CategoryEnabled { get; set; } = true;
}
