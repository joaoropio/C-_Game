public static class Validation
{
    public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        // The beasts are typed object so that a Griffin or Wyvern that is not a Beast, or a Griffin that does not implement
        // IFlyer, is reported by a check instead of stopping the compilation; Roar and FlySpeed are called by reflection
        // for the same reason.
        object griffin = new Griffin();
        object wyvern = new Wyvern();

        yield return ("typeof(Beast)", "abstract class", Kind(typeof(Beast)));
        yield return ("typeof(IFlyer)", "interface", Kind(typeof(IFlyer)));
        yield return ("typeof(Griffin)", "class", Kind(typeof(Griffin)));
        yield return ("typeof(Griffin).BaseType", "Beast", typeof(Griffin).BaseType?.Name ?? "none");
        yield return ("typeof(Wyvern).BaseType", "Beast", typeof(Wyvern).BaseType?.Name ?? "none");
        yield return ("new Griffin() is IFlyer", "true", Shown(typeof(IFlyer).IsInstanceOfType(griffin)));
        yield return ("new Wyvern() is IFlyer", "false", Shown(typeof(IFlyer).IsInstanceOfType(wyvern)));
        yield return ("Beast.Sound", "abstract", SoundKind());
        yield return ("IFlyer.FlySpeed", "abstract", FlySpeedKind());
        yield return ("IFlyer.FlySpeed implemented in", "Griffin", FlySpeedOwner());
        yield return ("typeof(Griffin).GetMethod(\"Roar\").DeclaringType", "Beast", RoarOwner());
        yield return ("new Griffin().Sound()", "\"screech\"", Call(griffin, typeof(Griffin), "Sound", Everything));
        yield return ("new Griffin().Roar()", "\"SCREECH!\"", Call(griffin, typeof(Beast), "Roar", global::System.Reflection.BindingFlags.Public));
        yield return ("new Wyvern().Roar()", "\"GROWL!\"", Call(wyvern, typeof(Beast), "Roar", global::System.Reflection.BindingFlags.Public));
        yield return ("((IFlyer)new Griffin()).FlySpeed()", "30", Call(griffin, typeof(IFlyer), "FlySpeed", global::System.Reflection.BindingFlags.Public));
    }

    // An interface method with a body, or one implemented by Beast, would let the Griffin skip writing FlySpeed.
    private static string FlySpeedKind() =>
        Find(typeof(IFlyer), "FlySpeed", global::System.Reflection.BindingFlags.Public) switch
        {
            null => "no FlySpeed()",
            { IsAbstract: true } => "abstract",
            _ => "not abstract",
        };

    private static string FlySpeedOwner()
    {
        if (!typeof(IFlyer).IsInterface)
        {
            return "not an interface";
        }

        if (!typeof(IFlyer).IsAssignableFrom(typeof(Griffin)))
        {
            return "not an IFlyer";
        }

        var map = typeof(Griffin).GetInterfaceMap(typeof(IFlyer));
        var index = global::System.Array.FindIndex(map.InterfaceMethods, method => method.Name == "FlySpeed");
        return index < 0 ? "no FlySpeed()" : map.TargetMethods[index].DeclaringType!.Name;
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
