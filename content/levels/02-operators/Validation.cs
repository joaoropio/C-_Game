public static class Validation
{
    public static IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        foreach (var (attack, defense, multiplier, expected) in new[]
        {
            (10, 3, 2, 17),
            (5, 0, 3, 15),
            (7, 4, 1, 3),
        })
        {
            var actual = Spell.Damage(attack, defense, multiplier);
            yield return (
                $"attack = {attack}, defense = {defense}, multiplier = {multiplier}",
                FormattableString.Invariant($"{expected}"),
                FormattableString.Invariant($"{actual}"));
        }
    }
}
