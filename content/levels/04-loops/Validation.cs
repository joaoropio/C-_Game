public static class Validation
{
    public static IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        foreach (var (turns, expected) in new[] { (1, 1), (3, 6), (5, 15), (0, 0) })
        {
            var actual = Spell.ChargeMana(turns);
            yield return ($"turns = {turns}", FormattableString.Invariant($"{expected}"), FormattableString.Invariant($"{actual}"));
        }
    }
}
