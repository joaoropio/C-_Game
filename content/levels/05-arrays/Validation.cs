public static class Validation
{
    public static IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        foreach (var (shelves, expected) in new[]
        {
            (new[] { 2, 3, 5 }, 10),
            (new[] { 7 }, 7),
            (new int[] { }, 0),
            (new[] { 0, 4, 0, 1 }, 5),
        })
        {
            var actual = Spell.CountPotions(shelves);
            var list = shelves.Length == 0 ? "{ }" : "{ " + string.Join(", ", shelves) + " }";
            yield return ($"potions = {list}", FormattableString.Invariant($"{expected}"), FormattableString.Invariant($"{actual}"));
        }
    }
}
