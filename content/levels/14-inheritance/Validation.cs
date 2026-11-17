public static class Validation
{
    public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        // The dragons are typed object so that a Dragon that does not derive from Creature is reported by a check
        // instead of stopping the compilation.
        object smaug = new Dragon("Smaug", 50, 7);
        object ember = new Dragon("Ember", 30, 4);
        object wisp = new Dragon("Wisp", 9, 0);

        yield return ("new Dragon(\"Smaug\", 50, 7).Describe()", "\"Smaug (50)\"", Describe(smaug));
        yield return ("new Dragon(\"Ember\", 30, 4).Describe()", "\"Ember (30)\"", Describe(ember));
        yield return ("new Creature(\"Rat\", 5).Describe()", "\"Rat (5)\"", Shown(new Creature("Rat", 5).Describe()));
        yield return ("new Dragon(\"Smaug\", 50, 7).Breathe()", "14", Shown(((Dragon)smaug).Breathe()));
        yield return ("new Dragon(\"Ember\", 30, 4).Breathe()", "8", Shown(((Dragon)ember).Breathe()));
        yield return ("new Dragon(\"Wisp\", 9, 0).Breathe()", "0", Shown(((Dragon)wisp).Breathe()));
        yield return ("new Dragon(\"Smaug\", 50, 7).Name", "\"Smaug\"", Read(smaug, "Name"));
        yield return ("new Dragon(\"Smaug\", 50, 7).Health", "50", Read(smaug, "Health"));
        yield return ("new Dragon(\"Ember\", 30, 4).Health", "30", Read(ember, "Health"));
        yield return ("new Dragon(\"Smaug\", 50, 7) is Creature", "true", Shown(smaug is Creature));
        yield return ("typeof(Creature)", "class", typeof(Creature).IsValueType ? "struct" : "class");
        yield return ("typeof(Dragon).GetMethod(\"Describe\").DeclaringType", "Creature", DeclaredIn("Describe"));
        yield return ("typeof(Dragon).GetMethod(\"Breathe\").DeclaringType", "Dragon", DeclaredIn("Breathe"));
    }

    private static string Describe(object dragon) =>
        dragon is Creature creature ? Shown(creature.Describe()) : "not a Creature";

    // Name and Health are read by reflection so that a field and a property are both accepted. Declaring the same
    // name again in Dragon (`public new int Name`) makes the lookup ambiguous.
    private static string Read(object target, string member)
    {
        var type = target.GetType();
        try
        {
            var value = type.GetProperty(member)?.GetValue(target) ?? type.GetField(member)?.GetValue(target);
            return value is null ? $"no public member {member}" : Shown(value);
        }
        catch (global::System.Reflection.AmbiguousMatchException)
        {
            return $"{member} is declared twice";
        }
    }

    private static string DeclaredIn(string method) =>
        typeof(Dragon).GetMethod(method, global::System.Type.EmptyTypes)?.DeclaringType?.Name ?? $"no method {method}";

    private static string Shown(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        bool flag => flag ? "true" : "false",
        int number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };
}
