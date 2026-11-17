using CodeQuest.Core;

namespace CodeQuest.Engine;

internal static class PlayerMessages
{
    public static readonly LocalizedText NoSpellWritten = new(
        "Ainda não escreveste nenhum feitiço.",
        "You haven't written any spell yet.");

    public static LocalizedText ExtraClosingBrace(int line) => new(
        $"Tens uma \"}}\" a mais na linha {line}.",
        $"There's an extra \"}}\" on line {line}.");

    public static LocalizedText UnclosedBrace(int line) => new(
        $"Falta fechar uma \"{{\" aberta na linha {line}.",
        $"A \"{{\" opened on line {line} is never closed.");

    public static LocalizedText MissingVariable(string name) => new(
        $"Falta declarar a variável \"{name}\".",
        $"You still need to declare the variable \"{name}\".");

    public static readonly LocalizedText MissingReturn = new(
        "O feitiço tem de devolver um valor com return.",
        "The spell must return a value with return.");

    public static LocalizedText UnassignedVariable(string name) => new(
        $"Dá um valor à variável \"{name}\" antes de a usares.",
        $"Give the variable \"{name}\" a value before you use it.");

    public static LocalizedText TypeMismatch(string? name, string actualType, string expectedType) => name is null
        ? new LocalizedText(
            $"O valor é do tipo {actualType}, mas aqui é preciso {expectedType}.",
            $"The value is of type {actualType}, but {expectedType} is needed here.")
        : new LocalizedText(
            $"O valor de \"{name}\" é do tipo {actualType}, mas aqui é preciso {expectedType}.",
            $"The value of \"{name}\" is of type {actualType}, but {expectedType} is needed here.");

    public static readonly LocalizedText UnreadableSpell = new(
        "Há um erro no feitiço que a porta não consegue ler. Verifica tipos e nomes.",
        "There is an error in the spell that the door can't read. Check types and names.");

    public static LocalizedText MissingMethod(string name) => new(
        $"Falta o método \"{name}\".",
        $"The method \"{name}\" is missing.");

    public static LocalizedText MissingMethodOnType(string type, string name) => new(
        $"\"{type}\" não tem nenhum método \"{name}\". Verifica o nome.",
        $"\"{type}\" has no method \"{name}\". Check the name.");

    public static readonly LocalizedText CountNotLength = new(
        "Numa lista usa Count, não Length.",
        "On a list use Count, not Length.");

    public static readonly LocalizedText LengthNotCount = new(
        "Num array (ou texto) usa Length, não Count.",
        "On an array (or text) use Length, not Count.");

    public static readonly LocalizedText StatementsBelongInMethods = new(
        "Aqui só podes escrever métodos: põe as instruções dentro de um método.",
        "Only methods go here: put statements inside a method.");

    public static LocalizedText MissingOverloadForType(string name, string type) => new(
        $"Falta uma versão de \"{name}\" que receba {type}.",
        $"There's no version of \"{name}\" that takes {type}.");

    public static LocalizedText MissingOverloadForCount(string name, int count) => count == 1
        ? new LocalizedText(
            $"Falta uma versão de \"{name}\" que receba 1 argumento.",
            $"There's no version of \"{name}\" that takes 1 argument.")
        : new LocalizedText(
            $"Falta uma versão de \"{name}\" que receba {count} argumentos.",
            $"There's no version of \"{name}\" that takes {count} arguments.");

    public static LocalizedText ReservedName(string name) => new(
        $"O nome {name} está reservado; escolhe outro nome.",
        $"The name {name} is reserved; pick another name.");

    public static LocalizedText WrongArgumentCount(string name, int count) => count == 1
        ? new LocalizedText(
            $"O método \"{name}\" não recebe 1 argumento.",
            $"The method \"{name}\" doesn't take 1 argument.")
        : new LocalizedText(
            $"O método \"{name}\" não recebe {count} argumentos.",
            $"The method \"{name}\" doesn't take {count} arguments.");

    public static LocalizedText MissingArguments(string name, string parameter) => new(
        $"Falta o valor de \"{parameter}\" na chamada ao método \"{name}\".",
        $"Missing arguments in the call to method \"{name}\": no value for \"{parameter}\".");

    public static LocalizedText DuplicateMethod(string name) => new(
        $"Já existe um método \"{name}\" com estes mesmos parâmetros.",
        $"There is already a method \"{name}\" with these same parameters.");

    public static LocalizedText MissingStatic(string name) => new(
        $"Falta static no método \"{name}\".",
        $"The method \"{name}\" needs to be static.");

    public static LocalizedText MissingReturnType(string name) => new(
        $"Falta o tipo do resultado do método \"{name}\" (por exemplo int, ou void se não devolver nada).",
        $"The method \"{name}\" is missing its return type (for example int, or void if it returns nothing).");

    public static readonly LocalizedText NamespaceNotAllowed = new(
        "Não uses namespace aqui.",
        "Don't use a namespace here.");

    public static LocalizedText ForbiddenSpell(string symbol, int line) => new(
        $"Esse feitiço é proibido nesta masmorra: \"{symbol}\" (linha {line}).",
        $"That spell is forbidden in this dungeon: \"{symbol}\" (line {line}).");

    public static LocalizedText StackallocNotAllowed(int line) => new(
        $"stackalloc não é permitido nesta masmorra (linha {line}).",
        $"stackalloc is not allowed in this dungeon (line {line}).");

    public static readonly LocalizedText TypesNotAllowed = new(
        "Ainda não podes criar tipos: escreve só métodos static.",
        "You can't create types yet: write only static methods.");

    public static readonly LocalizedText OnlyMethodsAllowed = new(
        "Para já só podes escrever métodos static, sem campos, propriedades nem construtores.",
        "For now you can only write static methods, not fields, properties or constructors.");

    public static LocalizedText NotYetInThisWorld(string pt, string en) => new(
        $"Ainda não podes escrever {pt} aqui.",
        $"You can't write {en} here yet.");

    public static readonly LocalizedText OnlyTypesAtFileLevel = new(
        "Aqui só podes escrever classes (e outros tipos): põe o código dentro de uma classe.",
        "Only classes (and other types) go here: put your code inside a class.");

    public static LocalizedText StaticConstructorFailed(string type) => new(
        $"O construtor static de \"{type}\" (ou o valor inicial de um campo static) lançou um erro.",
        $"The static constructor of \"{type}\" (or a static field's initial value) threw an error.");

    public static LocalizedText AsyncNotAllowed(int line) => new(
        $"Ainda não podes usar async nesta masmorra (linha {line}).",
        $"You can't use async in this dungeon yet (line {line}).");

    public static LocalizedText ExternNotAllowed(int line) => new(
        $"Ainda não podes usar extern nesta masmorra (linha {line}).",
        $"You can't use extern in this dungeon yet (line {line}).");

    public static LocalizedText MissingClass(string name) => new(
        $"Falta a classe \"{name}\".",
        $"The class \"{name}\" is missing.");

    public static LocalizedText UnknownType(string name) => new(
        $"Não existe nenhum tipo \"{name}\". Verifica o nome.",
        $"There is no type \"{name}\". Check the name.");

    public static LocalizedText NoConstructorForCount(string type, int count) => count == 1
        ? new LocalizedText(
            $"A classe \"{type}\" não tem um construtor que receba 1 argumento.",
            $"The class \"{type}\" has no constructor that takes 1 argument.")
        : new LocalizedText(
            $"A classe \"{type}\" não tem um construtor que receba {count} argumentos.",
            $"The class \"{type}\" has no constructor that takes {count} arguments.");

    public static LocalizedText MissingConstructorArguments(string type, string parameter) => new(
        $"Falta o valor de \"{parameter}\" ao criar \"{type}\".",
        $"Missing the value of \"{parameter}\" when creating \"{type}\".");

    public static LocalizedText PrivateMember(string member, string type) => new(
        $"\"{member}\" é private: só se pode usar dentro da classe \"{type}\".",
        $"\"{member}\" is private: it can only be used inside the class \"{type}\".");

    public static LocalizedText CannotAssignFromOutside(string member) => new(
        $"\"{member}\" não se pode mudar de fora da classe (o set é private ou não existe).",
        $"\"{member}\" can't be changed from outside the class (its set is private or missing).");

    public static LocalizedText NothingToOverride(string name) => new(
        $"Não há nenhum método \"{name}\" para substituir: falta virtual na classe base?",
        $"There is no method \"{name}\" to override: is virtual missing in the base class?");

    public static LocalizedText NotOverridable(string name) => new(
        $"\"{name}\" na classe base não é virtual nem abstract, por isso não se pode fazer override.",
        $"\"{name}\" in the base class is neither virtual nor abstract, so it can't be overridden.");

    public static LocalizedText MissingImplementation(string type, string member, string required) => new(
        $"A classe \"{type}\" ainda não implementa \"{member}\" (pedido por \"{required}\").",
        $"The class \"{type}\" doesn't implement \"{member}\" yet (required by \"{required}\").");

    public static LocalizedText AbstractInstance(string type) => new(
        $"Não se pode fazer new de \"{type}\": é abstrata (ou uma interface).",
        $"You can't use new on \"{type}\": it is abstract (or an interface).");

    public static LocalizedText InstanceMemberInStaticMethod(string name) => new(
        $"\"{name}\" pertence a cada objeto: um método static não lhe pode aceder sem um objeto (ou \"{name}\" tem de ser static).",
        $"\"{name}\" belongs to each object: a static method can't use it without an object (or make \"{name}\" static).");

    public static readonly LocalizedText DividedByZero = new(
        "Dividiste por zero.",
        "You divided by zero.");

    public static readonly LocalizedText UsedNull = new(
        "Usaste algo que está vazio (null).",
        "You used something that is empty (null).");

    public static readonly LocalizedText PositionDoesNotExist = new(
        "Tentaste usar uma posição que não existe.",
        "You tried to use a position that doesn't exist.");

    public static readonly LocalizedText KeyNotInDictionary = new(
        "Pediste uma chave que o dicionário não tem. Verifica com ContainsKey ou TryGetValue antes de a ler.",
        "You asked for a key the dictionary does not have. Check with ContainsKey or TryGetValue before reading it.");

    public static readonly LocalizedText UnexpectedFormat = new(
        "O texto não tem o formato esperado.",
        "The text isn't in the expected format.");

    public static readonly LocalizedText InfiniteRecursion = new(
        "O feitiço chama-se a si próprio sem parar (recursão infinita).",
        "The spell keeps calling itself and never stops (infinite recursion).");

    public static readonly LocalizedText SpellFailedWhileRunning = new(
        "O feitiço falhou durante a execução.",
        "The spell failed while running.");
}
