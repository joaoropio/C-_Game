public static partial class Spell
{
    public static class Validation
    {
        public static IEnumerable<(string Case, string Expected, string Actual)> Run()
        {
            yield return ("SplitMana(10, 2)", "returns 5", Outcome(() => SplitMana(10, 2)));
            yield return ("SplitMana(10, 0)", "throws ArgumentException", Outcome(() => SplitMana(10, 0)));
            yield return ("SplitMana(10, -1)", "throws ArgumentException", Outcome(() => SplitMana(10, -1)));
            yield return ("SafeSplit(10, 0)", "returns 0", $"returns {SafeSplit(10, 0)}");
            yield return ("SafeSplit(9, 3)", "returns 3", $"returns {SafeSplit(9, 3)}");
        }

        private static string Outcome(global::System.Func<int> spell)
        {
            try
            {
                return $"returns {spell()}";
            }
            catch (global::System.Exception exception) when (!global::__LoopGuard.Tripped)
            {
                return $"throws {exception.GetType().Name}";
            }
        }
    }
}
