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

        yield return text.Format("ui.sheet.mood", ("mood", MoodRules.Value(_simulation, colonist)));
        if (MoodRules.CrisisOf(_simulation, colonist) is { } crisis)
        {
            yield return text.Format("ui.sheet.crisis", ("crisis", NameOf(crisis)));
        }

        // Orientation shows from the stages that work, as the teenager on (see the spec).
        if (_simulation.World.TryGet<Personality>(colonist, out var personality) && stage?.Work != StageWork.None)
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

    private List<string> Thoughts(EntityId colonist)
    {
        var gender = GenderOf(colonist);
        return [.. MoodRules.Thoughts(_simulation, colonist)
            .Select(thought => _services.Text.Format("ui.sheet.thought", ("thought", NameOf(thought, gender)), ("mood", Signed(thought.Mood))))];
    }
}
