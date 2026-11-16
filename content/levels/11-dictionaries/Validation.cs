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
            var keys = new global::System.Collections.Generic.List<string>(counts.Keys);
            keys.Sort(global::System.StringComparer.Ordinal);
            var entries = new global::System.Collections.Generic.List<string>();
            foreach (var key in keys)
            {
                entries.Add($"[\"{key}\"] = {counts[key].ToString(global::System.Globalization.CultureInfo.InvariantCulture)}");
            }

            return entries.Count == 0 ? "{ }" : "{ " + global::System.String.Join(", ", entries) + " }";
        }

        private static string FormatList(string[] items)
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
