using System.Diagnostics;

namespace OurLife.Tests.Shared;

/// <summary>
/// Timing for performance budgets that a busy machine cannot fail. The budget stays the real one;
/// what changes is how it is measured: the work is run in several rounds and the cheapest round counts,
/// because a round that is slow only because other processes took the CPU says nothing about the code.
/// One round more runs first and does not count, so the JIT has compiled the code the way a running game has it.
/// The tests that measure also share the "Timing" collection, which xUnit runs apart from the rest of the assembly.
/// Shared by every test project through a linked file.
/// </summary>
internal static class Timing
{
    /// <summary>Runs <paramref name="round"/> <paramref name="rounds"/> times and returns the least wall time of one round, in milliseconds.</summary>
    public static double BestRoundMs(int rounds, Action<int> round)
    {
        var best = double.MaxValue;
        for (var i = 0; i <= rounds; i++)
        {
            var started = Stopwatch.GetTimestamp();
            round(i);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            best = i == 0 ? best : Math.Min(best, elapsed);
        }

        return best;
    }
}

/// <summary>Collection of the tests that measure time: they run one at a time, apart from the others of their assembly.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Timing";
}
