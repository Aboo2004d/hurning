namespace HiringPs.Core;

/// <summary>عقد تفسير تواريخ الإعلانات الذي يضاف إلى تعليمات AI.</summary>
public static class PromptDatePolicy
{
    public const string SemanticDeadline = """
SEMANTIC DEADLINE POLICY v1: Determine the submission/application/registration closing date from the meaning and context of the original announcement, in any language or phrasing. There is no required keyword or exhaustive list of closing expressions. Treat any clearly stated end of the application, registration, or bid-submission window as deadline, even if the wording is unfamiliar. This is not the end of the job, training course, project, contract, or event; do not use publication, modification, opening of registration, site-visit, document-purchase, or bid-opening dates as deadline. Return the explicit date in yyyy-MM-dd and copy one exact continuous source clause into deadlineEvidence containing the date and enough context to establish the closing meaning. Do not invent a keyword, translate the evidence, infer a missing year, or invent a date. If the closing meaning or date is ambiguous, return null and explain the ambiguity; a genuinely absent deadline alone never requires review when AllowUnknownDeadline is true. An unfamiliar closing phrase alone is not a reason for review.
""";

    public static string CurrentDateContract =>
        PromptDefaults.DateInstructions.ReplaceLineEndings("\n")
        + "\n"
        + SemanticDeadline.ReplaceLineEndings("\n");
}
