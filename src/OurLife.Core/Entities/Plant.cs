namespace OurLife.Core.Entities;

/// <summary>A tree, bush or crop. Its numbers come from the <see cref="Defs.PlantDef"/> with id <see cref="Def"/>.</summary>
public sealed class Plant
{
    public string Def { get; set; } = string.Empty;
}
