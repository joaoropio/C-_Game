using CodeQuest.Core;

namespace CodeQuest.Engine;

internal static class PlayerMessages
{
    public static readonly LocalizedText NoSpellWritten = new(
        "Ainda não escreveste nenhum feitiço.",
        "You haven't written any spell yet.");

    public static LocalizedText ExtraClosingBrace(int line) => new(
        $"Tens uma `}}` a mais na linha {line}.",
        $"There's an extra `}}` on line {line}.");

    public static LocalizedText UnclosedBrace(int line) => new(
        $"Falta fechar uma `{{` aberta na linha {line}.",
        $"A `{{` opened on line {line} is never closed.");

    public static LocalizedText MissingVariable(string name) => new(
        $"Falta declarar a variável `{name}`.",
        $"You still need to declare the variable `{name}`.");
}
