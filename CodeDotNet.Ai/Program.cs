namespace CodeDotNet.Ai;

public sealed partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (Cli.IsCommand(args))
        {
            return await Cli.RunAsync(args, Console.Out);
        }

        var app = AiApp.Build(args);
        await app.RunAsync();
        return 0;
    }
}
