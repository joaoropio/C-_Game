static List<string> PackBag(string[] loot)
{
    var bag = new List<string>();
    foreach (var item in loot)
    {
        if (item != "stone")
        {
            bag.Add(item);
        }
    }

    return bag;
}
