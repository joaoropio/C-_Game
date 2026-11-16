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
                object actual = PackBag((string[])loot.Clone());
                yield return ($"PackBag({Format(loot)})", Format(expected), Shown(actual));
            }
        }

        private static string Shown(object actual) => actual switch
        {
            null => "null",
            global::System.Collections.Generic.List<string> list => Format(list),
            _ => "not a List<string>",
        };

        private static string Format(global::System.Collections.Generic.IEnumerable<string> items)
        {
            var quoted = new global::System.Collections.Generic.List<string>();
            foreach (var item in items)
            {
                quoted.Add(item is null ? "null" : "\"" + item + "\"");
            }

            return quoted.Count == 0 ? "{ }" : "{ " + global::System.String.Join(", ", quoted) + " }";
        }
    }
}
