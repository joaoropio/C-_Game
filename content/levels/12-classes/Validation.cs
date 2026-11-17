public static class Validation
{
    public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
    {
        var hitOnce = Make("Blob", 10);
        hitOnce.Hit(3);
        yield return ("Slime { Health = 10 }, Hit(3): Health", "7", Read(hitOnce, "Health"));

        var hitTooHard = Make("Blob", 10);
        hitTooHard.Hit(20);
        yield return ("Slime { Health = 10 }, Hit(20): Health", "0", Read(hitTooHard, "Health"));

        yield return ("Slime { Health = 0 }.IsAlive()", "false", Shown(Make("Blob", 0).IsAlive()));
        yield return ("Slime { Health = 4 }.IsAlive()", "true", Shown(Make("Blob", 4).IsAlive()));

        var a = Make("Blob", 10);
        var b = Make("Goo", 10);
        a.Hit(5);
        yield return ("Slimes a and b with Health 10, a.Hit(5): a.Health", "5", Read(a, "Health"));
        yield return ("Slimes a and b with Health 10, a.Hit(5): b.Health", "10", Read(b, "Health"));

        yield return ("Slime { Name = \"Blob\" }.Name", "\"Blob\"", Read(Make("Blob", 1), "Name"));
    }

    // The fields are read and written by reflection so that a wrong shape (private, static) is reported by a check.
    private static Slime Make(string name, int health)
    {
        var slime = new Slime();
        typeof(Slime).GetField("Name")?.SetValue(slime, name);
        typeof(Slime).GetField("Health")?.SetValue(slime, health);
        return slime;
    }

    private static string Read(Slime slime, string field)
    {
        var info = typeof(Slime).GetField(field);
        return info is null ? $"no public field {field}" : Shown(info.GetValue(slime));
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
