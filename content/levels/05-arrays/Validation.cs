public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        foreach (var (shelves, expected) in new[]
        {
            (new[] { 2, 3, 5 }, 10),
            (new[] { 7 }, 7),
            (new int[] { }, 0),
            (new[] { 0, 4, 0, 1 }, 5),
        })
        {
            var actual = Spell.CountPotions(shelves);
            if (actual != expected)
            {
                var list = shelves.Length == 0 ? "{ }" : "{ " + string.Join(", ", shelves) + " }";
                yield return (
                    $"Com as prateleiras {list} o total devia ser {expected}, mas deu {actual}.",
                    $"With the shelves {list} the total should be {expected}, but it was {actual}.");
            }
        }
    }
}
