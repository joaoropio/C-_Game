public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        var hero = Spell.Cast();

        if (hero.Name != "Merlin")
        {
            yield return (
                $"O nome devia ser name = \"Merlin\", mas deu \"{hero.Name}\".",
                $"The name should be name = \"Merlin\", but it was \"{hero.Name}\".");
        }

        if (hero.Health != 100)
        {
            yield return (
                $"A vida devia ser health = 100, mas deu {hero.Health}.",
                $"The health should be health = 100, but it was {hero.Health}.");
        }

        if (hero.Mana != 50.5)
        {
            var mana = hero.Mana.ToString(System.Globalization.CultureInfo.InvariantCulture);
            yield return (
                $"A mana devia ser mana = 50.5, mas deu {mana}.",
                $"The mana should be mana = 50.5, but it was {mana}.");
        }

        if (!hero.IsAlive)
        {
            yield return (
                "O mago devia estar vivo, isAlive = true, mas deu false.",
                "The wizard should be alive, isAlive = true, but it was false.");
        }
    }
}
