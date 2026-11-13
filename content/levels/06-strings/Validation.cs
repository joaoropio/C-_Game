public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
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
            if (actual != expected)
            {
                yield return (
                    $"Com name = \"{name}\" o código devia ser \"{expected}\", mas deu \"{actual}\".",
                    $"With name = \"{name}\" the code should be \"{expected}\", but it was \"{actual}\".");
            }
        }
    }
}
