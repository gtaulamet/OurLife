using OurLife.Core.Colony;
using OurLife.Core.Defs;

namespace OurLife.Content.Tests;

/// <summary>The real medieval tree of knowledge (<c>docs/gdd/conocimientos.md</c>, "El árbol medieval").</summary>
public class KnowledgeContentTests
{
    private static readonly string[] Tree =
    [
        "agriculture", "carpentry", "cereals", "charcoal_burning", "cooking", "food_preservation",
        "ironwork", "joinery", "metallurgy", "milling", "silviculture", "stonecutting",
    ];

    [Fact]
    public void The_tree_has_the_twelve_medieval_knowledge_and_ten_of_them_are_learned()
    {
        var all = GameContent.Defs.All<KnowledgeDef>();

        all.Select(def => def.Id).OrderBy(id => id, StringComparer.Ordinal).ShouldBe(Tree);
        all.ShouldAllBe(def => def.Era == "medieval");
        all.Where(def => def.Cost > 0).Count().ShouldBe(10);
        all.Where(def => def.Cost == 0).Select(def => def.Id).Order(StringComparer.Ordinal).ShouldBe(["carpentry", "cooking"]);
        all.Sum(def => def.Cost).ShouldBe(28_000);
    }

    [Fact]
    public void Every_knowledge_can_be_reached_from_the_ones_the_village_starts_with_and_none_requires_itself()
    {
        var known = new HashSet<string>(ColonyStart.TheOnlyScenario(GameContent.Defs).StartingKnowledge, StringComparer.Ordinal);
        var pending = GameContent.Defs.All<KnowledgeDef>().Where(def => !known.Contains(def.Id)).ToList();
        while (pending.Count > 0)
        {
            var next = pending.Where(def => def.Requires.All(known.Contains)).ToList();
            next.ShouldNotBeEmpty("a knowledge that nothing can unlock: " + string.Join(", ", pending.Select(def => def.Id)));
            foreach (var def in next)
            {
                known.Add(def.Id);
                pending.Remove(def);
            }
        }
    }

    [Fact]
    public void Knowledge_is_practiced_by_a_work_type_of_the_content_within_the_branches_of_the_panel()
    {
        foreach (var def in GameContent.Defs.All<KnowledgeDef>())
        {
            def.Branch.ShouldBeOneOf("crafts", "field", "fire_metal");
            if (def.Practice is { } practice)
            {
                GameContent.Defs.Get<WorkTypeDef>(practice.WorkType).ShouldNotBeNull();
            }
        }

        // What cannot be done before knowing it is not practiced.
        GameContent.Defs.Get<KnowledgeDef>("metallurgy").Practice.ShouldBeNull();
        GameContent.Defs.Get<KnowledgeDef>("ironwork").Practice.ShouldBeNull();
    }

    [Fact]
    public void The_scriptorium_needs_no_knowledge_and_every_starting_knowledge_exists()
    {
        var scriptorium = GameContent.Defs.Get<BuildableDef>("scriptorium");

        scriptorium.Requires.ShouldBeEmpty();
        scriptorium.Workstation!.WorkType.ShouldBe(ColonyRules.From(GameContent.Defs).Knowledge!.WorkType);
        scriptorium.Materials.ShouldBe(["planks"]);
        scriptorium.Cost.ShouldBe(6);
    }
}
