using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using OurLife.Core.Sim;

namespace OurLife.Core.Defs;

/// <summary>
/// Every def of the game, validated and immutable. Defs of each type are sorted by id, which
/// also gives each one a compact index (used, for example, to store materials in the map).
/// Indexes hold only while the content does not change: adding or removing a def shifts them,
/// so saves store ids, never indexes.
/// </summary>
public sealed partial class DefDatabase
{
    private readonly Dictionary<Type, Array> _defsByType;
    private readonly Dictionary<Type, Dictionary<string, int>> _indexByType;

    private DefDatabase(
        Dictionary<Type, Array> defsByType,
        Dictionary<Type, Dictionary<string, int>> indexByType,
        TimeSettings time,
        EraDef firstEra)
    {
        _defsByType = defsByType;
        _indexByType = indexByType;
        Time = time;
        FirstEra = firstEra;
        Count = defsByType.Values.Sum(defs => defs.Length);
    }

    public TimeSettings Time { get; }

    /// <summary>The era with the lowest order: every game starts there.</summary>
    public EraDef FirstEra { get; }

    public int Count { get; }

    /// <summary>Defs of a type, sorted by id.</summary>
    public IReadOnlyList<T> All<T>()
        where T : Def => _defsByType.TryGetValue(typeof(T), out var defs) ? (T[])defs : [];

    /// <summary>Every def of every type: types by name, then ids in order.</summary>
    public IEnumerable<Def> AllDefs() =>
        _defsByType
            .OrderBy(entry => DefTypes.NameOf(entry.Key), StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Cast<Def>());

    public T Get<T>(string id)
        where T : Def =>
        TryGet<T>(id, out var def)
            ? def
            : throw new KeyNotFoundException($"There is no {DefTypes.NameOf(typeof(T))} def '{id}'.");

    public bool TryGet<T>(string id, [NotNullWhen(true)] out T? def)
        where T : Def
    {
        if (_indexByType.TryGetValue(typeof(T), out var indexes) && indexes.TryGetValue(id, out var index))
        {
            def = ((T[])_defsByType[typeof(T)])[index];
            return true;
        }

        def = null;
        return false;
    }

    /// <summary>Position of a def among the defs of its type, sorted by id. Changes when the content changes.</summary>
    public int IndexOf<T>(string id)
        where T : Def =>
        _indexByType.TryGetValue(typeof(T), out var indexes) && indexes.TryGetValue(id, out var index)
            ? index
            : throw new KeyNotFoundException($"There is no {DefTypes.NameOf(typeof(T))} def '{id}'.");

    /// <exception cref="DefValidationException">Lists every problem found.</exception>
    internal static DefDatabase Build(IEnumerable<Def> defs)
    {
        var errors = new List<string>();
        var byType = new Dictionary<Type, SortedDictionary<string, Def>>();

        foreach (var def in defs)
        {
            var label = Describe(def);
            if (!IdPattern().IsMatch(def.Id))
            {
                errors.Add($"{label}: invalid id. Use lowercase letters, digits and underscores, starting with a letter.");
                continue;
            }

            if (!byType.TryGetValue(def.GetType(), out var ofType))
            {
                ofType = new SortedDictionary<string, Def>(StringComparer.Ordinal);
                byType.Add(def.GetType(), ofType);
            }

            if (ofType.TryGetValue(def.Id, out var existing))
            {
                errors.Add($"{label}: duplicate id, already defined in {existing.SourceFile}.");
                continue;
            }

            ofType.Add(def.Id, def);
            errors.AddRange(def.Validate().Select(problem => $"{label}: {problem}"));
        }

        foreach (var def in byType.Values.SelectMany(ofType => ofType.Values))
        {
            var references = def.References()
                .Append(new DefReference(typeof(EraDef), def.Era, "era"))
                .Concat(def.Requires.Select(knowledge => new DefReference(typeof(KnowledgeDef), knowledge, "requires")));
            foreach (var reference in references)
            {
                if (reference.Id is not null
                    && !(byType.TryGetValue(reference.Target, out var targets) && targets.ContainsKey(reference.Id)))
                {
                    errors.Add($"{Describe(def)}: {reference.Field} points to '{reference.Id}', but there is no {DefTypes.NameOf(reference.Target)} def with that id.");
                }
            }
        }

        var eras = byType.TryGetValue(typeof(EraDef), out var eraDefs) ? eraDefs.Values.Cast<EraDef>().ToArray() : [];
        if (eras.Length == 0)
        {
            errors.Add("The content needs at least one Era def.");
        }

        foreach (var clash in eras.GroupBy(era => era.Order).Where(group => group.Count() > 1))
        {
            errors.Add($"Eras {string.Join(", ", clash.Select(era => era.Id))} share order {clash.Key}.");
        }

        // Free will is optional, but one config at most: FreeWillRules.ConfigOf reads the only one.
        if (byType.TryGetValue(typeof(FreeWillConfigDef), out var freeWillConfigs) && freeWillConfigs.Count > 1)
        {
            errors.Add($"The content can have at most one FreeWillConfig def, found {freeWillConfigs.Count}.");
        }

        // A task looks at a stock target: the free will config must have one for its category.
        if (byType.TryGetValue(typeof(InitiativeTaskDef), out var tasks))
        {
            var freeWill = freeWillConfigs?.Values.Cast<FreeWillConfigDef>().FirstOrDefault();
            foreach (var task in tasks.Values.Cast<InitiativeTaskDef>().Where(task => freeWill?.DefaultTargets.ContainsKey(task.Category) != true))
            {
                errors.Add($"{Describe(task)}: the FreeWillConfig has no target for '{task.Category}', so the task would never be done.");
            }
        }

        var timeConfigs = byType.TryGetValue(typeof(TimeConfigDef), out var configs) ? configs.Values.Cast<TimeConfigDef>().ToArray() : [];
        if (timeConfigs.Length != 1)
        {
            errors.Add($"The content needs exactly one TimeConfig def, found {timeConfigs.Length}.");
        }
        else if (byType.TryGetValue(typeof(PlantDef), out var plantDefs))
        {
            // Seasons are not defs: their ids come from the calendar.
            var seasons = timeConfigs[0].Seasons;
            foreach (var plant in plantDefs.Values.Cast<PlantDef>())
            {
                foreach (var season in plant.DormantSeasons.Where(season => !seasons.Contains(season)))
                {
                    errors.Add($"{Describe(plant)}: dormantSeasons names '{season}', but the TimeConfig has no such season.");
                }

                if (plant.DormantSeasons.Count > 0 && seasons.All(plant.DormantSeasons.Contains))
                {
                    errors.Add($"{Describe(plant)}: dormantSeasons lists every season, so once gathered it would never bear fruit again.");
                }
            }
        }

        var materials = byType.TryGetValue(typeof(MaterialDef), out var materialDefs)
            ? materialDefs.Values.Cast<MaterialDef>().ToDictionary(material => material.Id, StringComparer.Ordinal)
            : [];
        foreach (var clash in materials.Values.Where(material => material.IsBuilt).GroupBy(material => material.BuiltFrom).Where(group => group.Count() > 1))
        {
            errors.Add($"Materials {string.Join(", ", clash.Select(material => material.Id))} are all built from '{clash.Key}'; an item builds one material.");
        }

        if (byType.TryGetValue(typeof(BuildableDef), out var buildableDefs))
        {
            foreach (var buildable in buildableDefs.Values.Cast<BuildableDef>())
            {
                foreach (var id in buildable.Materials.Where(id => materials.TryGetValue(id, out var material) && !material.IsBuilt))
                {
                    errors.Add($"{Describe(buildable)}: materials names '{id}', which has no builtFrom.");
                }
            }
        }

        // A workshop works with a work type of workshops, and a recipe is made at a workshop.
        var workTypes = byType.TryGetValue(typeof(WorkTypeDef), out var workTypeDefs)
            ? workTypeDefs.Values.Cast<WorkTypeDef>().ToDictionary(workType => workType.Id, StringComparer.Ordinal)
            : [];
        var buildables = byType.TryGetValue(typeof(BuildableDef), out var allBuildables)
            ? allBuildables.Values.Cast<BuildableDef>().ToDictionary(buildable => buildable.Id, StringComparer.Ordinal)
            : [];
        foreach (var buildable in buildables.Values)
        {
            if (buildable.Workstation is { } station && workTypes.TryGetValue(station.WorkType, out var stationType) && !stationType.Workshop)
            {
                errors.Add($"{Describe(buildable)}: workstation.workType '{station.WorkType}' is not a work type of workshops (workshop: true).");
            }
        }

        if (byType.TryGetValue(typeof(RecipeDef), out var recipeDefs))
        {
            foreach (var recipe in recipeDefs.Values.Cast<RecipeDef>())
            {
                if (buildables.TryGetValue(recipe.Workshop, out var workshop) && workshop.Workstation is null)
                {
                    errors.Add($"{Describe(recipe)}: workshop '{recipe.Workshop}' is not a workshop (it has no workstation).");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new DefValidationException(errors);
        }

        var defsByType = new Dictionary<Type, Array>();
        var indexByType = new Dictionary<Type, Dictionary<string, int>>();
        foreach (var (type, ofType) in byType)
        {
            var array = Array.CreateInstance(type, ofType.Count);
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            var position = 0;
            foreach (var def in ofType.Values)
            {
                array.SetValue(def, position);
                indexes.Add(def.Id, position);
                position++;
            }

            defsByType.Add(type, array);
            indexByType.Add(type, indexes);
        }

        var firstEra = eras.MinBy(era => era.Order)!;
        return new DefDatabase(defsByType, indexByType, timeConfigs[0].ToTimeSettings(), firstEra);
    }

    private static string Describe(Def def) =>
        $"{(def.SourceFile.Length > 0 ? def.SourceFile : "?")}: {DefTypes.NameOf(def.GetType())} '{def.Id}'";

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex IdPattern();
}
