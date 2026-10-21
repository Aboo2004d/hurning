using System.ComponentModel.DataAnnotations;
using HiringPs.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HiringPs.Web.Models;

/// <summary>مدخل اعتماد مجموعة؛ الكتالوج المساعد لا يؤخذ من الطلب.</summary>
public sealed class CategoryGroupInput
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    public string[] Members { get; set; } = [];
    public string Version { get; set; } = "";
    [BindNever] public CategoryGroupOptionDto[] Options { get; set; } = [];
}
