using System.Globalization;
using HiringPs.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace HiringPs.Web.Infrastructure;

/// <summary>يفسر حقول datetime-local كتوقيت فلسطين ويعيد لحظة UTC دون الاعتماد على توقيت الخادم.</summary>
public sealed class PalestineDateTimeOffsetModelBinder(IStringLocalizer<Labels> labels) : IModelBinder
{
    /// <summary>يربط وقت النموذج المحلي بالمنطقة المركزية ويرفض الساعة غير الموجودة عند انتقال التوقيت الصيفي.</summary>
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        string[] formats = ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"];
        if (!DateTime.TryParseExact(value.FirstValue, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed))
        {
            context.ModelState.TryAddModelError(context.ModelName, labels["PalestineDateTimeInvalid"].Value);
            return Task.CompletedTask;
        }
        var local = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        if (TextRules.PalestineTimeZone.IsInvalidTime(local))
        {
            context.ModelState.TryAddModelError(context.ModelName, labels["PalestineDateTimeInvalid"].Value);
            return Task.CompletedTask;
        }
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, TextRules.PalestineTimeZone);
        context.Result = ModelBindingResult.Success(new DateTimeOffset(utc));
        return Task.CompletedTask;
    }
}
