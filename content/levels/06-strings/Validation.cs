public static class Validation
{
    public static IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        foreach (var (name, expected) in new[]
        {
            ("merlin", "M6"),
            ("Ana", "A3"),
            ("z", "Z1"),
            ("morgana", "M7"),
        })
        {
            var actual = Spell.SealCode(name);
            yield return ($"name = \"{name}\"", $"\"{expected}\"", $"\"{actual}\"");
        }
    }
}
