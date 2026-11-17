class Element
{
    public static Element Fire = new Element();
    public static Element Water = new Element();
    public static Element Earth = new Element();
}

class Rune
{
    public Element Element;
    public int Power;
}

class RuneMath
{
    public int Total(Rune[] runes)
    {
        return 0;
    }

    public Element Strongest(Rune[] runes)
    {
        return Element.Fire;
    }
}
