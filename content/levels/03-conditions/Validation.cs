public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        foreach (var (rune, expected) in new[]
        {
            ("fire", "left"),
            ("ice", "right"),
            ("wind", "center"),
            ("earth", "back"),
        })
        {
            var actual = Spell.ChooseDoor(rune);
            if (actual != expected)
            {
                yield return (
                    $"Com rune = \"{rune}\" a porta devia ser \"{expected}\", mas deu \"{actual}\".",
                    $"With rune = \"{rune}\" the door should be \"{expected}\", but it was \"{actual}\".");
            }
        }
    }
}
