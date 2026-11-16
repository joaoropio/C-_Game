static int Fireball(int power) => power * 2;

static int Fireball(int power, int targets) => power * 2 * targets;

static int Shield(int strength, int turns = 1) => strength * turns;
