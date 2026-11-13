public static class Validation
{
    public static IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        var hero = Spell.Cast();
        var invariant = System.Globalization.CultureInfo.InvariantCulture;

        yield return ("name", "\"Merlin\"", hero.Name is null ? "null" : $"\"{hero.Name}\"");
        yield return ("health", "100", hero.Health.ToString(invariant));
        yield return ("mana", "50.5", hero.Mana.ToString(invariant));
        yield return ("isAlive", "true", hero.IsAlive ? "true" : "false");
    }
}
