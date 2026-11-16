static int SplitMana(int mana, int wizards)
{
    if (wizards <= 0)
    {
        throw new ArgumentException("There must be at least one wizard.");
    }

    return mana / wizards;
}

static int SafeSplit(int mana, int wizards)
{
    try
    {
        return SplitMana(mana, wizards);
    }
    catch (ArgumentException)
    {
        return 0;
    }
}
