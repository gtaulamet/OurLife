using OurLife.Core.Colony;
using OurLife.Core.Defs;
using OurLife.Core.Ecs;
using OurLife.Core.Entities;

namespace OurLife.Game.UI;

/// <summary>The lines of the sheet about wishes and other people.</summary>
public partial class ColonistSheet
{
    private List<string> Desires(EntityId colonist)
    {
        var lines = new List<string>();
        if (!_simulation.World.TryGet<Desires>(colonist, out var desires))
        {
            return lines;
        }

        var text = _services.Text;
        var day = _simulation.Time.TicksPerDay;
        foreach (var active in desires.Active)
        {
            if (!_simulation.Content.TryGet<DesireDef>(active.Desire, out var desire))
            {
                continue;
            }

            var days = (int)Math.Max(1, (active.UntilTick - _simulation.CurrentTick + day - 1) / day);
            var about = !active.Target.IsNone && IsColonist(active.Target) ? TitleOf(active.Target)
                : active.WorkType is { } type && _simulation.Content.TryGet<WorkTypeDef>(type, out var work) ? NameOf(work)
                : null;
            lines.Add(about is null
                ? text.Format("ui.sheet.desire", ("desire", NameOf(desire)), ("days", days))
                : text.Format("ui.sheet.desire_about", ("desire", NameOf(desire)), ("about", about), ("days", days)));
        }

        return lines;
    }

    private List<string> Relations(EntityId colonist)
    {
        var lines = new List<string>();
        if (!_simulation.World.TryGet<Relations>(colonist, out var relations))
        {
            return lines;
        }

        var text = _services.Text;
        foreach (var relation in relations.Others.Where(relation => IsColonist(relation.Other)).OrderByDescending(relation => relation.Opinion))
        {
            var kind = Relationships.Kind(_simulation, colonist, relation.Other) switch
            {
                RelationKind.Partner => "ui.sheet.kind.partner",
                RelationKind.Family => "ui.sheet.kind.family",
                RelationKind.Friend => "ui.sheet.kind.friend",
                RelationKind.Rival => "ui.sheet.kind.rival",
                _ => null,
            };
            var name = TitleOf(relation.Other);
            var opinion = Signed(relation.Opinion);
            lines.Add(kind is null
                ? text.Format("ui.sheet.relation", ("name", name), ("opinion", opinion))
                : text.Format("ui.sheet.relation_kind", ("name", name), ("kind", text.Format(kind)), ("opinion", opinion)));
        }

        return lines;
    }

    private List<string> Family(EntityId colonist)
    {
        var lines = new List<string>();
        var text = _services.Text;
        if (_simulation.World.TryGet<Family>(colonist, out var family))
        {
            var relatives = new List<(string Key, Relative? Relative)>
            {
                ("ui.sheet.family.father", family.Father),
                ("ui.sheet.family.mother", family.Mother),
                ("ui.sheet.family.partner", family.Partner),
            };
            relatives.AddRange(family.LatePartners.Select(late => ("ui.sheet.family.late_partner", (Relative?)late)));
            relatives.AddRange(family.Children.Select(child => ("ui.sheet.family.child", (Relative?)child)));
            foreach (var (key, relative) in relatives)
            {
                if (relative is not null)
                {
                    lines.Add(text.Format(key, ("name", NameOf(relative)), ("gender", ColonyText.GenderId(relative.Gender))));
                }
            }
        }

        foreach (var sibling in Relationships.SiblingsOf(_simulation, colonist))
        {
            lines.Add(text.Format("ui.sheet.family.sibling", ("name", TitleOf(sibling)), ("gender", GenderOf(sibling))));
        }

        return lines;
    }

    /// <summary>A relative's name, marked as dead if it is gone.</summary>
    private string NameOf(Relative relative)
    {
        var name = relative.Surname.Length == 0 ? relative.Name : $"{relative.Name} {relative.Surname}";
        return relative.Dead || !IsColonist(relative.Entity)
            ? _services.Text.Format("ui.sheet.dead", ("name", name), ("gender", ColonyText.GenderId(relative.Gender)))
            : name;
    }

    private List<string> Recent(EntityId colonist)
    {
        if (!_simulation.World.TryGet<RecentInteractions>(colonist, out var recent))
        {
            return [];
        }

        return [.. recent.Entries.AsEnumerable().Reverse()
            .Where(entry => IsColonist(entry.Other) && _simulation.Content.TryGet<InteractionDef>(entry.Interaction, out _))
            .Select(entry => Recalled(entry))];
    }

    /// <summary>
    /// An interaction it remembers: in the words of each interaction when it started it, like
    /// "Le enseñó a Alfonso", or as the other one coming to it, like "Alfonso vino a enseñar".
    /// </summary>
    private string Recalled(InteractionRecord entry)
    {
        var text = _services.Text;
        var interaction = _simulation.Content.Get<InteractionDef>(entry.Interaction);
        var name = TitleOf(entry.Other);
        return entry.Started
            ? text.Capitalized(text.Format($"def.{interaction.TextCategory}.{interaction.Id}.started", ("name", name)))
            : text.Format("ui.sheet.recent_received", ("verb", text.Format(interaction.NameKey!)), ("name", name));
    }
}
