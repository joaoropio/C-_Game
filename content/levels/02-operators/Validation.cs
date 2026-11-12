public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        if (Spell.Damage(10, 3, 2) != 17)
        {
            yield return (
                "Com ataque 10, defesa 3 e multiplicador 2, o dano devia ser 17.",
                "With attack 10, defense 3 and multiplier 2, the damage should be 17.");
        }

        if (Spell.Damage(5, 0, 3) != 15)
        {
            yield return (
                "Com ataque 5, defesa 0 e multiplicador 3, o dano devia ser 15.",
                "With attack 5, defense 0 and multiplier 3, the damage should be 15.");
        }

        if (Spell.Damage(7, 4, 1) != 3)
        {
            yield return (
                "Com ataque 7, defesa 4 e multiplicador 1, o dano devia ser 3.",
                "With attack 7, defense 4 and multiplier 1, the damage should be 3.");
        }
    }
}
