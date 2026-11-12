namespace CodeQuest.Core.Grimoire;

public sealed record GrimoirePage(
    string Id,
    int World,
    LocalizedText Title,
    LocalizedText WhatItIs,
    string Syntax,
    string Example,
    IReadOnlyList<LocalizedText> CommonMistakes,
    string LearnUrl);
