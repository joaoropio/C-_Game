enum Element
{
    Fire,
    Water,
    Earth,
}

struct Rune
{
    public Element Element;
    public int Power;
}

static class RuneMath
{
    public static int Total(Rune[] runes)
    {
        int total = 0;
        foreach (Rune rune in runes)
        {
            total += rune.Power;
        }

        return total;
    }

    public static Element Strongest(Rune[] runes)
    {
        Rune best = runes[0];
        foreach (Rune rune in runes)
        {
            if (rune.Power > best.Power)
            {
                best = rune;
            }
        }

        return best.Element;
    }
}
