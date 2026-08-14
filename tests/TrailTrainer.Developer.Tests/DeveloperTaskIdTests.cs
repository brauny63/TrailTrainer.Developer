using TrailTrainer.Developer.Core;

namespace TrailTrainer.Developer.Tests;

public sealed class DeveloperTaskIdTests
{
    [Fact]
    public void ToString_ValidNumber_UsesCanonicalFormat()
    {
        Assert.Equal("DEV-0042", new DeveloperTaskId(42).ToString());
    }

    [Fact]
    public void Equality_SameNumber_HasValueEquality()
    {
        Assert.Equal(new DeveloperTaskId(42), new DeveloperTaskId(42));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public void Constructor_NumberOutsideRange_Throws(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeveloperTaskId(number));
    }
}
