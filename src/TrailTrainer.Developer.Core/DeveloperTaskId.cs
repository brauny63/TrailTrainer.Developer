namespace TrailTrainer.Developer.Core;

public readonly record struct DeveloperTaskId
{
    public DeveloperTaskId(int number)
    {
        if (number is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(number),
                number,
                "Developer Task numbers must be between 1 and 9999.");
        }

        Number = number;
    }

    public int Number { get; }

    public override string ToString() => $"DEV-{Number:D4}";
}
