static int Heal(int health, int potion)
{
    return Math.Min(health + potion, 100);
}
