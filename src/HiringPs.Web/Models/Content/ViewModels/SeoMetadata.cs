// إنشاء العناوين والوصف والروابط الأساسية والبيانات المنظمة للصفحات العامة.
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed record SeoMetadata(
    string Title,
    string Description,
    string Canonical,
    string Arabic,
    string English,
    string Robots,
    string Language,
    string StructuredData,
    string? Previous,
    string? Next
);
