using CodeQuest.Core;
using CodeQuest.Core.Running;
using CodeQuest.Engine;

namespace CodeQuest.Tests.Engine;

// World 3: the marker is alone at file level, the player writes whole types and the hidden Validation is a top-level class.
public class FileScopeRunnerTests
{
    private const string Template = "// {{PLAYER_CODE}}";

    private const string Recursion = "The spell keeps calling itself and never stops (infinite recursion).";

    private const string GoodHero = "class Hero\n{\n    public string Name { get; }\n    public Hero(string name) { Name = name; }\n    public string Greet() => \"Hi \" + Name;\n}";

    private readonly RoslynCodeRunner _runner = new();

    // The probe is the body of a method returning object; the single check passes when its text equals the expectation.
    private static string ValidationFor(string probe, string expected) => $$"""
        public static class Validation
        {
            public static global::System.Collections.Generic.IEnumerable<(string Case, string Expected, string Actual)> Run()
            {
                yield return ("probe", "{{expected}}", global::System.Convert.ToString(Probe(), global::System.Globalization.CultureInfo.InvariantCulture)!);
            }

            private static object Probe()
            {
                {{probe}}
            }
        }
        """;

    private Task<RunResult> Run(string playerCode, string probe = "return 1;", string expected = "1") =>
        _runner.RunAsync(new RunRequest(Template, playerCode, ValidationFor(probe, expected)), Language.En, CancellationToken.None);

    [Fact]
    public async Task Run_ClassWithMethodConstructorAndProperty_Passed() =>
        Assert.IsType<Passed>(await Run(GoodHero, "return new Hero(\"Ana\").Greet();", "Hi Ana"));

    [Fact]
    public async Task Run_Passed_CountsStatementsInsideAllBodies()
    {
        var result = Assert.IsType<Passed>(await Run(GoodHero, "return new Hero(\"Ana\").Greet();", "Hi Ana"));

        Assert.Equal(2, result.StatementCount);
    }

    [Theory]
    [InlineData("class Hero { public int Hp = 10; public System.Collections.Generic.List<int> Log = new(); public string Tag { get; } = \"x\"; }", "return new Hero().Hp + new Hero().Tag;", "10x")]
    [InlineData("class Hero(string name) { public string Name = name; }", "return new Hero(\"Ana\").Name;", "Ana")]
    [InlineData("struct P { public int X = 1; public P() { } }", "return new P().X;", "1")]
    [InlineData("class Counter { public static int N = 3; static Counter() { N++; } }", "return Counter.N;", "4")]
    [InlineData("class Outer { public class Inner { public int V = 7; } }", "return new Outer.Inner().V;", "7")]
    [InlineData("class Box<T> { public T Value; public Box(T v) { Value = v; } }", "return new Box<int>(5).Value;", "5")]
    [InlineData("interface IFoo { int Foo(); }\nabstract class Base : IFoo { public abstract int Foo(); }\nclass Impl : Base { public override int Foo() => 3; }\nenum Color { Red }", "return new Impl().Foo();", "3")]
    [InlineData("class Hero { public Hero() : this(1) { } public Hero(int x) { V = x; } public int V; int w = 2; }", "return new Hero().V;", "1")]
    [InlineData("class Hero { public override string ToString() => \"hero\"; }", "return $\"{new Hero()}\";", "hero")]
    public async Task Run_TypicalWorldThreeCode_Passed(string playerCode, string probe, string expected) =>
        Assert.IsType<Passed>(await Run(playerCode, probe, expected));

    [Fact]
    public async Task Run_ValidationSeesInternalTypesAndPublicMembers() =>
        Assert.IsType<Passed>(await Run(
            "class Hero { public int Hp = 4; private int secret = 1; }", "return new Hero().Hp;", "4"));

    [Theory]
    [InlineData("class Hero { public int F(int x) { return F(x); } }", "return new Hero().F(1);")]
    [InlineData("class Hero { public int F(int x) => F(x) + 1; }", "return new Hero().F(1);")]
    [InlineData("class Hero { int A() => B(); int B() => A(); public int Go() => A(); }", "return new Hero().Go();")]
    [InlineData("class Hero { public Hero() { new Hero(); } }", "return new Hero();")]
    [InlineData("class Hero { public Hero() => new Hero(); }", "return new Hero();")]
    [InlineData("class Hero { public Hero() { new Pet(); } }\nclass Pet { public Pet() { new Hero(); } }", "return new Hero();")]
    [InlineData("class Hero { public Hero() : this(1) { } public Hero(int x) { new Hero(x); } }", "return new Hero();")]
    [InlineData("class Hero { public int P { get { return P; } } }", "return new Hero().P;")]
    [InlineData("class Hero { public int P { get => P; } }", "return new Hero().P;")]
    [InlineData("class Hero { public int P => P; }", "return new Hero().P;")]
    [InlineData("class Hero { public int this[int i] => this[i]; }", "return new Hero()[0];")]
    [InlineData("class Hero { public int P { set { P = value; } } }", "var h = new Hero(); h.P = 1; return 0;")]
    [InlineData("class Hero { public int P { get; init { P = value; } } }", "return new Hero { P = 1 }.P;")]
    [InlineData("class Hero { public override string ToString() => $\"{this}\"; }", "return new Hero().ToString();")]
    [InlineData("class Hero { public override string ToString() => string.Join(\",\", new System.Collections.Generic.List<Hero> { this }); }", "return new Hero().ToString();")]
    [InlineData("class Hero { public override int GetHashCode() => new System.Collections.Generic.Dictionary<Hero, int> { { this, 1 } }.Count; }", "return new Hero().GetHashCode();")]
    [InlineData("class Hero { public override bool Equals(object? o) => new System.Collections.Generic.List<Hero> { this }.Contains(this); }", "return new Hero().Equals(null);")]
    [InlineData("class Hero : System.IComparable<Hero> { public int CompareTo(Hero? o) => CompareTo(o); }", "var l = new System.Collections.Generic.List<Hero> { new Hero(), new Hero() }; l.Sort(); return l.Count;")]
    [InlineData("class Hero { public int Go() { System.Func<int> f = null!; f = () => f(); return f(); } }", "return new Hero().Go();")]
    [InlineData("class Hero { A next = new A(); }\nclass A { public A() { new Hero(); } }", "return new Hero();")]
    [InlineData("class Box<T> { public object o = new Box<Box<T>>(); }", "return new Box<int>();")]
    [InlineData("class R : System.IDisposable { public void Dispose() { using var r = new R(); } }", "using var r = new R(); return 1;")]
    [InlineData("class B { public B() { Go(); } public virtual void Go() { } }\nclass D : B { public override void Go() { new D(); } }", "return new D();")]
    [InlineData("class A { public int F() { try { return F(); } finally { new A().F(); } } }", "return new A().F();")]
    public async Task Run_InfiniteRecursionThroughPlayerMembers_RuntimeErrorAndNextRunPasses(string playerCode, string probe)
    {
        var result = Assert.IsType<RuntimeError>(await Run(playerCode, probe));

        Assert.Equal(Recursion, result.Explanation.En);
        Assert.IsType<Passed>(await Run(GoodHero, "return new Hero(\"Ana\").Greet();", "Hi Ana"));
    }

    [Theory]
    [InlineData("class A { A next = new A(); }", "return new A();")]
    [InlineData("class A { public A next = new A(); public A() { } }", "return new A();")]
    [InlineData("class A { A next { get; } = new A(); }", "return new A();")]
    [InlineData("class A { B b = new B(); }\nclass B : A { }", "return new B();")]
    [InlineData("class A { int x = 1; A next = new A(); int y = 2; }", "return new A();")]
    [InlineData("class A<T> { A<T> next = new A<T>(); }", "return new A<int>();")]
    [InlineData("struct S(int a) { object t = new T(1); }\nstruct T(int b) { object s = new S(1); }", "return new S(1);")]
    [InlineData("class A { public static int X = F(); static int F() => X + F(); }", "return A.X;")]
    [InlineData("class A { public static A Instance = new A(); A next = new A(); }", "return A.Instance;")]
    public async Task Run_RecursionWithoutAnyWrittenMethod_RuntimeErrorAndNextRunPasses(string playerCode, string probe)
    {
        var result = Assert.IsType<RuntimeError>(await Run(playerCode, probe));

        Assert.Equal(Recursion, result.Explanation.En);
        Assert.IsType<Passed>(await Run(GoodHero, "return new Hero(\"Ana\").Greet();", "Hi Ana"));
    }

    [Theory]
    [InlineData("class Hero\n{\n    public Hero()\n    {\n        new Hero();\n    }\n}", "return new Hero();", 5)]
    [InlineData("class A\n{\n    A next = new A();\n}", "return new A();", 3)]
    [InlineData("class Hero\n{\n    public int P\n    {\n        get\n        {\n            return P;\n        }\n    }\n}", "return new Hero().P;", 7)]
    public async Task Run_RecursionReportsTheLineOfTheCall(string playerCode, string probe, int line)
    {
        var result = Assert.IsType<RuntimeError>(await Run(playerCode, probe));

        Assert.Equal(line, result.Line);
    }

    [Theory]
    [InlineData("class Hero { public Hero() { while (true) { } } }", "return new Hero();")]
    [InlineData("class Hero { public int P { get { while (true) { } } } }", "return new Hero().P;")]
    [InlineData("class Hero { public int P { set { for (;;) { } } } }", "var h = new Hero(); h.P = 1; return 0;")]
    [InlineData("class Hero { static Hero() { while (true) { } } }", "return new Hero();")]
    [InlineData("class Hero { int x = Spin(); static int Spin() { while (true) { } } }", "return new Hero();")]
    [InlineData("class Hero { public Hero() { Again: goto Again; } }", "return new Hero();")]
    public async Task Run_InfiniteLoopInAConstructorOrAccessor_TimedOut(string playerCode, string probe)
    {
        Assert.IsType<TimedOut>(await Run(playerCode, probe));
        Assert.IsType<Passed>(await Run(GoodHero, "return new Hero(\"Ana\").Greet();", "Hi Ana"));
    }

    [Theory]
    [InlineData("class Hero { public Hero() { try { new Hero(); } catch { } } }", "return new Hero();")]
    [InlineData("class Hero { public Hero() { try { new Hero(); } catch (System.Exception) { } finally { System.Console.Write(\"\"); } } }", "return new Hero();")]
    [InlineData("class Hero { public int P { get { try { return P; } catch { return 0; } } } }", "return new Hero().P;")]
    [InlineData("class Hero { public Hero() { try { new Hero(); } catch (System.Exception e) when (e.Message != \"\") { } } }", "return new Hero();")]
    public async Task Run_PlayerCatchInAConstructorOrAccessor_DoesNotSwallowTheGuard(string playerCode, string probe)
    {
        var result = Assert.IsType<RuntimeError>(await Run(playerCode, probe));

        Assert.Equal(Recursion, result.Explanation.En);
    }

    [Fact]
    public async Task Run_StaticConstructorThatThrows_RuntimeErrorWithExplanation()
    {
        var result = Assert.IsType<RuntimeError>(await Run(
            "class Hero\n{\n    static Hero()\n    {\n        throw new System.InvalidOperationException(\"boom\");\n    }\n}", "return new Hero();"));

        Assert.Equal("The static constructor of \"Hero\" (or a static field's initial value) threw an error.", result.Explanation.En);
        Assert.Equal("boom", result.Message);
        Assert.Equal(5, result.Line);
    }

    [Fact]
    public async Task Run_StaticFieldInitializerThatThrows_RuntimeErrorWithExplanation()
    {
        var result = Assert.IsType<RuntimeError>(await Run(
            "class Hero { public static int X = int.Parse(\"x\"); }", "return Hero.X;"));

        Assert.Equal(PlayerMessages.UnexpectedFormat, result.Explanation);
    }

    [Fact]
    public async Task Run_ExceptionWhoseMessageRecurses_RuntimeErrorNotAHostFailure()
    {
        var result = Assert.IsType<RuntimeError>(await Run(
            "class MyError : System.Exception { public override string Message => Message; }\nclass Hero { public Hero() { throw new MyError(); } }",
            "return new Hero();"));

        Assert.NotNull(result.Explanation);
    }

    [Fact]
    public async Task Run_ExceptionWhoseMessageLoops_RuntimeErrorNotAHostFailure() =>
        Assert.IsType<RuntimeError>(await Run(
            "class MyError : System.Exception { public override string Message { get { while (true) { } } } }\nclass Hero { public Hero() { throw new MyError(); } }",
            "return new Hero();"));

    [Theory]
    [InlineData("namespace Game { class A { } }", "Don't use a namespace here.")]
    [InlineData("namespace Game;\nclass A { }", "Don't use a namespace here.")]
    [InlineData("class A { ~A() { } }", "You can't write destructors here yet.")]
    [InlineData("class A { public static A operator +(A a, A b) => a; }", "You can't write operators here yet.")]
    [InlineData("class A { public static implicit operator int(A a) => 0; }", "You can't write conversions here yet.")]
    [InlineData("class A { public event System.Action Changed; }", "You can't write events here yet.")]
    [InlineData("class A { public event System.Action E { add { } remove { } } }", "You can't write events here yet.")]
    [InlineData("record Pair(int A, int B);", "You can't write records here yet.")]
    [InlineData("record struct Pair(int A);", "You can't write records here yet.")]
    [InlineData("delegate int Op(int a);", "You can't write delegates here yet.")]
    [InlineData("class A { async void F() { } }", "You can't use async in this dungeon yet (line 3).")]
    [InlineData("class A { void F() { System.Action a = async () => { }; } }", "You can't use async in this dungeon yet (line 3).")]
    [InlineData("class A { extern void F(); }", "You can't use extern in this dungeon yet (line 3).")]
    [InlineData("class A { extern A(); }", "You can't use extern in this dungeon yet (line 3).")]
    [InlineData("class A { extern int P { get; } }", "You can't use extern in this dungeon yet (line 3).")]
    [InlineData("class Validation { }", "The name Validation is reserved; pick another name.")]
    [InlineData("class Validation<T> { }", "The name Validation is reserved; pick another name.")]
    [InlineData("interface Validation { }", "The name Validation is reserved; pick another name.")]
    [InlineData("class A { class Validation { } }", "The name Validation is reserved; pick another name.")]
    [InlineData("struct __S { }", "The name __S is reserved; pick another name.")]
    [InlineData("enum __E { X }", "The name __E is reserved; pick another name.")]
    [InlineData("class __LoopGuard { }", "The name __LoopGuard is reserved; pick another name.")]
    [InlineData("int x = 5;", "Only classes (and other types) go here: put your code inside a class.")]
    [InlineData("static int Twice(int x) => x * 2;", "Only classes (and other types) go here: put your code inside a class.")]
    public async Task Run_NotAllowedInWorldThree_CodeRejectedOnItsLine(string playerCode, string reason)
    {
        var result = Assert.IsType<CodeRejected>(await Run("class Hero { }\n\n" + playerCode));

        Assert.Equal(3, result.Line);
        Assert.Equal(reason, result.Reason.En);
    }

    [Fact]
    public async Task Run_RejectionHasAPortugueseMessage()
    {
        var result = Assert.IsType<CodeRejected>(await Run("class A { ~A() { } }"));

        Assert.Equal("Ainda não podes escrever destrutores aqui.", result.Reason.Pt);
    }

    [Fact]
    public async Task Run_UsingAnAllowedNamespace_Passed() =>
        Assert.IsType<Passed>(await Run(
            "using System.Text;\nclass Hero { public string S() { return new StringBuilder().Append(\"x\").ToString(); } }",
            "return new Hero().S();",
            "x"));

    [Theory]
    [InlineData("using System.IO;\nclass Hero { }")]
    [InlineData("using F = System.IO.File;\nclass Hero { }")]
    [InlineData("using static System.Environment;\nclass Hero { }")]
    public async Task Run_UsingAForbiddenNamespaceOrType_CodeRejectedOnLineOne(string playerCode)
    {
        var result = Assert.IsType<CodeRejected>(await Run(playerCode));

        Assert.Equal(1, result.Line);
    }

    [Fact]
    public async Task Run_ExtraClosingBrace_CodeRejected()
    {
        var result = Assert.IsType<CodeRejected>(await Run(GoodHero + "\n}"));

        Assert.Equal(7, result.Line);
    }

    [Fact]
    public async Task Run_CompileErrorInsideAConstructor_ReportsTheLineAndColumnAsWritten()
    {
        var result = Assert.IsType<CompilationFailed>(await Run(
            "class Hero\n{\n    int hp = 3;\n    public Hero()\n    {\n        hp = undefined;\n    }\n}"));

        var error = Assert.Single(result.Errors);
        Assert.Equal(6, error.Line);
        Assert.Equal(14, error.Column);
    }

    [Fact]
    public async Task Run_PlayerTypeNamedLikeALibraryType_StillWorks()
    {
        var result = await Run(
            "class List { public int Count = 2; }\nclass Exception { }\nclass Hero { public int F() => new List().Count; }",
            "return new Hero().F();",
            "2");

        Assert.IsType<Passed>(result);
    }

    [Fact]
    public async Task Run_GuardFieldDoesNotLeakIntoTheValidationShape() =>
        Assert.IsType<Passed>(await Run(
            "class Hero { public int Hp = 1; }",
            "return string.Join(\",\", typeof(Hero).GetFields(global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Public).Where(f => !f.Name.StartsWith(\"__\")).Select(f => f.Name));",
            "Hp"));

    // Probes from the review of Task 1: every one of these crashed the host with a stack overflow.
    [Theory]
    [InlineData("class B { public B(object o) { } }\nclass A : B { public A() : base(new A()) { } }", "return new A();")]
    [InlineData("class A { public A() : this(new A(), 0) { } public A(A a, int i) { } }", "return new A();")]
    [InlineData("class B { public B(object o) { } }\nclass A(int x) : B(new A(x)) { }", "return new A(1);")]
    [InlineData("struct S { public S() : this(new S(), 1) { } public S(S s, int i) { } }", "return new S();")]
    [InlineData("struct S(int a) { public S() : this(new S().A) { } public int A = a; }", "return new S();")]
    [InlineData("class A { public A() : this(Make()) { } public A(A a) { } static A Make() => new A(); }", "return new A();")]
    [InlineData("class C<T> { public static object S = C<C<T>>.S; }", "return C<int>.S ?? 1;")]
    [InlineData("class C<T> { static C() { } public static object S = C<C<T>>.S; }", "return C<int>.S ?? 1;")]
    [InlineData("interface I<T> { static object S = I<I<T>>.S; }", "return I<int>.S ?? 1;")]
    [InlineData("struct C<T> { public static object S = C<C<T>>.S; }", "return C<int>.S ?? 1;")]
    [InlineData("class A { static A() { F(); } static void F() => F(); public static int X; }", "return A.X;")]
    [InlineData("class A { public A() { try { } finally { new A(); } } }", "return new A();")]
    [InlineData("partial class A { int x = 1; }\npartial class A { A n = new A(); }", "return new A();")]
    [InlineData("class O { public struct S { public object o = new O(); public S() { } } S s = new S(); }", "return new O();")]
    [InlineData("class A<T> { public A() { } public object M() => new A<A<T>>().M(); }", "return new A<int>().M();")]
    [InlineData("class A { public System.Collections.Generic.IEnumerable<int> P { get { yield return P.First(); } } }", "return new A().P.First();")]
    [InlineData("interface I { int F() => F(); }\nclass A : I { }", "return ((I)new A()).F();")]
    [InlineData("interface I { int F(); }\nclass A : I { int I.F() => ((I)this).F(); }", "return ((I)new A()).F();")]
    [InlineData("class A(int n) { public int F() => new A(n).F(); }", "return new A(1).F();")]
    [InlineData("class A { static int Y = 1; static int X = Y; static int Z = X + F(); static int F() => F(); public static int G => Z; }", "return A.G;")]
    [InlineData("class A { public int this[int i] { get => this[i]; } }", "return new A()[0];")]
    [InlineData("class A { public A() { int F() => F(); F(); } }", "return new A();")]
    public async Task Run_ReviewedRecursionPrograms_RuntimeErrorAndNextRunPasses(string playerCode, string probe)
    {
        var result = Assert.IsType<RuntimeError>(await Run(playerCode, probe));

        Assert.Equal(Recursion, result.Explanation.En);
        Assert.IsType<Passed>(await Run(GoodHero, "return new Hero(\"Ana\").Greet();", "Hi Ana"));
    }

    [Theory]
    [InlineData("class A { public A() : this(null) { } public A(string s) { V = 1; } public int V; }")]
    [InlineData("class A { public A() : this(x => x + 1) { } public A(System.Func<int, int> f) { V = f(0); } public int V; }")]
    [InlineData("class A { public A() : this(default) { } public A(string s) { V = 1; } public int V; }")]
    [InlineData("class A { public A() : this(Inc) { } public A(System.Func<int, int> f) { V = f(0); } static int Inc(int x) => x + 1; public int V; }")]
    [InlineData("class A { public A() : this(new()) { } public A(System.Text.StringBuilder s) { V = 1; } public int V; }")]
    [InlineData("class A { public A() : this(b: 0, a: 1) { } public A(int a, int b) { V = a; } public int V; }")]
    [InlineData("class A { public A() : this(1, 2, 3) { } public A(params int[] xs) { V = xs.Length - 2; } public int V; }")]
    [InlineData("class A { public A() : this(\n        1) { } public A(int x) { V = x; } public int V; }")]
    [InlineData("struct A { public A() : this(1) { } public A(int x) { V = x; } public int V; }")]
    [InlineData("class A { public A() : this(1, new A(2)) { } public A(int x, A? other = null) { V = x; } public int V; }")]
    public async Task Run_ThisInitializerArguments_StillWorkWhenGuarded(string playerCode) =>
        Assert.IsType<Passed>(await Run(playerCode, "return new A().V;", "1"));

    [Theory]
    [InlineData("class A { public A(int x) : this(ref x, 1) { } public A(ref int x, int y) { } }")]
    [InlineData("class A { public A() : this(out var x) { } public A(out int x) { x = 1; } }")]
    [InlineData("class A { public A() : this(in 1) { } public A(in int x) { } }")]
    public async Task Run_RefOutOrInInAThisInitializer_CodeRejected(string playerCode)
    {
        var result = Assert.IsType<CodeRejected>(await Run("class Hero { }\n\n" + playerCode));

        Assert.Equal(3, result.Line);
        Assert.Equal("You can't write ref, out or in here yet.", result.Reason.En);
    }

    [Fact]
    public async Task Run_ValidationThatCatchesEverything_CannotSwallowATrippedGuard()
    {
        var result = await Run(
            "class A { public int F() => F(); }",
            "try { return new A().F(); } catch (global::System.Exception) { return 1; }");

        Assert.IsNotType<Passed>(result);
    }

    [Fact]
    public async Task Run_ValidationThatCatchesALimit_StillTimedOut() =>
        Assert.IsType<TimedOut>(await Run(
            "class A { public int F() { while (true) { } } }",
            "try { return new A().F(); } catch (global::System.Exception) { return 1; }"));

    [Theory]
    [InlineData("class System { }", "System")]
    [InlineData("class A { public A() { __LoopGuard.Enter(); } }", "__LoopGuard")]
    [InlineData("class A { public A() { throw new __LimitExceededException(); } }", "__LimitExceededException")]
    [InlineData("class A { public int __x; }", "__x")]
    [InlineData("class A { public void F() { int __y = 1; } }", "__y")]
    [InlineData("class A { public void F(int __p) { } }", "__p")]
    [InlineData("class A { public void F() { int @__z = 1; } }", "__z")]
    public async Task Run_ReservedNames_CodeRejectedOnTheirLine(string playerCode, string name)
    {
        var result = Assert.IsType<CodeRejected>(await Run("class Hero { }\n\n" + playerCode));

        Assert.Equal(3, result.Line);
        Assert.Equal($"The name {name} is reserved; pick another name.", result.Reason.En);
    }

    [Fact]
    public async Task Run_CompilerGeneratedAttributeWrittenByThePlayer_CodeRejected() =>
        Assert.IsType<CodeRejected>(await Run("[System.Runtime.CompilerServices.CompilerGenerated] class A { }"));

    [Fact]
    public async Task Run_InjectedGuardsDoNotTripTheForbiddenApiCheck() =>
        Assert.IsType<Passed>(await Run(
            "class A { static int S = 1; int i = 2; public int V => S + i; }\nstruct P { int j = 1; public P() { } }\ninterface I { static int T = 1; }",
            "return new A().V;",
            "3"));

    [Fact]
    public async Task Run_StructWithAConstructor_KeepsValueEquality() =>
        Assert.IsType<Passed>(await Run(
            "struct S { public int X; public S(int x) { X = x; } }",
            "return default(S).Equals(new S(0)) && default(S).GetHashCode() == new S(0).GetHashCode();",
            "True"));

    [Theory]
    [InlineData("class A\n{\n    int x = 1;\n    public int F() { int z = 0; return x / z; }\n}", "return new A().F();", 4)]
    [InlineData("class A\n{\n    static int s = 1;\n    public static int F() { int z = 0; return s / z; }\n}", "return A.F();", 4)]
    [InlineData("class A { // note\n    int x = 1;\n    public A() { throw new InvalidOperationException(); }\n}", "return new A();", 3)]
    [InlineData("class A\n{\n    public A() : this(\n        1)\n    { throw new InvalidOperationException(); }\n    public A(int x) { }\n}", "return new A();", 5)]
    [InlineData("class A\n{\n    public int P => throw new InvalidOperationException(\"x\");\n}", "return new A().P;", 3)]
    public async Task Run_LinesStayWhereThePlayerWroteThemAfterEveryInjection(string playerCode, string probe, int line)
    {
        var result = Assert.IsType<RuntimeError>(await Run(playerCode, probe));

        Assert.Equal(line, result.Line);
    }

    [Fact]
    public async Task Run_ColumnsStayWhereThePlayerWroteThemAfterAnInjection()
    {
        const string code = "class A { int x = 1; public int F() { return y; } }";
        var result = Assert.IsType<CompilationFailed>(await Run(code));

        Assert.Equal(code.IndexOf("y;", StringComparison.Ordinal) + 1, Assert.Single(result.Errors).Column);
    }

    [Fact]
    public async Task Run_RefReturningExpressionBodiedProperty_StillWorks() =>
        Assert.IsType<Passed>(await Run("class A { int x = 1; public ref int P => ref x; }", "return new A().P;", "1"));

    [Fact]
    public async Task Run_StaticInitializerThatFailsOnFirstUse_RuntimeErrorExplainingTheCause()
    {
        var result = Assert.IsType<RuntimeError>(await Run(
            "class A { static int X = 1 / int.Parse(\"0\"); public static int Y => X; }", "return A.Y;"));

        Assert.Equal(PlayerMessages.DividedByZero, result.Explanation);
    }
}
