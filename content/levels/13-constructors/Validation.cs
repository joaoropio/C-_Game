public static class Validation
{
    public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        yield return ("typeof(Golem)", "class", typeof(Golem).IsValueType ? "struct" : "class");

        yield return ("new Golem(\"Rocky\", 3); Armor", "3", Read(new Golem("Rocky", 3), "Armor"));
        yield return ("new Golem(\"Pebble\", 5); Armor", "5", Read(new Golem("Pebble", 5), "Armor"));
        yield return ("new Golem(\"Pebble\", -2); Armor", "0", Read(new Golem("Pebble", -2), "Armor"));
        yield return ("new Golem(\"Rocky\", 3); Name", "\"Rocky\"", Read(new Golem("Rocky", 3), "Name"));
        yield return ("new Golem(\"Pebble\", 5); Name", "\"Pebble\"", Read(new Golem("Pebble", 5), "Name"));

        var cracked = new Golem("Rocky", 3);
        cracked.Crack();
        yield return ("new Golem(\"Rocky\", 3); Crack(); Armor", "2", Read(cracked, "Armor"));

        var crackedTwice = new Golem("Pebble", 5);
        crackedTwice.Crack();
        crackedTwice.Crack();
        yield return ("new Golem(\"Pebble\", 5); Crack(); Crack(); Armor", "3", Read(crackedTwice, "Armor"));

        var crackedToZero = new Golem("Rocky", 3);
        for (var i = 0; i < 4; i++)
        {
            crackedToZero.Crack();
        }

        yield return ("new Golem(\"Rocky\", 3); Crack() x4; Armor", "0", Read(crackedToZero, "Armor"));

        var bare = new Golem("Chip", 0);
        bare.Crack();
        yield return ("new Golem(\"Chip\", 0); Crack(); Armor", "0", Read(bare, "Armor"));

        var a = new Golem("A", 4);
        var b = new Golem("B", 4);
        a.Crack();
        yield return ("a = new Golem(\"A\", 4), b = new Golem(\"B\", 4); a.Crack(); a.Armor", "3", Read(a, "Armor"));
        yield return ("a = new Golem(\"A\", 4), b = new Golem(\"B\", 4); a.Crack(); b.Armor", "4", Read(b, "Armor"));
        yield return ("a = new Golem(\"A\", 4), b = new Golem(\"B\", 4); a.Crack(); a.Name", "\"A\"", Read(a, "Name"));

        yield return ("Armor setter", "private set", ArmorSetter());
        yield return ("Name setter", "no public set", NameSetter());
        yield return ("public fields", "none", PublicFields());
    }

    // Properties are read by reflection so that a field or a missing property is reported by a check.
    private static string Read(Golem golem, string property)
    {
        var info = typeof(Golem).GetProperty(property);
        return info is null ? $"no public property {property}" : Shown(info.GetValue(golem));
    }

    private static string ArmorSetter()
    {
        var property = typeof(Golem).GetProperty("Armor");
        if (property is null)
        {
            return "no property Armor";
        }

        return property.GetSetMethod(nonPublic: true) switch
        {
            null => "no set",
            { IsPublic: true } => "public set",
            { IsPrivate: true } => "private set",
            _ => "set that is neither public nor private",
        };
    }

    // A public field would let anyone change Armor; the engine's own __ fields are not the player's.
    private static string PublicFields()
    {
        var names = new global::System.Collections.Generic.List<string>();
        foreach (var field in typeof(Golem).GetFields(global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Static))
        {
            if (!field.Name.StartsWith("__", global::System.StringComparison.Ordinal))
            {
                names.Add(field.Name);
            }
        }

        return names.Count == 0 ? "none" : string.Join(", ", names);
    }

    private static string NameSetter()
    {
        var property = typeof(Golem).GetProperty("Name");
        if (property is null)
        {
            return "no property Name";
        }

        return property.GetSetMethod() is null ? "no public set" : "public set";
    }

    private static string Shown(object? value) => value switch
    {
        null => "null",
        string text => "\"" + text + "\"",
        int number => number.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };
}
