namespace ComputerBuddy.App.Services.Minifigure;

public static class MinifigureCatalog
{
    public static IReadOnlyList<Option> Heads { get; } =
    [
        new Option("smile", "Smile"),
        new Option("visor", "Visor")
    ];

    public static IReadOnlyList<Option> Bodies { get; } =
    [
        new Option("classic", "Classic"),
        new Option("hoodie", "Hoodie")
    ];

    public static IReadOnlyList<Option> Hats { get; } =
    [
        new Option("none", "None"),
        new Option("cap", "Cap"),
        new Option("helmet", "Helmet")
    ];

    public static IReadOnlyList<Option> Accessories { get; } =
    [
        new Option("none", "None"),
        new Option("backpack", "Backpack")
    ];

    public sealed record Option(string Id, string Name);
}

