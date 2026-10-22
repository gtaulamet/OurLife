namespace OurLife.Core.Defs;

/// <summary>Def types the loader knows: their JSON name, class and localization category.</summary>
internal static class DefTypes
{
    private static readonly (string Name, Type ClrType, string? TextCategory)[] Known =
    [
        ("Era", typeof(EraDef), "era"),
        ("Knowledge", typeof(KnowledgeDef), "knowledge"),
        ("TimeConfig", typeof(TimeConfigDef), null),
        ("Material", typeof(MaterialDef), "material"),
        ("Plant", typeof(PlantDef), "plant"),
        ("ItemCategory", typeof(ItemCategoryDef), "item_category"),
        ("Item", typeof(ItemDef), "item"),
        ("Buildable", typeof(BuildableDef), "buildable"),
        ("Need", typeof(NeedDef), "need"),
        ("Creature", typeof(CreatureDef), "creature"),
        ("NameList", typeof(NameListDef), null),
        ("WorldGen", typeof(WorldGenDef), null),
        ("WorkType", typeof(WorkTypeDef), "work_type"),
        ("Scenario", typeof(ScenarioDef), null),
        ("AiConfig", typeof(AiConfigDef), null),
        ("LifePace", typeof(LifePaceDef), "life_pace"),
        ("LifeStage", typeof(LifeStageDef), "life_stage"),
        ("LifeConfig", typeof(LifeConfigDef), null),
        ("Trait", typeof(TraitDef), "trait"),
        ("PersonalityConfig", typeof(PersonalityConfigDef), null),
        ("SkillConfig", typeof(SkillConfigDef), null),
        ("Interaction", typeof(InteractionDef), "interaction"),
        ("SocialConfig", typeof(SocialConfigDef), null),
        ("Thought", typeof(ThoughtDef), "thought"),
        ("Crisis", typeof(CrisisDef), "crisis"),
        ("Desire", typeof(DesireDef), "desire"),
        ("MoodConfig", typeof(MoodConfigDef), null),
        ("FreeWillConfig", typeof(FreeWillConfigDef), null),
        ("InitiativeTask", typeof(InitiativeTaskDef), null),
        ("Quality", typeof(QualityDef), "quality"),
        ("QualityConfig", typeof(QualityConfigDef), null),
    ];

    public static IEnumerable<string> Names => Known.Select(entry => entry.Name);

    public static bool TryGetClrType(string name, out Type clrType)
    {
        foreach (var entry in Known)
        {
            if (entry.Name == name)
            {
                clrType = entry.ClrType;
                return true;
            }
        }

        clrType = typeof(void);
        return false;
    }

    public static string NameOf(Type clrType) => Find(clrType).Name;

    public static string? TextCategoryOf(Type clrType) => Find(clrType).TextCategory;

    private static (string Name, Type ClrType, string? TextCategory) Find(Type clrType)
    {
        foreach (var entry in Known)
        {
            if (entry.ClrType == clrType)
            {
                return entry;
            }
        }

        throw new ArgumentException($"{clrType.Name} is not a registered def type.", nameof(clrType));
    }
}
