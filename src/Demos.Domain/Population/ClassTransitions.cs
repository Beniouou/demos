namespace Demos.Domain.Population;

/// <summary>
/// Passages entre classes : chaque passage a son propre mécanisme. Une classe est définie par son rang et son
/// idéologie ; quand des membres changent de rang, ils gardent leur idéologie et rejoignent la classe d'arrivée,
/// qui naît si elle n'existe pas encore. Pour l'instant :
/// - vers le haut, par l'éducation : ouvrier → profession intermédiaire (éducation de base),
///   profession intermédiaire → cadre (éducation avancée) ;
/// - vers le bas, par le chômage : des chômeurs descendent d'un rang (sauf les ouvriers) et deviennent
///   ségrégationnistes s'ils étaient égalitaristes.
/// </summary>
public static class ClassTransitions
{
    /// <summary>Proposition : une année d'études (365 points) fait monter un membre d'un rang.</summary>
    public const double EducationPointsPerPromotion = 365;

    /// <summary>Proposition : au plus 2 % des membres d'une classe changent de rang par saison.</summary>
    public const double MaxSeasonalShare = 0.02;

    /// <summary>Un passage d'un rang à un autre, et la ressource de la classe qu'il dépense.</summary>
    private sealed record Promotion(Rank From, Rank To, Func<SocialClass, double> Points, Action<SocialClass, double> Spend);

    private static readonly Promotion[] Promotions =
    [
        new(Rank.Workers, Rank.Intermediate, c => c.EducationPoints, (c, spent) => c.EducationPoints -= spent),
        new(Rank.Intermediate, Rank.Executives, c => c.AdvancedEducationPoints, (c, spent) => c.AdvancedEducationPoints -= spent),
    ];

    /// <summary>Proposition : le chômage pèse au-delà de 20 % de chômeurs dans la classe.</summary>
    public const double UnemploymentThreshold = 0.2;

    /// <summary>Proposition : chaque saison, 10 % des chômeurs d'une classe touchée descendent ou basculent.</summary>
    public const double UnemployedShareAffected = 0.1;

    /// <summary>Calcul saisonnier. Renvoie les classes nées de ces passages, à ajouter à la partie.</summary>
    public static List<SocialClass> RunSeasonal(IReadOnlyList<SocialClass> classes)
    {
        var all = classes.ToList();
        var created = new List<SocialClass>();

        SocialClass Destination(Rank rank, Ideology ideology)
        {
            var target = all.FirstOrDefault(c => c.Rank == rank && c.Ideology == ideology);
            if (target is not null)
                return target;

            target = Create(rank, ideology);
            all.Add(target);
            created.Add(target);
            return target;
        }

        RunUnemployment(all, Destination);
        RunPromotions(all, Destination);

        foreach (var socialClass in all)
            socialClass.WealthPerMemberLastSeason = socialClass.WealthPerMember;

        return created;
    }

    /// <summary>
    /// Une classe dont plus de 20 % des membres sont au chômage et dont la richesse par membre n'a pas augmenté
    /// depuis la saison précédente (elle a baissé, ou elle est déjà épuisée) voit 10 % de ses chômeurs descendre
    /// d'un rang (s'ils le peuvent) et devenir ségrégationnistes s'ils étaient égalitaristes.
    /// Des ouvriers ségrégationnistes ne changent pas.
    /// </summary>
    private static void RunUnemployment(List<SocialClass> all, Func<Rank, Ideology, SocialClass> destination)
    {
        foreach (var source in all.Where(c => c.Population > 0).ToList())
        {
            var hit = source.UnemployedMembers / source.Population > UnemploymentThreshold
                && source.WealthPerMemberLastSeason is { } previous
                && source.WealthPerMember <= previous;
            if (!hit)
                continue;

            var rank = source.Rank == Rank.Workers ? Rank.Workers : source.Rank - 1;
            var ideology = source.Ideology.With(IdeologicalAxis.Hierarchy, Pole.B);
            if (rank == source.Rank && ideology == source.Ideology)
                continue;

            var count = (long)(source.UnemployedMembers * UnemployedShareAffected);
            if (count > 0)
                source.TransferMembersTo(destination(rank, ideology), count);
        }
    }

    private static void RunPromotions(List<SocialClass> all, Func<Rank, Ideology, SocialClass> destination)
    {
        foreach (var promotion in Promotions)
        {
            foreach (var source in all.Where(c => c.Rank == promotion.From && c.Population > 0).ToList())
            {
                var count = (long)Math.Min(promotion.Points(source) / EducationPointsPerPromotion, source.Population * MaxSeasonalShare);
                if (count <= 0)
                    continue;

                promotion.Spend(source, count * EducationPointsPerPromotion);
                source.TransferMembersTo(destination(promotion.To, source.Ideology), count);
            }
        }
    }

    /// <summary>
    /// Crée une classe vide ; son nom se forme à partir du rang et de l'idéologie
    /// (« Professions intermédiaires communistes »).
    /// </summary>
    public static SocialClass Create(Rank rank, Ideology ideology)
    {
        var ideologyName = ideology.Name.ToLowerInvariant();
        var plural = ideologyName.EndsWith('s') ? ideologyName : ideologyName + "s";
        return new SocialClass($"{RankSlug(rank)}-{ideologyName}", $"{RankName(rank)} {plural}", rank, ideology, 0);
    }

    public static string RankName(Rank rank) => rank switch
    {
        Rank.Workers => "Ouvriers",
        Rank.Intermediate => "Professions intermédiaires",
        _ => "Cadres",
    };

    private static string RankSlug(Rank rank) => rank switch
    {
        Rank.Workers => "ouvriers",
        Rank.Intermediate => "intermediaires",
        _ => "cadres",
    };
}
