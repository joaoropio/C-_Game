class Creature
{
    public virtual string Speak()
    {
        return "...";
    }
}

class Cat : Creature
{
    public override string Speak()
    {
        return "Meow";
    }
}

class Owl : Creature
{
    public override string Speak()
    {
        return "Hoot";
    }
}
