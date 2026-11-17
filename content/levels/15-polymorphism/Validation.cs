public static class Validation
{
    public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        yield return ("typeof(Creature)", "class", Kind(typeof(Creature)));
        yield return ("typeof(Cat)", "class", Kind(typeof(Cat)));
        yield return ("typeof(Owl)", "class", Kind(typeof(Owl)));
        yield return ("typeof(Cat).BaseType", "Creature", typeof(Cat).BaseType?.Name ?? "none");
        yield return ("typeof(Owl).BaseType", "Creature", typeof(Owl).BaseType?.Name ?? "none");

        // The array is typed object so that a Cat or an Owl that does not derive from Creature is reported by a check
        // instead of stopping the compilation; the calls still go through a Creature reference.
        object[] creatures = { new Creature(), new Cat(), new Owl() };
        yield return ("creatures[0].Speak()", "\"...\"", Speak(creatures[0]));
        yield return ("creatures[1].Speak()", "\"Meow\"", Speak(creatures[1]));
        yield return ("creatures[2].Speak()", "\"Hoot\"", Speak(creatures[2]));

        yield return ("Creature.Speak", "virtual", BaseSpeakKind());
        yield return ("Cat.Speak", "override", DerivedSpeakKind(typeof(Cat)));
        yield return ("Owl.Speak", "override", DerivedSpeakKind(typeof(Owl)));
    }

    private static string Kind(global::System.Type type) => type.IsValueType ? "struct" : type.IsInterface ? "interface" : type.IsAbstract ? "abstract class" : "class";

    private static string Speak(object creature) =>
        creature is Creature speaker ? Shown(speaker.Speak()) : "not a Creature";

    private static global::System.Reflection.MethodInfo? DeclaredSpeak(global::System.Type type) =>
        type.GetMethod(
            "Speak",
            global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.DeclaredOnly,
            null,
            global::System.Type.EmptyTypes,
            null);

    private static string BaseSpeakKind()
    {
        var speak = DeclaredSpeak(typeof(Creature));
        return speak switch
        {
            null => "no public Speak()",
            { IsAbstract: true } => "abstract",
            { IsVirtual: true } => "virtual",
            _ => "not virtual",
        };
    }

    // GetBaseDefinition is the method itself for `new` (a hiding method), and the first virtual declaration for an override.
    private static string DerivedSpeakKind(global::System.Type type)
    {
        if (!type.IsSubclassOf(typeof(Creature)))
        {
            return "not a Creature";
        }

        var speak = DeclaredSpeak(type);
        if (speak is null)
        {
            return DeclaredSpeak(typeof(Creature)) is null ? "no public Speak()" : "inherited (missing override)";
        }

        return speak.GetBaseDefinition().DeclaringType == typeof(Creature) ? "override" : "hides (missing override)";
    }

    private static string Shown(string? text) => text is null ? "null" : "\"" + text + "\"";
}
