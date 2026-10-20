using HiringPs.Web.Models;

namespace HiringPs.Web.Services.Content;

/// <summary>Edits and validates an unsaved question draft; persistence remains an explicit save action.</summary>
public static class SiteQuestionsDraft
{
    /// <summary>Applies an add/remove/move command to the submitted draft without touching stored content.</summary>
    public static bool TryApply(SiteQuestionsInput input, string command)
    {
        if (command == "add")
        {
            input.Items.Add(new());
            return true;
        }
        var parts = command.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var index) || index < 0 || index >= input.Items.Count)
            return false;
        if (parts[0] == "remove")
            input.Items.RemoveAt(index);
        else if (parts[0] == "up" && index > 0)
            (input.Items[index - 1], input.Items[index]) = (input.Items[index], input.Items[index - 1]);
        else if (parts[0] == "down" && index + 1 < input.Items.Count)
            (input.Items[index + 1], input.Items[index]) = (input.Items[index], input.Items[index + 1]);
        return true;
    }

    /// <summary>Trims both languages and counts incomplete question/answer pairs using the existing validation rule.</summary>
    public static int NormalizeAndCountIncompletePairs(SiteQuestionsInput input)
    {
        var incomplete = 0;
        foreach (var item in input.Items)
        {
            item.QuestionAr = item.QuestionAr?.Trim() ?? "";
            item.AnswerAr = item.AnswerAr?.Trim() ?? "";
            item.QuestionEn = item.QuestionEn?.Trim() ?? "";
            item.AnswerEn = item.AnswerEn?.Trim() ?? "";
            if (string.IsNullOrEmpty(item.QuestionAr) != string.IsNullOrEmpty(item.AnswerAr)
                || string.IsNullOrEmpty(item.QuestionEn) != string.IsNullOrEmpty(item.AnswerEn))
                incomplete++;
        }
        return incomplete;
    }
}
