using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

// World 3 (file scope): friendly explanations for the object-oriented mistakes a beginner makes.
// "Hidden" cases are raised by the Validation (no line); the others sit in the player's own code (with a line).
public class OopErrorExplanationTests
{
    private const string Template = "// {{PLAYER_CODE}}";

    private readonly PlayerCompiler _compiler = new();

    private static string Validation(string probe) => $$"""
        public static class Validation
        {
            public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
            {
                yield return ("case", "1", global::System.Convert.ToString(Probe(), global::System.Globalization.CultureInfo.InvariantCulture)!);
            }

            private static object Probe()
            {
                {{probe}}
            }
        }
        """;

    private CompilationError Explained(string playerCode, string probe, bool insidePlayerCode)
    {
        var errors = _compiler.Compile(new RunRequest(Template, playerCode, Validation(probe))).Errors;
        return Assert.Single(errors, error => (error.Line is not null) == insidePlayerCode);
    }

    private static void AssertExplained(CompilationError error, string en, string pt)
    {
        Assert.Equal(en, error.Explanation!.En);
        Assert.Equal(pt, error.Explanation.Pt);
    }

    [Fact]
    public void MissingClass_UsedByValidation_Explained() =>
        AssertExplained(
            Explained("class Hero { }", "return new Golem();", insidePlayerCode: false),
            "The class \"Golem\" is missing.",
            "Falta a classe \"Golem\".");

    // In the player's own code the name may just be a misspelt library type, so "missing class" would mislead.
    [Theory]
    [InlineData("class Hero { Golem Pet; }", "Golem")]
    [InlineData("class Hero { Lsit<int> Xs; }", "Lsit")]
    public void UnknownType_InPlayerCode_ExplainedNeutrally(string playerCode, string name)
    {
        var error = Explained(playerCode, "return 1;", insidePlayerCode: true);

        AssertExplained(error, $"There is no type \"{name}\". Check the name.", $"Não existe nenhum tipo \"{name}\". Verifica o nome.");
        Assert.Equal(1, error.Line);
    }

    [Theory]
    [InlineData("class Golem { public Golem(int armor) { } }", "return new Golem(1, 2);", "2 arguments", "2 argumentos")]
    [InlineData("class Golem { }", "return new Golem(1);", "1 argument", "1 argumento")]
    public void ConstructorWithWrongArgumentCount_UsedByValidation_Explained(
        string playerCode, string probe, string countEn, string countPt) =>
        AssertExplained(
            Explained(playerCode, probe, insidePlayerCode: false),
            $"The class \"Golem\" has no constructor that takes {countEn}.",
            $"A classe \"Golem\" não tem um construtor que receba {countPt}.");

    [Fact]
    public void ConstructorWithWrongArgumentCount_InPlayerCode_ExplainedWithLine()
    {
        var error = Explained(
            "class Golem { public Golem(int armor) { } }\nclass Hero { object Pet = new Golem(1, 2); }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "The class \"Golem\" has no constructor that takes 2 arguments.",
            "A classe \"Golem\" não tem um construtor que receba 2 argumentos.");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void ConstructorMissingArgument_UsedByValidation_Explained() =>
        AssertExplained(
            Explained("class Golem { public Golem(int hp, int armor) { } }", "return new Golem(1);", insidePlayerCode: false),
            "Missing the value of \"armor\" when creating \"Golem\".",
            "Falta o valor de \"armor\" ao criar \"Golem\".");

    [Fact]
    public void ConstructorMissingArgument_InPlayerCode_ExplainedWithLine()
    {
        var error = Explained(
            "class Golem { public Golem(int hp, int armor) { } }\nclass Hero { object Pet = new Golem(1); }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "Missing the value of \"armor\" when creating \"Golem\".",
            "Falta o valor de \"armor\" ao criar \"Golem\".");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void MissingMethodArgument_StillExplainedAsMethodCall() =>
        AssertExplained(
            Explained("class Golem { public int Hit(int a, int b) => a; }", "return new Golem().Hit(1);", insidePlayerCode: false),
            "Missing arguments in the call to method \"Hit\": no value for \"b\".",
            "Falta o valor de \"b\" na chamada ao método \"Hit\".");

    [Theory]
    [InlineData("class Golem { int Armor = 3; }", "return new Golem().Armor;")]
    [InlineData("class Golem { int Armor() => 3; }", "return new Golem().Armor();")]
    public void PrivateMember_UsedByValidation_Explained(string playerCode, string probe) =>
        AssertExplained(
            Explained(playerCode, probe, insidePlayerCode: false),
            "\"Armor\" is private: it can only be used inside the class \"Golem\".",
            "\"Armor\" é private: só se pode usar dentro da classe \"Golem\".");

    [Fact]
    public void PrivateMember_InPlayerCode_ExplainedWithLine()
    {
        var error = Explained(
            "class Golem { int Armor = 3; }\nclass Hero { object V = new Golem().Armor; }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"Armor\" is private: it can only be used inside the class \"Golem\".",
            "\"Armor\" é private: só se pode usar dentro da classe \"Golem\".");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void PrivateConstructor_HasNoMisleadingExplanation()
    {
        var error = Explained("class Golem { Golem() { } }", "return new Golem();", insidePlayerCode: false);

        Assert.DoesNotContain("é private", error.Explanation!.Pt);
    }

    [Theory]
    [InlineData("class Golem { public int Armor { get; } = 3; }")]
    [InlineData("class Golem { public int Armor { get; private set; } }")]
    public void PropertyAssignedFromOutside_UsedByValidation_Explained(string playerCode) =>
        AssertExplained(
            Explained(playerCode, "var g = new Golem(); g.Armor = 5; return 1;", insidePlayerCode: false),
            "\"Armor\" can't be changed from outside the class (its set is private or missing).",
            "\"Armor\" não se pode mudar de fora da classe (o set é private ou não existe).");

    [Fact]
    public void PropertyAssignedFromOutside_InPlayerCode_ExplainedWithLine()
    {
        var error = Explained(
            "class Golem { public int Armor { get; } }\nclass Hero { void F(Golem g) { g.Armor = 5; } }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"Armor\" can't be changed from outside the class (its set is private or missing).",
            "\"Armor\" não se pode mudar de fora da classe (o set é private ou não existe).");
        Assert.Equal(2, error.Line);
    }

    [Theory]
    [InlineData("class Beast { }\nclass Griffin : Beast { public override string Speak() => \"x\"; }")]
    [InlineData("class Beast { }\nclass Griffin : Beast { public override int Level { get => 1; } }")]
    public void OverrideWithoutBaseMember_Explained(string playerCode)
    {
        var error = Explained(playerCode, "return 1;", insidePlayerCode: true);

        var name = playerCode.Contains("Speak", StringComparison.Ordinal) ? "Speak" : "Level";
        AssertExplained(
            error,
            $"There is no method \"{name}\" to override: is virtual missing in the base class?",
            $"Não há nenhum método \"{name}\" para substituir: falta virtual na classe base?");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void OverrideOfNonVirtualMember_Explained()
    {
        var error = Explained(
            "class Beast { public string Speak() => \"a\"; }\nclass Griffin : Beast { public override string Speak() => \"b\"; }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"Speak\" in the base class is neither virtual nor abstract, so it can't be overridden.",
            "\"Speak\" na classe base não é virtual nem abstract, por isso não se pode fazer override.");
        Assert.Equal(2, error.Line);
    }

    [Theory]
    [InlineData("abstract class Beast { public abstract string Sound(); }\nclass Griffin : Beast { }", "Sound", "Beast")]
    [InlineData("abstract class Beast { public abstract string Sound { get; } }\nclass Griffin : Beast { }", "Sound", "Beast")]
    [InlineData("interface IFlyer { void Fly(); }\nclass Griffin : IFlyer { }", "Fly", "IFlyer")]
    [InlineData("interface IBox<T> { T Get(); }\nclass Griffin : IBox<int> { }", "Get", "IBox")]
    public void MissingImplementation_Explained(string playerCode, string member, string required)
    {
        var error = Explained(playerCode, "return 1;", insidePlayerCode: true);

        AssertExplained(
            error,
            $"The class \"Griffin\" doesn't implement \"{member}\" yet (required by \"{required}\").",
            $"A classe \"Griffin\" ainda não implementa \"{member}\" (pedido por \"{required}\").");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void NewOnAbstractClass_UsedByValidation_Explained() =>
        AssertExplained(
            Explained("abstract class Beast { }", "return new Beast();", insidePlayerCode: false),
            "You can't use new on \"Beast\": it is abstract (or an interface).",
            "Não se pode fazer new de \"Beast\": é abstrata (ou uma interface).");

    [Fact]
    public void NewOnInterface_InPlayerCode_ExplainedWithLine()
    {
        var error = Explained("interface IFlyer { }\nclass Hero { object V = new IFlyer(); }", "return 1;", insidePlayerCode: true);

        AssertExplained(
            error,
            "You can't use new on \"IFlyer\": it is abstract (or an interface).",
            "Não se pode fazer new de \"IFlyer\": é abstrata (ou uma interface).");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void InstanceMethodCalledWithoutObject_UsedByValidation_ExplainedAsMissingStatic() =>
        AssertExplained(
            Explained("class Golem { public static int Hits() => 1; public int Hit() => 1; }", "return Golem.Hit();", insidePlayerCode: false),
            "The method \"Hit\" needs to be static.",
            "Falta static no método \"Hit\".");

    [Fact]
    public void InstanceFieldUsedInStaticMethod_InPlayerCode_Explained()
    {
        var error = Explained(
            "class Hero\n{\n    int hp = 3;\n    static int F() { return hp; }\n}",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"hp\" belongs to each object: a static method can't use it without an object (or make \"hp\" static).",
            "\"hp\" pertence a cada objeto: um método static não lhe pode aceder sem um objeto (ou \"hp\" tem de ser static).");
        Assert.Equal(4, error.Line);
    }

    [Fact]
    public void InstanceMethodCalledFromStaticMethod_InPlayerCode_ExplainedAsMissingStatic()
    {
        var error = Explained(
            "class Hero\n{\n    int Hit() => 3;\n    static int F() { return Hit(); }\n}",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(error, "The method \"Hit\" needs to be static.", "Falta static no método \"Hit\".");
    }

    // string.Length is not the player's member, so no "make it static" advice.
    [Fact]
    public void InstanceMemberOfLibraryTypeUsedAsStatic_HasNoExplanation()
    {
        var error = Explained(
            "class Hero\n{\n    int Length = 3;\n    static int F() { return string.Length; }\n}",
            "return 1;",
            insidePlayerCode: true);

        Assert.Null(error.Explanation);
    }
}
