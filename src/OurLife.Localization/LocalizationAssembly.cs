using System.Reflection;

namespace OurLife.Localization;

/// <summary>
/// Marker for the localization assembly. Tests and tools use it to inspect the assembly.
/// Catalogs and ICU formatting arrive in milestone H1.3.
/// </summary>
public static class LocalizationAssembly
{
    public static Assembly Assembly => typeof(LocalizationAssembly).Assembly;
}
