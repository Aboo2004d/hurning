using System.ComponentModel.DataAnnotations;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public class RetentionEditor
{
    public bool Enabled { get; set; }

    [Range(7, 3650)]
    public int RevisionDays { get; set; }

    [Range(7, 3650)]
    public int DiagnosticDays { get; set; }
    public string Version { get; set; } = "";
    public RetentionPolicy? Current { get; set; }
}
