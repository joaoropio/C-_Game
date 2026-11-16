static Dictionary<string, int> CountPotions(string[] potions)
{
    var counts = new Dictionary<string, int>();
    foreach (var potion in potions)
    {
        if (counts.ContainsKey(potion))
        {
            counts[potion]++;
        }
        else
        {
            counts[potion] = 1;
        }
    }

    return counts;
}
