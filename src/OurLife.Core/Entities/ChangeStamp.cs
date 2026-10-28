namespace OurLife.Core.Entities;

/// <summary>
/// A counter that goes up whenever a component the rooms depend on is edited in place (furniture def, material
/// or quality; the cells of a stockpile). The room index compares it with the last value it saw instead of
/// scanning every piece. It is never saved nor hashed and only compared for inequality within a run, so it
/// is shared by all simulations of the process: another simulation's edit costs a rebuild, never a wrong answer.
/// </summary>
internal static class ChangeStamp
{
    private static long _value;

    public static long Value => Interlocked.Read(ref _value);

    public static void Bump() => Interlocked.Increment(ref _value);
}
