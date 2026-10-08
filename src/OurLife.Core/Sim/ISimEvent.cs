namespace OurLife.Core.Sim;

/// <summary>
/// Something that happened in the simulation and that the presentation may want to show:
/// a tile changed, an entity appeared, a message for the player. Events are not state;
/// they are not saved nor included in the state fingerprint.
/// </summary>
public interface ISimEvent
{
}
