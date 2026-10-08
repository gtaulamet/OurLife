using System.Reflection;

namespace OurLife.Localization.Tests;

/// <summary>
/// The localization library is pure .NET so it can be tested without Godot,
/// and it is independent from the simulation core.
/// </summary>
public class ArchitectureTests
{
    private static readonly AssemblyName[] References = LocalizationAssembly.Assembly.GetReferencedAssemblies();

    [Fact]
    public void Localization_assembly_does_not_reference_Godot()
    {
        References.ShouldNotContain(
            a => a.Name != null && a.Name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Localization_assembly_does_not_reference_the_simulation_core()
    {
        References.ShouldNotContain(a => a.Name == "OurLife.Core");
    }
}
