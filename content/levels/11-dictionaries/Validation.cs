public static partial class Spell
{
    public static class Validation
    {
        public static IEnumerable<(string Case, string Expected, string Actual)> Run()
        {
            foreach (var (potions, expected) in new (string[], global::System.Collections.Generic.Dictionary<string, int>)[]
            {
                (new[] { "heal", "mana", "heal" }, new() { ["heal"] = 2, ["mana"] = 1 }),
                (new string[] { }, new()),
                (new[] { "fire" }, new() { ["fire"] = 1 }),
                (new[] { "mana", "fire", "mana", "mana" }, new() { ["fire"] = 1, ["mana"] = 3 }),
            })
            {
                object actual = CountPotions((string[])potions.Clone());
                yield return ($"CountPotions({FormatList(potions)})", Format(expected), Shown(actual));
            }
        }

        private static string Shown(object actual) => actual switch
        {
            null => "null",
            global::System.Collections.Generic.Dictionary<string, int> counts => Format(counts),
            _ => "not a Dictionary<string, int>",
        };

        private static string Format(global::System.Collections.Generic.Dictionary<string, int> counts)
        {
            var pairs = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<string, int>>(counts);
            pairs.Sort((left, right) => global::System.String.CompareOrdinal(left.Key, right.Key));
            var entries = new global::System.Collections.Generic.List<string>();
            foreach (var pair in pairs)
            {
                entries.Add($"[\"{pair.Key}\"] = {pair.Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture)}");
            }

            return entries.Count == 0 ? "{ }" : "{ " + global::System.String.Join(", ", entries) + " }";
        }

        private static string FormatList(string[] items)
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
