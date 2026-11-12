namespace CodeQuest.Core.Progress;

public static class StarCalculator
{
    public static int Calculate(int world, int statementCount, int maxStatements, bool usedHint, bool sawSolution)
    {
        if (sawSolution)
        {
            return 0;
        }

        var stars = 1;
        var earnedSecond = world == 1 || !usedHint;
        if (earnedSecond)
        {
            stars++;
            if (statementCount <= maxStatements)
            {
                stars++;
            }
        }

        return stars;
    }
}
