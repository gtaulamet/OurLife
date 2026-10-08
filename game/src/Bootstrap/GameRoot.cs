using System.Diagnostics;
using System.Globalization;
using Godot;
using OurLife.Core.Sim;

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

    private const ulong DefaultSmokeSeed = 1;
    private const int DefaultSmokeTicks = 1_000;

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

    /// <summary>
    /// Boots the simulation headless, runs the requested ticks and reports timing and the state
    /// fingerprint. The real-time measurement lives here, outside the deterministic core.
    /// </summary>
    private void RunSmoke(CommandLineOptions options)
    {
        try
        {
            var godotVersion = Engine.GetVersionInfo()["string"];
            GD.Print($"[smoke] Godot {godotVersion} | .NET {System.Environment.Version} | {OS.GetName()}");
            GD.Print($"[smoke] options: {options.Describe()}");

            var ticks = options.Ticks ?? DefaultSmokeTicks;
            var simulation = new Simulation(options.Seed ?? DefaultSmokeSeed, TimeSettings.PlanDefaults);
            var stopwatch = Stopwatch.StartNew();
            simulation.Run(ticks);
            stopwatch.Stop();

            var totalMs = stopwatch.Elapsed.TotalMilliseconds;
            var msPerTick = ticks == 0 ? 0 : totalMs / ticks;
            GD.Print(string.Create(
                CultureInfo.InvariantCulture,
                $"[smoke] simulated {ticks} ticks in {totalMs:F1} ms ({msPerTick:F4} ms/tick)"));
            GD.Print($"[smoke] date {simulation.CurrentDate} | state {simulation.ComputeStateHash():X16}");
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
