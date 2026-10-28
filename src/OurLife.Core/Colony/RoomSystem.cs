using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// Notices when the map or the furniture changed what the rooms are and tells the presentation with a
/// <see cref="RoomsChangedEvent"/>, at the end of the tick of the change (see <c>docs/gdd/habitaciones.md</c>).
/// The rooms themselves are found when asked for, by whoever asks first. This system compares the rooms with the
/// last ones it told about (kept by the index, <see cref="RoomIndex.NotifiedSignature"/>), not with the ones the
/// index had before its own refresh, so a change found earlier in the tick by a query still says so, exactly once.
/// Draws no random numbers and changes no simulated state.
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
        index.Refresh(simulation);
        if (index.Signature != index.NotifiedSignature)
        {
            index.NotifiedSignature = index.Signature;
            simulation.Events.Emit(new RoomsChangedEvent(simulation.CurrentTick));
        }
    }
}
