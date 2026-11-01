namespace Demos.Domain.Population;

/// <summary>
/// Passages entre classes : chaque passage a son propre mécanisme. Une classe est définie par son rang et son
/// idéologie ; quand des membres changent de classe, ils rejoignent la classe d'arrivée, qui naît si elle
/// n'existe pas encore. Pour l'instant :
/// - chaque année, vers le haut, par l'éducation : ouvrier → profession intermédiaire (éducation de base),
///   profession intermédiaire → cadre (éducation avancée), en gardant leur idéologie ;
/// - chaque saison, vers le bas, par le chômage : des chômeurs descendent d'un rang (sauf les ouvriers) et
///   deviennent ségrégationnistes s'ils étaient égalitaristes ;
/// - chaque saison, par le plein emploi : des membres d'une classe ségrégationniste redeviennent égalitaristes.
/// </summary>
public static class ClassTransitions
{
    /// <summary>Proposition : une année d'études (365 points) fait monter un membre d'un rang.</summary>
    public const double EducationPointsPerPromotion = 365;

    /// <summary>Proposition : au plus 2 % des membres d'une classe montent de rang chaque année.</summary>
    public const double MaxYearlyPromotionShare = 0.02;

    /// <summary>Proposition : le chômage pèse au-delà de 20 % de chômeurs dans la classe.</summary>
    public const double UnemploymentThreshold = 0.2;

    /// <summary>Proposition : chaque saison, 10 % des chômeurs d'une classe touchée descendent ou basculent.</summary>
    public const double UnemployedShareAffected = 0.1;

    /// <summary>Proposition : une classe est en plein emploi quand moins de 5 % de ses membres sont au chômage.</summary>
    public const double FullEmploymentThreshold = 0.05;

    /// <summary>Proposition : chaque saison, 5 % des membres d'une classe ségrégationniste en plein emploi redeviennent égalitaristes.</summary>
    public const double FullEmploymentShareAffected = 0.05;

    /// <summary>Un passage d'un rang à un autre, et la ressource de la classe qu'il dépense.</summary>
    private sealed record Promotion(Rank From, Rank To, Func<SocialClass, double> Points, Action<SocialClass, double> Spend);

    private static readonly Promotion[] Promotions =
    [
        new(Rank.Workers, Rank.Intermediate, c => c.EducationPoints, (c, spent) => c.EducationPoints -= spent),
        new(Rank.Intermediate, Rank.Executives, c => c.AdvancedEducationPoints, (c, spent) => c.AdvancedEducationPoints -= spent),
    ];

    /// <summary>Calcul annuel : les promotions par l'éducation. Renvoie les classes nées de ces passages.</summary>
    public static List<SocialClass> RunYearly(IReadOnlyList<SocialClass> classes)
    {
        var destinations = new Destinations(classes);

        foreach (var promotion in Promotions)
        {
            foreach (var source in destinations.All.Where(c => c.Rank == promotion.From && c.Population > 0).ToList())
            {
                var count = (long)Math.Min(promotion.Points(source) / EducationPointsPerPromotion, source.Population * MaxYearlyPromotionShare);
                if (count <= 0)
                    continue;

                promotion.Spend(source, count * EducationPointsPerPromotion);
                source.TransferMembersTo(destinations.Get(promotion.To, source.Ideology), count);
            }
        }

        return destinations.Created;
    }

    /// <summary>Calcul saisonnier : le chômage et le plein emploi. Renvoie les classes nées de ces passages.</summary>
    public static List<SocialClass> RunSeasonal(IReadOnlyList<SocialClass> classes)
    {
        var destinations = new Destinations(classes);
        var sources = destinations.All.Where(c => c.Population > 0).ToList();

        foreach (var source in sources)
        {
            var unemploymentRate = source.UnemployedMembers / source.Population;

            if (unemploymentRate > UnemploymentThreshold
                && source.WealthPerMemberLastSeason is { } previous
                && source.WealthPerMember <= previous)
            {
                ApplyUnemployment(source, destinations);
            }
            else if (unemploymentRate < FullEmploymentThreshold
                && source.Ideology[IdeologicalAxis.Hierarchy] == Pole.B)
            {
                var count = (long)(source.Population * FullEmploymentShareAffected);
                if (count > 0)
                    source.TransferMembersTo(destinations.Get(source.Rank, source.Ideology.With(IdeologicalAxis.Hierarchy, Pole.A)), count);
            }
        }

        foreach (var socialClass in destinations.All)
            socialClass.WealthPerMemberLastSeason = socialClass.WealthPerMember;

        return destinations.Created;
    }

    /// <summary>
    /// Chômage : une classe dont plus de 20 % des membres sont au chômage et dont la richesse par membre n'a pas
    /// augmenté depuis la saison précédente (elle a baissé, ou elle est déjà épuisée) voit 10 % de ses chômeurs
    /// descendre d'un rang (s'ils le peuvent) et devenir ségrégationnistes s'ils étaient égalitaristes.
    /// Des ouvriers ségrégationnistes ne changent pas.
    /// </summary>
    private static void ApplyUnemployment(SocialClass source, Destinations destinations)
    {
        var rank = source.Rank == Rank.Workers ? Rank.Workers : source.Rank - 1;
        var ideology = source.Ideology.With(IdeologicalAxis.Hierarchy, Pole.B);
        if (rank == source.Rank && ideology == source.Ideology)
            return;

        var count = (long)(source.UnemployedMembers * UnemployedShareAffected);
        if (count > 0)
            source.TransferMembersTo(destinations.Get(rank, ideology), count);
    }

    /// <summary>Trouve la classe d'arrivée d'un passage, ou la crée si elle n'existe pas encore.</summary>
    private sealed class Destinations(IReadOnlyList<SocialClass> classes)
    {
        public List<SocialClass> All { get; } = classes.ToList();
        public List<SocialClass> Created { get; } = [];

        public SocialClass Get(Rank rank, Ideology ideology)
        {
            var target = All.FirstOrDefault(c => c.Rank == rank && c.Ideology == ideology);
            if (target is not null)
                return target;

            target = Create(rank, ideology);
            All.Add(target);
            Created.Add(target);
            return target;
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
