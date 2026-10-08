using System.Reflection;

namespace OurLife.Core.Tests;

/// <summary>
/// Architecture rule 1: the simulation core is pure .NET. It must not depend on Godot
/// nor on the localization library (the simulation only emits keys and arguments).
/// </summary>
public class ArchitectureTests
{
    private static readonly AssemblyName[] CoreReferences = CoreAssembly.Assembly.GetReferencedAssemblies();

    [Fact]
    public void Core_assembly_does_not_reference_Godot()
    {
        CoreReferences.ShouldNotContain(
            a => a.Name != null && a.Name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Core_assembly_does_not_reference_the_localization_library()
    {
        CoreReferences.ShouldNotContain(a => a.Name == "OurLife.Localization");
    }

    [Fact]
    public void Core_project_file_declares_no_Godot_or_localization_dependency()
    {
        // Assembly metadata only lists references that are actually used, so the project
        // file is checked too: an unused reference today becomes a used one tomorrow.
        var projectFile = File.ReadAllText(Path.Combine(TestPaths.CoreSourceDir, "OurLife.Core.csproj"));

        projectFile.ShouldNotContain("Godot", Case.Insensitive);
        projectFile.ShouldNotContain("OurLife.Localization", Case.Insensitive);
    }
}
