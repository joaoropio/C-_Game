public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        foreach (var (attack, defense, multiplier, expected) in new[]
        {
            (10, 3, 2, 17),
            (5, 0, 3, 15),
            (7, 4, 1, 3),
        })
        {
            var actual = Spell.Damage(attack, defense, multiplier);
            if (actual != expected)
            {
                yield return (
                    $"Com attack = {attack}, defense = {defense} e multiplier = {multiplier}, o dano devia ser {expected}, mas deu {actual}.",
                    $"With attack = {attack}, defense = {defense} and multiplier = {multiplier}, the damage should be {expected}, but it was {actual}.");
            }
        }
    }
}
