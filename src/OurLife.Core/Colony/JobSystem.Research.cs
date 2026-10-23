using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

// Researching at a scriptorium (see "El scriptorium" in docs/gdd/conocimientos.md).
public sealed partial class JobSystem
{
    /// <summary>
    /// A tick of research: it gives the knowledge at the head of the queue as many hundredths of a
    /// point as the speed of the worker, in percent, so a stretch interrupted keeps what it gave.
    /// The stretch ends after the ticks of the config, and the researcher decides again.
    /// </summary>
    private bool Research(Simulation simulation, Job job, int speed)
    {
        ResearchRules.AddResearch(simulation, speed);
        job.Progress++;
        return job.Progress >= _rules.Knowledge!.ResearchChunkTicks;
    }
}
