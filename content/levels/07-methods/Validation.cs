public static class Validation
{
    public static IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        foreach (var (health, potion, expected) in new[]
        {
            (50, 30, 80),
            (90, 30, 100),
            (100, 0, 100),
            (0, 25, 25),
        })
        {
            yield return ($"Heal({health}, {potion})", $"{expected}", $"{Spell.Heal(health, potion)}");
        }
    }
}
