using HiringPs.Core;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Controllers;

public partial class AdminController
{
    [HttpGet]
    public IActionResult PdfArchive(string hash, string format = "report")
    {
        string path;
        try
        {
            path = PdfTextFiles.FilePath(configuration, hash, format);
        }
        catch (Exception ex) when (ex is ArgumentException or ContentNeedsReviewException)
        {
            return BadRequest();
        }
        if (
            !System.IO.File.Exists(path)
            || (System.IO.File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
        )
            return NotFound();
        return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
    }
}
