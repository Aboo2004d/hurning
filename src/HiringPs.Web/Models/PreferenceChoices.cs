namespace HiringPs.Web.Models;

public sealed record PreferenceChoices(
    string Field,
    string LabelKey,
    string[] Selected,
    string[] Available
);
