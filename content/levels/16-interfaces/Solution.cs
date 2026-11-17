interface IFlyer
{
    int FlySpeed();
}

abstract class Beast
{
    public abstract string Sound();

    public string Roar()
    {
        return Sound().ToUpper() + "!";
    }
}

class Griffin : Beast, IFlyer
{
    public override string Sound()
    {
        return "screech";
    }

    public int FlySpeed()
    {
        return 30;
    }
}

class Wyvern : Beast
{
    public override string Sound()
    {
        return "growl";
    }
}
