namespace CodeQuest.Core;

public sealed record LocalizedText(string Pt, string En)
{
    public string Get(Language language) => language switch
    {
        Language.Pt => Pt,
        Language.En => En,
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };
}
