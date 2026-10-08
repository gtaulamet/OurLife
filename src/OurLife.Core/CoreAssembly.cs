using System.Reflection;

namespace OurLife.Core;

/// <summary>
/// Marker for the simulation assembly. Tests and tools use it to inspect the assembly.
/// The simulation itself starts in milestone H1.2.
/// </summary>
public static class CoreAssembly
{
    public static Assembly Assembly => typeof(CoreAssembly).Assembly;
}
