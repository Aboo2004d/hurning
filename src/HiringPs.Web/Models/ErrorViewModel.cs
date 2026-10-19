namespace HiringPs.Web.Models;

public class ErrorViewModel
{
    public bool GoogleFormExpired { get; set; }
    public int StatusCode { get; set; } = 500;
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
