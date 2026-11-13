public static class Validation
{
    public static IEnumerable<(string Case, string Expected, string Actual)> Run()
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
            yield return ($"rune = \"{rune}\"", $"\"{expected}\"", $"\"{actual}\"");
        }
    }
}
