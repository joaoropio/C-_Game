public static class Validation
{
    public static System.Collections.Generic.IEnumerable<(string Pt, string En)> Run()
    {
        var hero = Spell.Cast();

        if (hero.Name != "Merlin")
        {
            yield return ("O nome devia ser \"Merlin\".", "The name should be \"Merlin\".");
        }

        if (hero.Health != 100)
        {
            yield return ("A vida (health) devia ser 100.", "The health should be 100.");
        }

        if (hero.Mana != 50.5)
        {
            yield return ("A mana devia ser 50.5.", "The mana should be 50.5.");
        }

        if (!hero.IsAlive)
        {
            yield return ("O mago devia estar vivo (isAlive = true).", "The wizard should be alive (isAlive = true).");
        }
    }
}
