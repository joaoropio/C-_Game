public static class Validation
{
    public static IEnumerable<(string Pt, string En)> Run()
    {
        if (Spell.ChooseDoor("fire") != "left")
        {
            yield return ("A runa \"fire\" devia levar à porta \"left\".", "The rune \"fire\" should lead to the door \"left\".");
        }

        if (Spell.ChooseDoor("ice") != "right")
        {
            yield return ("A runa \"ice\" devia levar à porta \"right\".", "The rune \"ice\" should lead to the door \"right\".");
        }

        if (Spell.ChooseDoor("wind") != "center")
        {
            yield return ("A runa \"wind\" devia levar à porta \"center\".", "The rune \"wind\" should lead to the door \"center\".");
        }

        if (Spell.ChooseDoor("earth") != "back")
        {
            yield return ("Uma runa desconhecida, como \"earth\", devia fazer-te recuar: \"back\".", "An unknown rune, like \"earth\", should make you step back: \"back\".");
        }
    }
}
