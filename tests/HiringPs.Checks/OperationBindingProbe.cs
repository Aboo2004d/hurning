using HiringPs.Core;
using HiringPs.Web.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>Examines production action model binding in the test host, before any SQL query.</summary>
public sealed class OperationBindingProbe : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.Controller is not OperationsController
            || context.HttpContext.Request.Headers["X-Fixture-Inspect"] != "operation-binding")
            return;
        context.ActionArguments.TryGetValue("filter", out var value);
        var filter = value as OperationFilterDto;
        context.Result = new JsonResult(new
        {
            valid = context.ModelState.IsValid,
            area = filter?.Area?.ToString(),
            outcome = filter?.Outcome?.ToString(),
            search = filter?.Search,
            from = filter?.From?.ToString("yyyy-MM-dd"),
        }) { StatusCode = context.ModelState.IsValid ? 200 : 400 };
    }
}
