public record Hero(string Name, int Health, double Mana, bool IsAlive);

public static class Spell
{
    public static Hero Cast()
    {
        // {{PLAYER_CODE}}
        return new Hero(name, health, mana, isAlive);
    }
}
