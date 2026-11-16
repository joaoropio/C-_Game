public static partial class Spell
{
    public static class Validation
    {
        public static IEnumerable<(string Case, string Expected, string Actual)> Run()
        {
            yield return ("SplitMana(10, 2)", "returns 5", Outcome(() => SplitMana(10, 2)));
            yield return ("SplitMana(10, 0)", "throws ArgumentException", Outcome(() => SplitMana(10, 0)));
            yield return ("SplitMana(10, -1)", "throws ArgumentException", Outcome(() => SplitMana(10, -1)));
            yield return ("SafeSplit(10, 0)", "catches ArgumentException, returns 0", Caught(() => SafeSplit(10, 0)));
            yield return ("SafeSplit(9, 3)", "returns 3", Outcome(() => SafeSplit(9, 3)));
        }

        private static string Caught(global::System.Func<int> spell)
        {
            var thread = global::System.Environment.CurrentManagedThreadId;
            var raised = false;
            global::System.EventHandler<global::System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> watch = (_, thrown) =>
            {
                if (global::System.Environment.CurrentManagedThreadId == thread && thrown.Exception.GetType() == typeof(global::System.ArgumentException))
                {
                    raised = true;
                }
            };
            global::System.AppDomain.CurrentDomain.FirstChanceException += watch;
            string outcome;
            try
            {
                outcome = Outcome(spell);
            }
            finally
            {
                global::System.AppDomain.CurrentDomain.FirstChanceException -= watch;
            }

            if (outcome.StartsWith("throws"))
            {
                return outcome;
            }

            return raised ? "catches ArgumentException, " + outcome : outcome + " without catching anything";
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
