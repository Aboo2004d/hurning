using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public static class ContactReplyLinks
{
    public static string Mailto(string email, string subject) =>
        "mailto:" + Uri.EscapeDataString(email) + "?subject=" + Uri.EscapeDataString(subject);

    public static string Gmail(string email, string subject) =>
        "https://mail.google.com/mail/?view=cm&fs=1&to="
        + Uri.EscapeDataString(email)
        + "&su="
        + Uri.EscapeDataString(subject);
}
