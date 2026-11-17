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
            $"\"Golem\" has no constructor that takes {countEn}.",
            $"\"Golem\" não tem um construtor que receba {countPt}.");

    [Fact]
    public void ConstructorWithWrongArgumentCount_InPlayerCode_ExplainedWithLine()
    {
        var error = Explained(
            "class Golem { public Golem(int armor) { } }\nclass Hero { object Pet = new Golem(1, 2); }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"Golem\" has no constructor that takes 2 arguments.",
            "\"Golem\" não tem um construtor que receba 2 argumentos.");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void ConstructorMissingArgument_UsedByValidation_Explained() =>
        AssertExplained(
            Explained("class Golem { public Golem(int hp, int armor) { } }", "return new Golem(1);", insidePlayerCode: false),
            "No value for \"armor\" when creating \"Golem\".",
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
            "No value for \"armor\" when creating \"Golem\".",
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
            "\"Armor\" isn't public, so it can't be used here, outside the class \"Golem\". Is public missing?",
            "\"Armor\" não é public, por isso não se pode usar aqui, fora da classe \"Golem\". Falta public?");

    [Fact]
    public void PrivateMember_InPlayerCode_ExplainedWithLine()
    {
        var error = Explained(
            "class Golem { int Armor = 3; }\nclass Hero { object V = new Golem().Armor; }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"Armor\" isn't public, so it can't be used here, outside the class \"Golem\". Is public missing?",
            "\"Armor\" não é public, por isso não se pode usar aqui, fora da classe \"Golem\". Falta public?");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void PrivateConstructor_UsedByValidation_Explained() =>
        AssertExplained(
            Explained("class Golem { Golem() { } }", "return new Golem();", insidePlayerCode: false),
            "The constructor of \"Golem\" isn't public, so new can't use it outside the class. Put public before Golem(...).",
            "O construtor de \"Golem\" não é public, por isso new não o pode usar fora da classe. Põe public antes de Golem(...).");

    [Fact]
    public void PrivateConstructor_UsedTwiceByValidation_IsReportedOnce() =>
        Assert.Equal(
            "O construtor de \"Golem\" não é public, por isso new não o pode usar fora da classe. Põe public antes de Golem(...).",
            Explained("class Golem { Golem() { } }", "var a = new Golem(); var b = new Golem(); return 1;", insidePlayerCode: false).Explanation!.Pt);

    [Fact]
    public void BaseCallWithoutABaseClass_Explained() =>
        AssertExplained(
            Explained("class Dragon { public Dragon(string name, int health) : base(name, health) { } }", "return 1;", insidePlayerCode: true),
            "\"Dragon\" doesn't inherit from any class yet, so base(...) has nowhere to go: add \": BaseClass\" after its name.",
            "\"Dragon\" ainda não herda de nenhuma classe, por isso base(...) não tem para onde ir: acrescenta \": ClasseBase\" depois do nome.");

    [Fact]
    public void ConstructorWithAReturnType_Explained() =>
        AssertExplained(
            Explained("class Golem { public void Golem() { } }", "return 1;", insidePlayerCode: true),
            "A constructor has no return type: remove the void (or the type) before \"Golem\".",
            "Um construtor não tem tipo de retorno: tira o void (ou o tipo) antes de \"Golem\".");

    [Fact]
    public void PropertyWithoutSet_AssignedByValidation_Explained() =>
        AssertExplained(
            Explained("class Golem { public int Armor { get; } = 3; }", "var g = new Golem(); g.Armor = 5; return 1;", insidePlayerCode: false),
            "\"Armor\" can't be changed here: it has no set (or its set is private).",
            "\"Armor\" não se pode mudar aqui: não tem set (ou o set é private).");

    [Fact]
    public void PropertyWithPrivateSet_AssignedByValidation_Explained() =>
        AssertExplained(
            Explained("class Golem { public int Armor { get; private set; } }", "var g = new Golem(); g.Armor = 5; return 1;", insidePlayerCode: false),
            "\"Armor\" can't be changed from outside the class (its set is private or missing).",
            "\"Armor\" não se pode mudar de fora da classe (o set é private ou não existe).");

    [Fact]
    public void PropertyWithoutSet_AssignedInPlayerCode_ExplainedWithLine()
    {
        var error = Explained(
            "class Golem { public int Armor { get; } }\nclass Hero { void F(Golem g) { g.Armor = 5; } }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"Armor\" can't be changed here: it has no set (or its set is private).",
            "\"Armor\" não se pode mudar aqui: não tem set (ou o set é private).");
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void PropertyWithoutSet_AssignedInsideItsOwnClass_ExplainedNeutrally() =>
        AssertExplained(
            Explained("class Golem { public int Armor { get; } void F() { Armor = 5; } }", "return 1;", insidePlayerCode: true),
            "\"Armor\" can't be changed here: it has no set (or its set is private).",
            "\"Armor\" não se pode mudar aqui: não tem set (ou o set é private).");

    [Theory]
    [InlineData("class Beast { }\nclass Griffin : Beast { public override string Speak() => \"x\"; }", "Speak")]
    [InlineData("class Beast { }\nclass Griffin : Beast { public override int Level { get => 1; } }", "Level")]
    [InlineData("class Beast { public virtual string Speak() => \"a\"; }\nclass Griffin { public override string Speak() => \"x\"; }", "Speak")]
    [InlineData("class Beast { public virtual string Speak() => \"a\"; }\nclass Griffin : Beast { public override string Speak(int n) => \"x\"; }", "Speak")]
    public void OverrideWithoutBaseMember_Explained(string playerCode, string name)
    {
        var error = Explained(playerCode, "return 1;", insidePlayerCode: true);

        AssertExplained(
            error,
            $"There is no \"{name}\" in the base class to override: check the name, the parameters and that the class inherits from the base (\": Base\").",
            $"Não há nenhum \"{name}\" na classe base para substituir: verifica o nome, os parâmetros e se a classe herda da base (\": Base\").");
        Assert.Equal(2, error.Line);
    }

    [Theory]
    [InlineData("class Creature { public Creature(string name) { } }\nclass Dragon : Creature { public Dragon() { } }", 2)]
    [InlineData("class Creature { public Creature(string name) { } }\nclass Dragon : Creature { }", 2)]
    [InlineData("class Creature { public Creature(string name, int hp) { } }\nclass Dragon : Creature { public Dragon() : base(\"a\") { } }", 2)]
    public void MissingBaseConstructorArgument_ExplainedWithDerivedClass(string playerCode, int line)
    {
        var error = Explained(playerCode, "return 1;", insidePlayerCode: true);

        var parameter = playerCode.Contains("int hp", StringComparison.Ordinal) ? "hp" : "name";
        AssertExplained(
            error,
            $"\"{parameter}\" must be passed to the constructor of \"Creature\": use : base(...) in the constructor of \"Dragon\".",
            $"Falta passar \"{parameter}\" ao construtor de \"Creature\": usa : base(...) no construtor de \"Dragon\".");
        Assert.Equal(line, error.Line);
    }

    // Indexing a string is a World 1-2 mistake, not an encapsulation one.
    [Fact]
    public void AssigningToStringIndexer_HasNoObjectOrientedExplanation()
    {
        var error = Explained("class Hero { void F() { string s = \"ab\"; s[0] = 'c'; } }", "return 1;", insidePlayerCode: true);

        Assert.Null(error.Explanation);
    }

    [Fact]
    public void ProtectedMember_UsedOutside_Explained()
    {
        var error = Explained(
            "class Golem { protected int Armor = 3; }\nclass Hero { object V = new Golem().Armor; }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"Armor\" isn't public, so it can't be used here, outside the class \"Golem\". Is public missing?",
            "\"Armor\" não é public, por isso não se pode usar aqui, fora da classe \"Golem\". Falta public?");
    }

    [Fact]
    public void InstanceFieldUsedByValidation_ExplainedAsMissingStatic() =>
        AssertExplained(
            Explained("class Golem { public static int Hits = 1; public int Armor = 2; }", "return Golem.Armor;", insidePlayerCode: false),
            "\"Armor\" needs to be static.",
            "Falta static em \"Armor\".");

    [Fact]
    public void TupleTypeArgumentsInSignature_DoNotBreakTheName() =>
        AssertExplained(
            Explained(
                "class Golem<T> { public Golem(T v, int armor) { } }",
                "return new Golem<(int, int)>((1, 2));",
                insidePlayerCode: false),
            "No value for \"armor\" when creating \"Golem\".",
            "Falta o valor de \"armor\" ao criar \"Golem\".");

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

    [Fact]
    public void VirtualWithoutPublic_Explained()
    {
        var error = Explained(
            "class Beast { virtual string Speak() => \"a\"; }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "\"Speak\" is virtual (or override), so it must be public.",
            "\"Speak\" é virtual (ou override), por isso tem de ser public.");
        Assert.Equal(1, error.Line);
    }

    [Fact]
    public void OverrideWithWeakerAccess_Explained()
    {
        var error = Explained(
            "class Beast { public virtual string Speak() => \"a\"; }\nclass Griffin : Beast { protected override string Speak() => \"b\"; }",
            "return 1;",
            insidePlayerCode: true);

        AssertExplained(
            error,
            "The override of \"Speak\" must have the same access as in the base class (public).",
            "O override de \"Speak\" tem de ter o mesmo acesso que o da classe base (public).");
        Assert.Equal(2, error.Line);
    }


    // `override string Speak()` has no access modifier: that is the CS0621 case, and the access-mismatch error that
    // comes with it is dropped.
    [Fact]
    public void OverrideWithoutAnyAccess_OnlyTheMissingPublicIsExplained()
    {
        var errors = _compiler.Compile(new RunRequest(
            Template,
            "class Beast { public virtual string Speak() => \"a\"; }\nclass Griffin : Beast { override string Speak() => \"b\"; }",
            Validation("return 1;"))).Errors;

        var error = Assert.Single(errors);
        Assert.Equal(2, error.Line);
        Assert.Equal("\"Speak\" is virtual (or override), so it must be public.", error.Explanation!.En);
    }
    // The base method is private, so the override finds nothing to override; only the missing public is worth saying.
    [Fact]
    public void OverrideOfPrivateVirtual_OnlyTheMissingPublicIsExplained()
    {
        var errors = _compiler.Compile(new RunRequest(
            Template,
            "class Beast { virtual string Speak() => \"a\"; }\nclass Griffin : Beast { public override string Speak() => \"b\"; }",
            Validation("return 1;"))).Errors;

        var error = Assert.Single(errors);
        Assert.Equal(1, error.Line);
        Assert.Equal("\"Speak\" is virtual (or override), so it must be public.", error.Explanation!.En);
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
            $"\"Griffin\" doesn't implement \"{member}\" yet (required by \"{required}\").",
            $"\"Griffin\" ainda não implementa \"{member}\" (pedido por \"{required}\").");
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
            "\"hp\" belongs to each object: a static member can't use it without an object (or make \"hp\" static).",
            "\"hp\" pertence a cada objeto: um membro static não lhe pode aceder sem um objeto (ou \"hp\" tem de ser static).");
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

    [Theory]
    [InlineData("class Slime { public static int Hit(int n) => n; }", "return new Slime().Hit(1);")]
    [InlineData("class Slime { public static int Hp = 3; }", "return new Slime().Hp;")]
    public void StaticMemberUsedThroughObject_UsedByValidation_Explained(string playerCode, string probe)
    {
        var name = probe.Contains("Hit", StringComparison.Ordinal) ? "Hit" : "Hp";

        AssertExplained(
            Explained(playerCode, probe, insidePlayerCode: false),
            $"\"{name}\" is static: it belongs to the class, not to each object. Remove the static.",
            $"\"{name}\" é static: pertence à classe e não a cada objeto. Tira o static.");
    }

    [Fact]
    public void StaticMemberUsedThroughObject_InPlayerCode_ExplainedWithLine()
    {
        var error = Explained(
            "class Slime { public static int Hit(int n) => n; }\nclass Hero { object V = new Slime().Hit(1); }",
            "return 1;",
            insidePlayerCode: true);

        Assert.Equal("Tira o static.", error.Explanation!.Pt[^14..]);
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public void LibraryStaticMemberUsedThroughObject_HasNoExplanation()
    {
        var error = Explained(
            "class Hero { int IsNullOrEmpty = 1; bool F(string s) => s.IsNullOrEmpty(s); }",
            "return 1;",
            insidePlayerCode: true);

        Assert.Null(error.Explanation);
    }
}
