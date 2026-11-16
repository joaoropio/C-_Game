public static partial class Spell
{
    public static class Validation
    {
        public static IEnumerable<(string Case, string Expected, string Actual)> Run()
        {
            foreach (var (loot, expected) in new[]
            {
                (new[] { "ruby", "stone", "key" }, new[] { "ruby", "key" }),
                (new[] { "stone" }, new string[] { }),
                (new string[] { }, new string[] { }),
                (new[] { "key", "key" }, new[] { "key", "key" }),
            })
            {
                var actual = PackBag((string[])loot.Clone());
                yield return ($"PackBag({Format(loot)})", Format(expected), actual is null ? "null" : Format(actual));
            }
        }

        private static string Format(global::System.Collections.Generic.IEnumerable<string> items)
        {
            var quoted = new global::System.Collections.Generic.List<string>();
            foreach (var item in items)
            {
                quoted.Add("\"" + item + "\"");
            }

            return quoted.Count == 0 ? "{ }" : "{ " + global::System.String.Join(", ", quoted) + " }";
        }
    }
}
