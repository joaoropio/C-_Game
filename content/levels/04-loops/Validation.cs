public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        foreach (var (turns, expected) in new[] { (1, 1), (3, 6), (5, 15), (0, 0) })
        {
            var actual = Spell.ChargeMana(turns);
            if (actual != expected)
            {
                yield return (
                    $"Com turns = {turns} a mana devia ser {expected}, mas deu {actual}.",
                    $"With turns = {turns} the mana should be {expected}, but it was {actual}.");
            }
        }
    }
}
