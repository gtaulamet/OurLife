namespace OurLife.Core.Sim;

/// <summary>
/// An order that changes the simulation from outside, usually from the player. Commands are
/// applied at the start of the next tick, in the order they were enqueued. Keep them as plain
/// serializable data: they are part of the state fingerprint and, later, of replays.
/// </summary>
public interface ISimCommand
{
    void Apply(Simulation simulation);
}
