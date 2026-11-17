public static class Validation
{
    public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        // The griffin is typed object so that a Griffin that is not a Beast, or does not implement IFlyer, is reported by a
        // check instead of stopping the compilation; Roar and FlySpeed are called by reflection for the same reason.
        object griffin = new Griffin();

        yield return ("typeof(Beast)", "abstract class", Kind(typeof(Beast)));
        yield return ("typeof(IFlyer)", "interface", Kind(typeof(IFlyer)));
        yield return ("typeof(Griffin)", "class", Kind(typeof(Griffin)));
        yield return ("typeof(Griffin).BaseType", "Beast", typeof(Griffin).BaseType?.Name ?? "none");
        yield return ("new Griffin() is IFlyer", "true", Shown(typeof(IFlyer).IsInstanceOfType(griffin)));
        yield return ("Beast.Sound", "abstract", SoundKind());
        yield return ("typeof(Griffin).GetMethod(\"Roar\").DeclaringType", "Beast", RoarOwner());
        yield return ("new Griffin().Sound()", "\"screech\"", Call(griffin, typeof(Griffin), "Sound", Everything));
        yield return ("new Griffin().Roar()", "\"SCREECH!\"", Call(griffin, typeof(Beast), "Roar", global::System.Reflection.BindingFlags.Public));
        yield return ("((IFlyer)new Griffin()).FlySpeed()", "30", Call(griffin, typeof(IFlyer), "FlySpeed", global::System.Reflection.BindingFlags.Public));
    }

    private const global::System.Reflection.BindingFlags Everything =
        global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic;

    private static string Kind(global::System.Type type) => type.IsValueType ? "struct" : type.IsInterface ? "interface" : type.IsAbstract ? "abstract class" : "class";

    private static global::System.Reflection.MethodInfo? Find(global::System.Type type, string name, global::System.Reflection.BindingFlags access) =>
        type.GetMethod(name, access | global::System.Reflection.BindingFlags.Instance, null, global::System.Type.EmptyTypes, null);

    private static string SoundKind()
    {
        var sound = Find(typeof(Beast), "Sound", Everything | global::System.Reflection.BindingFlags.DeclaredOnly);
        return sound switch
        {
            null => "no Sound()",
            { IsAbstract: true } => "abstract",
            { IsVirtual: true } => "virtual",
            _ => "not abstract",
        };
    }

    // GetMethod gives the most derived declaration, so a Roar written again in Griffin (override or new) is not "Beast".
    private static string RoarOwner() =>
        Find(typeof(Griffin), "Roar", global::System.Reflection.BindingFlags.Public)?.DeclaringType?.Name ?? "no public Roar()";

    private static string Call(object target, global::System.Type owner, string name, global::System.Reflection.BindingFlags access)
    {
        if (!owner.IsInstanceOfType(target))
        {
            return owner == typeof(IFlyer) ? "not an IFlyer" : "not a Beast";
        }

        var method = Find(owner, name, access);
        if (method is null)
        {
            return $"no public {name}()";
        }

        try
        {
            return Shown(method.Invoke(target, null));
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
        string text => "\"" + text + "\"",
        bool flag => flag ? "true" : "false",
        int number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };
}
