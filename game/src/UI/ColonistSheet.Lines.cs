using System.Globalization;
using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;

namespace OurLife.Game.UI;

/// <summary>The lines of each part of the sheet, in the current language.</summary>
public partial class ColonistSheet
{
    private PersonalityRules? _personalities;
    private SkillRules? _skills;

    private PersonalityRules Personalities => _personalities ??= PersonalityRules.From(_simulation.Content);

    private SkillRules SkillRules => _skills ??= SkillRules.From(_simulation.Content);

    private static string Signed(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);

    private string GenderOf(EntityId colonist) => ColonyText.GenderId(_simulation.World.Get<Colonist>(colonist).Gender);

    private string TitleOf(EntityId colonist) => ColonyText.FullName(_simulation.World.Get<Colonist>(colonist));

    /// <summary>A name of a def in the current language, starting with a capital; <paramref name="gender"/> for words that agree.</summary>
    private string NameOf(Def def, string? gender = null) =>
        _services.Text.Capitalized(_services.Text.Format(def.NameKey!, ("count", 1), ("gender", gender)));

    private IEnumerable<string> SummaryOf(EntityId colonist)
    {
        var text = _services.Text;
        var gender = GenderOf(colonist);
        var stage = LifeRules.Stage(_simulation, colonist);
        if (stage is not null)
        {
            yield return text.Format("ui.inspector.life", ("stage", NameOf(stage, gender)), ("years", LifeRules.AgeYears(_simulation, colonist)));
        }

        if (LifeRules.PregnancyPercent(_simulation, colonist) is { } pregnant)
        {
            yield return text.Format("ui.inspector.pregnant", ("percent", pregnant));
        }

        yield return text.Format("ui.sheet.mood", ("mood", MoodRules.Value(_simulation, colonist)));
        if (MoodRules.CrisisOf(_simulation, colonist) is { } crisis)
        {
            yield return text.Format("ui.sheet.crisis", ("crisis", NameOf(crisis)));
        }

        // Orientation shows from the stages that work, as the teenager on (see the spec).
        if (_simulation.World.TryGet<Personality>(colonist, out var personality) && stage is not null && stage.Work != StageWork.None)
        {
            var key = personality.Orientation switch
            {
                Orientation.Same => "ui.sheet.orientation.same",
                Orientation.Both => "ui.sheet.orientation.both",
                _ => "ui.sheet.orientation.opposite",
            };
            yield return text.Format(key, ("gender", gender));
        }
    }

    private List<string> Traits(EntityId colonist)
    {
        var lines = new List<string>();
        if (!_simulation.World.TryGet<Personality>(colonist, out var personality))
        {
            return lines;
        }

        var text = _services.Text;
        var gender = GenderOf(colonist);
        foreach (var value in personality.Traits)
        {
            var word = Personalities.WordOf(value.Value);
            if (word == TraitWord.Average || !_simulation.Content.TryGet<TraitDef>(value.Trait, out var trait))
            {
                continue;
            }

            var side = word is TraitWord.VeryLow or TraitWord.Low ? "low" : "high";
            var degree = word is TraitWord.VeryLow or TraitWord.VeryHigh ? "ui.sheet.trait_very" : "ui.sheet.trait_some";
            var name = text.Format($"def.{trait.TextCategory}.{trait.Id}.{side}", ("gender", gender));
            lines.Add(text.Capitalized(text.Format(degree, ("word", name))));
        }

        return lines;
    }

    private List<string> Skills(EntityId colonist)
    {
        var lines = new List<string>();
        if (!_simulation.World.TryGet<Skills>(colonist, out var skills))
        {
            return lines;
        }

        var config = SkillRules.Config;
        foreach (var skill in skills.Values)
        {
            if (!_simulation.Content.TryGet<WorkTypeDef>(skill.WorkType, out var type))
            {
                continue;
            }

            lines.Add(skill.Level >= config.MaxLevel
                ? _services.Text.Format("ui.sheet.skill_max", ("skill", NameOf(type)), ("level", skill.Level))
                : _services.Text.Format(
                    "ui.sheet.skill",
                    ("skill", NameOf(type)),
                    ("level", skill.Level),
                    ("percent", skill.Experience / config.ExperienceFor(skill.Level))));
        }

        return lines;
    }

    /// <summary>The tools the colonist has equipped, with their quality and the speed they add to their work.</summary>
    private List<string> ToolLines(EntityId colonist)
    {
        var lines = new List<string>();
        var text = _services.Text;
        var content = _simulation.Content;
        var defaultQuality = QualityRules.From(content).Default.Id;
        foreach (var stack in Tools.Of(_simulation, colonist))
        {
            var item = content.Get<ItemDef>(stack.Def);
            if (item.Tool is not { } tool)
            {
                continue;
            }

            var name = NameOf(item);
            if (stack.Quality is { } quality && quality != defaultQuality)
            {
                name = text.Format("ui.quality.label", ("item", name), ("quality", NameOf(content.Get<QualityDef>(quality)).ToLowerInvariant()));
            }

            lines.Add(text.Format(
                "ui.inspector.tool",
                ("item", name),
                ("percent", Tools.SpeedPercent(_simulation, item, stack.Quality)),
                ("work_type", NameOf(content.Get<WorkTypeDef>(tool.WorkType)).ToLowerInvariant())));
        }

        return lines;
    }

    /// <summary>Each thought once, with how many times it adds up and its whole effect.</summary>
    private List<string> Thoughts(EntityId colonist)
    {
        var gender = GenderOf(colonist);
        return [.. MoodRules.Thoughts(_simulation, colonist)
            .GroupBy(thought => thought.Id)
            .Select(group =>
            {
                var thought = group.First();
                var mood = Signed(group.Sum(each => each.Mood));
                return group.Count() == 1
                    ? _services.Text.Format("ui.sheet.thought", ("thought", NameOf(thought, gender)), ("mood", mood))
                    : _services.Text.Format("ui.sheet.thought_many", ("thought", NameOf(thought, gender)), ("count", group.Count()), ("mood", mood));
            })];
    }
}
