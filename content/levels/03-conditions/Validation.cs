public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        foreach (var (rune, expected, note) in new[]
        {
            ("fire", "left", ""),
            ("ice", "right", ""),
            ("wind", "center", ""),
            ("earth", "back", " (qualquer outra runa)"),
        })
        {
            var actual = Spell.ChooseDoor(rune);
            var noteEn = note == "" ? "" : " (any other rune)";
            if (actual != expected)
            {
                yield return (
                    $"Com rune = \"{rune}\"{note} a porta devia ser \"{expected}\", mas deu \"{actual}\".",
                    $"With rune = \"{rune}\"{noteEn} the door should be \"{expected}\", but it was \"{actual}\".");
            }
        }
    }
}
