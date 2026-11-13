public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        var hero = Spell.Cast();

        if (hero.Name != "Merlin")
        {
            yield return (
                $"name devia ser \"Merlin\", mas deu \"{hero.Name}\".",
                $"name should be \"Merlin\", but it was \"{hero.Name}\".");
        }

        if (hero.Health != 100)
        {
            yield return (
                $"health devia ser 100, mas deu {hero.Health}.",
                $"health should be 100, but it was {hero.Health}.");
        }

        if (hero.Mana != 50.5)
        {
            var mana = hero.Mana.ToString(System.Globalization.CultureInfo.InvariantCulture);
            yield return (
                $"mana devia ser 50.5, mas deu {mana}.",
                $"mana should be 50.5, but it was {mana}.");
        }

        if (!hero.IsAlive)
        {
            yield return (
                "isAlive devia ser true, mas deu false.",
                "isAlive should be true, but it was false.");
        }
    }
}
