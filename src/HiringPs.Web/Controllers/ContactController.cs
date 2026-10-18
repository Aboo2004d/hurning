using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class ContactController(HiringDb db) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new ContactInput());

    [HttpPost, EnableRateLimiting("contact")]
    public async Task<IActionResult> Index(ContactInput input, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(input.Website))
        {
            TempData["Notice"] = "ContactReceived";
            return RedirectToAction(nameof(Index));
        }

        input.Name = input.Name?.Trim() ?? "";
        input.Email = input.Email?.Trim() ?? "";
        input.Message = input.Message?.Trim() ?? "";
        if (
            input.Email.Any(char.IsControl)
            || !System.Net.Mail.MailAddress.TryCreate(input.Email, out var address)
            || !string.Equals(address.Address, input.Email, StringComparison.OrdinalIgnoreCase)
        )
            ModelState.AddModelError(nameof(input.Email), "ContactEmailInvalid");
        if (!ModelState.IsValid)
            return View(input);
        db.ContactMessages.Add(
            new ContactMessage
            {
                Name = input.Name,
                Email = input.Email,
                Message = input.Message,
            }
        );
        await db.SaveChangesAsync(ct);
        TempData["Notice"] = "ContactReceived";
        return RedirectToAction(nameof(Index));
    }
}
