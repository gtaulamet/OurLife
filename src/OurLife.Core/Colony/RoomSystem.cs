using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Notices when the map or the furniture changed what the rooms are and tells the presentation with a
/// <see cref="RoomsChangedEvent"/>, at the end of the tick of the change (see <c>docs/gdd/habitaciones.md</c>).
/// The rooms themselves are found when asked for; this only compares the key of the map with the last one,
/// and finds them again, once, when it differs. Draws no random numbers and changes no state.
/// </summary>
public sealed class RoomSystem : ISimSystem
{
    public void Tick(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        if (!Rooms.IsEnabled(simulation))
        {
            return;
        }

        var index = Rooms.IndexOf(simulation);
        var before = index.Signature;
        index.Refresh(simulation);
        if (index.Signature != before)
        {
            simulation.Events.Emit(new RoomsChangedEvent(simulation.CurrentTick));
        }
    }
}
