namespace HiringPs.Web.Models;

public sealed record SaveButton(long OpportunityId, bool Saved)
{
    public string FormId =>
        "save-" + OpportunityId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
