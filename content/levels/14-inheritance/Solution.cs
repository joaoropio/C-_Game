class Creature
{
    public string Name { get; }
    public int Health { get; }

    public Creature(string name, int health)
    {
        Name = name;
        Health = health;
    }

    public string Describe()
    {
        return Name + " (" + Health + ")";
    }
}

class Dragon : Creature
{
    private int fire;

    public Dragon(string name, int health, int fire) : base(name, health)
    {
        this.fire = fire;
    }

    public int Breathe()
    {
        return fire * 2;
    }
}
