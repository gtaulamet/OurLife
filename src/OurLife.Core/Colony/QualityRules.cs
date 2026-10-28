using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;
using OurLife.Core.Sim;

namespace OurLife.Core.Colony;

/// <summary>
/// The quality of what colonists make (see "Calidad" in <c>docs/gdd/oficios.md</c>), checked once:
/// the ladder of <see cref="QualityDef"/>s and the roll of <see cref="QualityConfigDef"/>. The
/// quality of a pile or a piece of furniture is the id of a <see cref="QualityDef"/>, or null for
/// the default one (<see cref="QualityConfigDef.DefaultQuality"/>), which is never written.
/// </summary>
public sealed class QualityRules
{
    /// <summary>The random stream of the quality rolls.</summary>
    public const string Stream = "craft.quality";

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DefDatabase, QualityRules> Cache = new();

    private readonly Dictionary<string, int> _rankById = new(StringComparer.Ordinal);

    private QualityRules(QualityConfigDef config, IReadOnlyList<QualityDef> ladder)
    {
        Config = config;
        Ladder = ladder;
        for (var i = 0; i < ladder.Count; i++)
        {
            _rankById[ladder[i].Id] = i;
        }

        Default = ladder[_rankById[config.DefaultQuality]];
    }

    public QualityConfigDef Config { get; }

    /// <summary>The qualities, lowest first.</summary>
    public IReadOnlyList<QualityDef> Ladder { get; }

    /// <summary>The quality of what has none of its own.</summary>
    public QualityDef Default { get; }

    /// <exception cref="InvalidOperationException">
    /// The content lacks the QualityConfig or has several, has no qualities, repeats an order, its
    /// bounds do not go up with the order, the highest quality can be missed by a roll, or the
    /// default quality is not one of them.
    /// </exception>
    public static QualityRules From(DefDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // The rules only depend on the (immutable) content, so they are built once per database; a
        // content that fails the checks throws every time and is never cached. Not part of the state.
        if (Cache.TryGetValue(content, out var cached))
        {
            return cached;
        }

        var rules = Build(content);
        Cache.AddOrUpdate(content, rules);
        return rules;
    }

    /// <summary>The rules, or null when the content has no qualities (then nothing has one).</summary>
    public static QualityRules? FromOrNull(DefDatabase content) => content.All<QualityConfigDef>().Count == 1 ? From(content) : null;

    /// <summary>The value of a quality in percent; 100 when the content has no qualities.</summary>
    internal static int ValuePercentOf(DefDatabase content, string? quality) => FromOrNull(content)?.Of(quality).ValuePercent ?? 100;

    private static QualityRules Build(DefDatabase content)
    {
        var configs = content.All<QualityConfigDef>();
        if (configs.Count != 1)
        {
            throw new InvalidOperationException($"The colony needs exactly one QualityConfig def, found {configs.Count}.");
        }

        var ladder = content.All<QualityDef>().OrderBy(quality => quality.Order).ThenBy(quality => quality.Id, StringComparer.Ordinal).ToArray();
        if (ladder.Length == 0)
        {
            throw new InvalidOperationException("The colony needs at least one Quality def.");
        }

        for (var i = 1; i < ladder.Length; i++)
        {
            if (ladder[i].Order == ladder[i - 1].Order)
            {
                throw new InvalidOperationException($"The qualities '{ladder[i - 1].Id}' and '{ladder[i].Id}' have the same order.");
            }

            if (ladder[i].Below <= ladder[i - 1].Below)
            {
                throw new InvalidOperationException($"The bound of the quality '{ladder[i].Id}' must be higher than the one of '{ladder[i - 1].Id}'.");
            }
        }

        var skills = SkillRules.From(content);
        var highestScore = configs[0].Die - 1 + (configs[0].PerLevel * skills.Config.MaxLevel);
        if (ladder[^1].Below <= highestScore)
        {
            throw new InvalidOperationException(
                $"The highest quality, '{ladder[^1].Id}', must be below more than {highestScore}, the best score a roll can give.");
        }

        return new QualityRules(configs[0], ladder);
    }

    /// <summary>
    /// The score of a roll: a die plus the points of each level of the skill. Draws one number from
    /// <paramref name="random"/>.
    /// </summary>
    public int Score(Pcg32 random, int skillLevel)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.NextInt(Config.Die) + (Config.PerLevel * Math.Max(0, skillLevel));
    }

    /// <summary>The first quality whose bound is above the score.</summary>
    public QualityDef ForScore(int score)
    {
        foreach (var quality in Ladder)
        {
            if (score < quality.Below)
            {
                return quality;
            }
        }

        return Ladder[^1];
    }

    /// <summary>The quality of a roll of someone with a skill at this level.</summary>
    public QualityDef Roll(Pcg32 random, int skillLevel) => ForScore(Score(random, skillLevel));

    /// <summary>
    /// The quality a colonist makes with the skill of a work type, rolled with the simulation's
    /// stream <see cref="Stream"/>. A colonist without skills rolls as an average one, and one
    /// without that skill, as level 0, as for the speed of work (<see cref="SkillRules.SpeedPercent"/>).
    /// </summary>
    public QualityDef Roll(Simulation simulation, EntityId colonist, string workType)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        var skills = SkillRules.From(simulation.Content);
        var level = simulation.World.TryGet<Skills>(colonist, out var own)
            ? Math.Min(own.Find(workType)?.Level ?? 0, skills.Config.MaxLevel)
            : skills.Config.AverageLevel;
        return Roll(simulation.Random.Get(Stream), level);
    }

    /// <summary>The value to keep in a pile or a piece of furniture for a quality: null for the default one.</summary>
    public string? Store(string? quality) => Normalize(quality, Default.Id);

    /// <summary>The id of a stored quality: the default one when null.</summary>
    public string IdOf(string? stored) => stored ?? Default.Id;

    /// <summary>The quality def of a stored quality; the default one when null.</summary>
    public QualityDef Of(string? stored) => stored is null ? Default : Ladder[RankOf(stored)];

    /// <summary>Place of a stored quality in the ladder: 0 for the lowest.</summary>
    /// <exception cref="ArgumentException">The content has no such quality.</exception>
    public int RankOf(string? stored) =>
        stored is null ? _rankById[Default.Id] : _rankById.TryGetValue(stored, out var rank) ? rank : throw new ArgumentException($"There is no Quality def '{stored}'.", nameof(stored));

    /// <summary>Whether a quality is the same as <paramref name="minimum"/> or better; any quality passes a null minimum.</summary>
    public bool AtLeast(string? stored, string? minimum) => minimum is null || RankOf(stored) >= RankOf(minimum);

    /// <summary>
    /// The stored form of a quality for code that has no <see cref="QualityRules"/> at hand: null
    /// stays null, and so does the default quality of the content, if the content has one.
    /// </summary>
    internal static string? Canonical(DefDatabase content, string? quality)
    {
        if (quality is null)
        {
            return null;
        }

        var configs = content.All<QualityConfigDef>();
        return configs.Count == 1 ? Normalize(quality, configs[0].DefaultQuality) : quality;
    }

    private static string? Normalize(string? quality, string defaultId) => quality is null || quality == defaultId ? null : quality;
}
