namespace CabinBingo.Api.Services;

public static class PreferenceAnswersValidator
{
    public static void ValidateOrThrow(
        IReadOnlyDictionary<string, IReadOnlyList<string>> answers,
        IReadOnlyList<PreferenceCatalogRow> catalog)
    {
        var catalogById = catalog.ToDictionary(c => c.PreferenceId, StringComparer.OrdinalIgnoreCase);

        foreach (var def in catalog)
        {
            if (!answers.TryGetValue(def.PreferenceId, out var _))
                throw new InvalidOperationException($"Missing answer for preference '{def.PreferenceId}'.");
        }

        foreach (var (prefId, values) in answers)
        {
            if (!catalogById.TryGetValue(prefId, out var def))
                throw new InvalidOperationException($"Unknown preference '{prefId}'.");

            var allowed = new HashSet<string>(def.Options.Select(o => o.Value), StringComparer.OrdinalIgnoreCase);
            if (values.Count == 0)
                throw new InvalidOperationException($"Preference '{prefId}' requires at least one value.");

            foreach (var v in values)
            {
                if (!allowed.Contains(v))
                    throw new InvalidOperationException($"Invalid value '{v}' for preference '{prefId}'.");
            }

            if (def.AnswerType.Equals("single", StringComparison.OrdinalIgnoreCase) && values.Count != 1)
                throw new InvalidOperationException($"Preference '{prefId}' expects a single answer.");
        }
    }
}
