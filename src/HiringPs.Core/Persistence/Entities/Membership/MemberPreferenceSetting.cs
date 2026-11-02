using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace HiringPs.Core;

/// <summary>Global category quota for accounts without a current subscription.</summary>
public sealed class MemberPreferenceSetting
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)] public int Id { get; set; } = 1;
    [Range(0, 1000)] public int NonSubscriberCategoryLimit { get; set; } = PreferenceLimitRules.NonSubscriberLimit;
}
