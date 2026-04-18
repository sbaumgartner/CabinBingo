namespace CabinBingo.Api.Services;

/// <summary>
/// Normalized answers used by bingo slot predicates. Missing keys are treated as unanswered.
/// </summary>
public sealed class UserAnswersState
{
    public string? Drink { get; set; }
    public string? Mtg { get; set; }
    public HashSet<string> BoardStyles { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? HotTub { get; set; }
    public string? Hike { get; set; }

    public static bool IsYes(string? v) =>
        v != null && v.Equals("Yes", StringComparison.OrdinalIgnoreCase);
}
