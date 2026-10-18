namespace OurLife.Core.Defs;

/// <summary>
/// The numbers of interactions and relations (see "Interacciones" and "Relaciones y familia" in
/// <c>docs/gdd/vida-y-generaciones.md</c>). The content must define exactly one.
/// </summary>
public sealed class SocialConfigDef : Def
{
    /// <summary>Each colonist looks for someone to interact with once every this many ticks.</summary>
    public required int CheckTicks { get; init; }

    /// <summary>The farthest the other one can be, in cells on the same level.</summary>
    public required int Radius { get; init; }

    /// <summary>Hours before the same two colonists interact again.</summary>
    public required int PairCooldownHours { get; init; }

    /// <summary>Chance of starting an interaction at each check, in percent, times the traits with <see cref="TraitDef.InteractionChance"/>.</summary>
    public required int StartChancePercent { get; init; }

    /// <summary>The weight of family and friends, in percent of a stranger's, when choosing whom to interact with.</summary>
    public required int CloseWeightPercent { get; init; }

    /// <summary>The opinion both need of each other to be friends.</summary>
    public required int FriendOpinion { get; init; }

    /// <summary>The opinion at which either one makes them rivals.</summary>
    public required int RivalOpinion { get; init; }

    /// <summary>
    /// A friendship or rivalry already told is told again only after both opinions went this far
    /// back past its threshold, so one that comes and goes does not fill the log.
    /// </summary>
    public required int RetellMargin { get; init; }

    /// <summary>The opinion both need of each other to become a couple.</summary>
    public required int CoupleOpinion { get; init; }

    /// <summary>The romance both need for each other to become a couple.</summary>
    public required int CoupleRomance { get; init; }

    /// <summary>A couple breaks up when either one's opinion of the other falls under this.</summary>
    public required int BreakupBelow { get; init; }

    /// <summary>Points each opinion comes back towards 0 on a day the two did not interact.</summary>
    public required int ForgetPerDay { get; init; }

    /// <summary>The opinion of family and of a partner does not fall under this by forgetting.</summary>
    public required int FamilyFloor { get; init; }

    /// <summary>What each founder thinks of each other, drawn.</summary>
    public required IntRange FounderOpinion { get; init; }

    /// <summary>Years of life before a widow or widower can form a couple again.</summary>
    public required int WidowYears { get; init; }

    /// <summary>How many of its last interactions a colonist remembers, for its sheet.</summary>
    public required int RecentInteractions { get; init; }

    internal override IEnumerable<string> Validate()
    {
        if (CheckTicks <= 0 || Radius < 0 || PairCooldownHours < 0 || RecentInteractions < 0 || WidowYears < 0 || ForgetPerDay < 0 || RetellMargin < 0)
        {
            yield return "checkTicks must be positive, and radius, pairCooldownHours, forgetPerDay, widowYears, recentInteractions and retellMargin zero or more.";
        }

        if (StartChancePercent is < 0 or > 100 || CloseWeightPercent < 0)
        {
            yield return "startChancePercent must be between 0 and 100, and closeWeightPercent zero or more.";
        }

        if (RivalOpinion >= FriendOpinion || FounderOpinion.Min > FounderOpinion.Max)
        {
            yield return "rivalOpinion must be under friendOpinion, and founderOpinion needs min <= max.";
        }

        // Opinions go from -100 to 100 and romance from 0 to 100: a threshold out of them never holds.
        if (new[] { FriendOpinion, RivalOpinion, CoupleOpinion, BreakupBelow, FounderOpinion.Min, FounderOpinion.Max }.Any(opinion => opinion is < -100 or > 100)
            || CoupleRomance is < 0 or > 100 || FamilyFloor is < 0 or > 100)
        {
            yield return "opinions must be between -100 and 100, and coupleRomance and familyFloor between 0 and 100.";
        }
    }
}

/// <summary>Founders who arrive as couples of a woman and a man (see "Llegada" in <c>docs/gdd/vida-y-generaciones.md</c>).</summary>
public sealed class FoundingCouple
{
    /// <summary>How many couples arrive: the first two colonists, then the next two, and so on.</summary>
    public int Count { get; init; } = 1;

    public required int MinYears { get; init; }

    public required int MaxYears { get; init; }

    /// <summary>What each of them thinks of the other.</summary>
    public required int Opinion { get; init; }

    public required int Romance { get; init; }
}
