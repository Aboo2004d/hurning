using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HiringPs.Web.Infrastructure;

// HTML number inputs submit ASCII decimals regardless of the interface language.
public sealed class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None)
            return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        if (
            decimal.TryParse(
                value.FirstValue,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var number
            )
        )
            context.Result = ModelBindingResult.Success(number);
        else
            context.ModelState.TryAddModelError(context.ModelName, "InvalidNumber");
        return Task.CompletedTask;
    }
}
