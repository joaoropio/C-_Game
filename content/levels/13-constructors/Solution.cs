class Golem
{
    public string Name { get; }
    public int Armor { get; private set; }

    public Golem(string name, int armor)
    {
        Name = name;
        Armor = armor < 0 ? 0 : armor;
    }

    public void Crack()
    {
        if (Armor > 0)
        {
            Armor--;
        }
    }
}
