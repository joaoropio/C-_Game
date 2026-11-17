public static class Validation
{
    public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        // The shape is reported by checks, Total and Strongest are called by reflection and the runes are filled in by name, so
        // that an Element that is not an enum, a Rune that is a class or a RuneMath that is not static show up as explained
        // checks and not as compiler errors about the Validation.
        yield return ("typeof(Element)", "enum", ElementKind());
        yield return ("typeof(Rune)", "struct", Kind(typeof(Rune)));
        yield return ("typeof(Rune).Element", "Element", ElementMember());
        yield return ("typeof(RuneMath)", "static class", Kind(typeof(RuneMath)));
        yield return ("var a = new Rune { Power = 1 }; var b = a; b.Power = 9; a.Power", "1", CopyThenChange());

        yield return ("RuneMath.Total([Fire 3, Water 5, Earth 2])", "10", Total(R("Fire", 3), R("Water", 5), R("Earth", 2)));
        yield return ("RuneMath.Total([Earth 4])", "4", Total(R("Earth", 4)));
        yield return ("RuneMath.Total([Water 6, Fire 6, Earth 7, Water 1])", "20", Total(R("Water", 6), R("Fire", 6), R("Earth", 7), R("Water", 1)));
        yield return ("RuneMath.Total([])", "0", Total());

        yield return ("RuneMath.Strongest([Fire 3, Water 5, Earth 2])", "Element.Water", Strongest(R("Fire", 3), R("Water", 5), R("Earth", 2)));
        yield return ("RuneMath.Strongest([Fire 7, Water 2, Earth 4])", "Element.Fire", Strongest(R("Fire", 7), R("Water", 2), R("Earth", 4)));
        yield return ("RuneMath.Strongest([Fire 1, Earth 9, Water 4])", "Element.Earth", Strongest(R("Fire", 1), R("Earth", 9), R("Water", 4)));
        yield return ("RuneMath.Strongest([Earth 1, Fire 8, Water 8])", "Element.Fire", Strongest(R("Earth", 1), R("Fire", 8), R("Water", 8)));
        yield return ("RuneMath.Strongest([Water 6, Earth 6, Fire 1])", "Element.Water", Strongest(R("Water", 6), R("Earth", 6), R("Fire", 1)));
    }

    private const global::System.Reflection.BindingFlags Instance =
        global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Instance;

    private static global::System.Type? ElementType() => typeof(Validation).Assembly.GetType("Element");

    private static string ElementKind() => ElementType() is { } type ? Kind(type) : "no Element";

    // "var b = a; b.Power = 9;": boxing a struct copies it, so b is boxed apart from a; for a class both are the same object.
    private static string CopyThenChange()
    {
        var a = R("Fire", 1);
        var b = a;
        object boxedB = b;
        if (!Set(boxedB, "Power", 9))
        {
            return "no public int Power";
        }

        object boxedA = a;
        return Shown(Get(boxedA, "Power"));
    }

    private static string ElementMember()
    {
        var type = (typeof(Rune).GetField("Element", Instance)?.FieldType) ?? typeof(Rune).GetProperty("Element", Instance)?.PropertyType;
        return type?.Name ?? "no public Element";
    }

    // A Rune with the element of that name and that power; a member that is missing or of another type is left as it is.
    private static Rune R(string element, int power)
    {
        object rune = new Rune();
        var value = ElementType()?.GetField(element, global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Static)?.GetValue(null);
        Set(rune, "Element", value);
        Set(rune, "Power", power);
        return (Rune)rune;
    }

    private static bool Set(object target, string member, object? value)
    {
        var type = target.GetType();
        if (value is null)
        {
            return false;
        }

        if (type.GetField(member, Instance) is { } field && field.FieldType.IsInstanceOfType(value))
        {
            field.SetValue(target, value);
            return true;
        }

        if (type.GetProperty(member, Instance) is { CanWrite: true } property && property.PropertyType.IsInstanceOfType(value))
        {
            property.SetValue(target, value);
            return true;
        }

        return false;
    }

    private static object? Get(object target, string member) =>
        target.GetType().GetField(member, Instance)?.GetValue(target) ?? target.GetType().GetProperty(member, Instance)?.GetValue(target);

    private static string Kind(global::System.Type type) =>
        type.IsEnum ? "enum"
        : type.IsValueType ? "struct"
        : type.IsInterface ? "interface"
        : type.IsAbstract && type.IsSealed ? "static class"
        : type.IsAbstract ? "abstract class"
        : "class";

    private static string Total(params Rune[] runes) => Call("Total", runes);

    private static string Strongest(params Rune[] runes) => Call("Strongest", runes);

    private static string Call(string name, Rune[] runes)
    {
        var method = typeof(RuneMath).GetMethod(
            name,
            global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Instance,
            null,
            [typeof(Rune[])],
            null);
        if (method is null)
        {
            return $"no public {name}(Rune[])";
        }

        if (!method.IsStatic)
        {
            return $"{name} is not static";
        }

        try
        {
            return Shown(method.Invoke(null, [runes]));
        }
        catch (global::System.Reflection.TargetInvocationException failure)
        {
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException!).Throw();
            throw;
        }
    }

    private static string Shown(object? value) => value switch
    {
        null => "null",
        global::System.Enum element => "Element." + element,
        int number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };
}
