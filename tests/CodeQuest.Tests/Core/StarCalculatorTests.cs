using CodeQuest.Core.Progress;

namespace CodeQuest.Tests.Core;

public class StarCalculatorTests
{
    [Theory]
    [InlineData(1, 3, 4, false, false, 3)]
    [InlineData(1, 9, 4, true, false, 2)]
    [InlineData(2, 3, 4, true, false, 1)]
    [InlineData(2, 3, 4, false, false, 3)]
    [InlineData(2, 9, 4, false, false, 2)]
    [InlineData(1, 1, 4, false, true, 0)]
    public void Calculate_AppliesStarRules(int world, int statements, int max, bool usedHint, bool sawSolution, int expected)
    {
        Assert.Equal(expected, StarCalculator.Calculate(world, statements, max, usedHint, sawSolution));
    }
}
