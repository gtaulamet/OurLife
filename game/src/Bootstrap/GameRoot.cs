using Godot;

namespace OurLife.Game.Bootstrap;

/// <summary>
/// Root node of the game. It reads the command line and, for now, only implements the
/// smoke mode used by agents and CI. Services and screens are mounted in later milestones.
/// </summary>
public partial class GameRoot : Node
{
    private const int ExitOk = 0;
    private const int ExitFailure = 1;
    private const int ExitBadArguments = 2;

    public override void _Ready()
    {
        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(OS.GetCmdlineUserArgs());
        }
        catch (ArgumentException ex)
        {
            GD.PushError($"Invalid command line: {ex.Message}");
            GetTree().Quit(ExitBadArguments);
            return;
        }

        foreach (var argument in options.UnknownArguments)
        {
            GD.PushWarning($"Unknown command line argument ignored: {argument}");
        }

        if (options.Smoke)
        {
            RunSmoke(options);
        }
    }

    private void RunSmoke(CommandLineOptions options)
    {
        try
        {
            var godotVersion = Engine.GetVersionInfo()["string"];
            GD.Print($"[smoke] Godot {godotVersion} | .NET {System.Environment.Version} | {OS.GetName()}");
            GD.Print($"[smoke] options: {options.Describe()}");
            GD.Print("[smoke] OK");
            GetTree().Quit(ExitOk);
        }
        catch (Exception ex)
        {
            GD.PushError($"[smoke] FAILED: {ex}");
            GetTree().Quit(ExitFailure);
        }
    }
}
