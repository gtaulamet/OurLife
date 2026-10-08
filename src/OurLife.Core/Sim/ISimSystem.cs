namespace OurLife.Core.Sim;

/// <summary>
/// A piece of simulation logic. Systems run on their scheduled ticks in registration order;
/// see <see cref="Simulation.AddSystem"/>. They read and change the world, draw from their own
/// random stream and emit events, but never touch the presentation.
/// </summary>
public interface ISimSystem
{
    void Tick(Simulation simulation);
}
