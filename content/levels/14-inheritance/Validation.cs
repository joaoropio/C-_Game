public static class Validation
{
    public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        var smaug = new Dragon("Smaug", 50, 7);
        var ember = new Dragon("Ember", 30, 4);

        yield return ("new Dragon(\"Smaug\", 50, 7).Describe()", "\"Smaug (50)\"", Shown(smaug.Describe()));
        yield return ("new Dragon(\"Ember\", 30, 4).Describe()", "\"Ember (30)\"", Shown(ember.Describe()));
        yield return ("new Creature(\"Rat\", 5).Describe()", "\"Rat (5)\"", Shown(new Creature("Rat", 5).Describe()));
        yield return ("new Dragon(\"Smaug\", 50, 7).Breathe()", "14", Shown(smaug.Breathe()));
        yield return ("new Dragon(\"Ember\", 30, 4).Breathe()", "8", Shown(ember.Breathe()));
        yield return ("new Dragon(\"Wisp\", 9, 0).Breathe()", "0", Shown(new Dragon("Wisp", 9, 0).Breathe()));
        yield return ("new Dragon(\"Smaug\", 50, 7).Name", "\"Smaug\"", Read(smaug, "Name"));
        yield return ("new Dragon(\"Smaug\", 50, 7).Health", "50", Read(smaug, "Health"));
        yield return ("new Dragon(\"Ember\", 30, 4).Health", "30", Read(ember, "Health"));

        object dragon = smaug;
        yield return ("new Dragon(\"Smaug\", 50, 7) is Creature", "true", Shown(dragon is Creature));
        yield return ("typeof(Dragon).BaseType", "Creature", typeof(Dragon).BaseType?.Name ?? "none");
        yield return ("Describe declared in", "Creature", DescribeDeclaredIn());
    }

    // Name and Health are read by reflection so that a field and a property are both accepted.
    private static string Read(object target, string member)
    {
        var type = target.GetType();
        var value = type.GetProperty(member)?.GetValue(target) ?? type.GetField(member)?.GetValue(target);
        return value is null ? $"no public member {member}" : Shown(value);
    }

    private static string DescribeDeclaredIn()
    {
        var method = typeof(Dragon).GetMethod("Describe", global::System.Type.EmptyTypes);
        return method?.DeclaringType?.Name ?? "no method Describe";
    }

    private static string Shown(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        bool flag => flag ? "true" : "false",
        int number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };
}
