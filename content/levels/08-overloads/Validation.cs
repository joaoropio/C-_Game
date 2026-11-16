public static partial class Spell
{
    public static class Validation
    {
        public static IEnumerable<(string Case, string Expected, string Actual)> Run()
        {
            yield return ("Fireball(5)", "10", $"{Fireball(5)}");
            yield return ("Fireball(5, 3)", "30", $"{Fireball(5, 3)}");
            yield return ("Shield(4)", "4", $"{Shield(4)}");
            yield return ("Shield(4, 3)", "12", $"{Shield(4, 3)}");
        }
    }
}
