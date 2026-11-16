static int SplitMana(int mana, int wizards)
{
    return mana / wizards;
}

static int SafeSplit(int mana, int wizards)
{
    return SplitMana(mana, wizards);
}
