using Demos.Domain.Politics;

namespace Demos.Domain.Population;

/// <summary>
/// Passages entre classes : chaque passage a son propre mécanisme. Une classe est définie par son rang et son
/// idéologie ; quand des membres changent de classe, ils rejoignent la classe d'arrivée, qui naît si elle
/// n'existe pas encore. Pour l'instant :
/// - chaque année, vers le haut, par l'éducation : ouvrier → profession intermédiaire (éducation de base),
///   profession intermédiaire → cadre (éducation avancée), en gardant leur idéologie ;
/// - chaque saison, vers le bas, par le chômage : des chômeurs descendent d'un rang (sauf les ouvriers) et
///   deviennent ségrégationnistes s'ils étaient égalitaristes ;
/// - chaque saison, par le plein emploi : des membres d'une classe ségrégationniste redeviennent égalitaristes ;
/// - chaque saison, sur l'axe propriété : une classe qui travaille sans pouvoir couvrir ses besoins devient
///   protectionniste, une classe comblée mais fortement prélevée devient libérale ;
/// - chaque saison, sur l'axe production : vivre dans une commune polluée rend écologiste, manquer de
///   produits ou de places rend positiviste.
/// Les passages saisonniers se jugent commune par commune (voir <see cref="RunSeasonal"/>).
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
        var destinations = new ClassFinder(classes);

        foreach (var promotion in Promotions)
        {
            foreach (var source in destinations.All.Where(c => c.Rank == promotion.From && c.Population > 0).ToList())
            {
                var count = (long)Math.Min(promotion.Points(source) / EducationPointsPerPromotion, source.Population * MaxYearlyPromotionShare);
                if (count <= 0)
                    continue;

                promotion.Spend(source, count * EducationPointsPerPromotion);
                source.TransferMembersTo(destinations.Get(promotion.To, source.Ideology, source.CommuneId), count);
            }
        }

        return destinations.Created;
    }

    /// <summary>Proposition : une classe qui travaille manque d'argent quand 20 % de ses besoins restent non couverts faute d'argent.</summary>
    public const double MoneyShortfallThreshold = 0.2;

    /// <summary>Proposition : un prélèvement est important au-delà de 30 % des revenus de la saison.</summary>
    public const double HeavyTaxRateThreshold = 0.3;

    /// <summary>Proposition : une classe a ses besoins complètement satisfaits au-delà de 99 %.</summary>
    public const double FullySatisfiedThreshold = 0.99;

    /// <summary>Proposition : chaque saison, 5 % des membres d'une classe concernée basculent sur l'axe propriété.</summary>
    public const double PropertyShareAffected = 0.05;

    /// <summary>Proposition : une classe manque de produits quand 20 % de ses besoins restent non couverts faute de produits ou de places.</summary>
    public const double SupplyShortageThreshold = 0.2;

    /// <summary>Proposition : chaque saison, 5 % des membres concernés basculent sur l'axe production.</summary>
    public const double ProductionShareAffected = 0.05;

    /// <summary>Un départ prévu : des membres d'une classe, pris dans une commune, vers une autre classe.</summary>
    private sealed record Move(SocialClass Source, string CommuneId, Rank Rank, Ideology Ideology, double Members);

    /// <summary>
    /// Calcul saisonnier, commune par commune : là où vivent des membres d'une classe, on juge leur situation
    /// et seuls les membres de cette commune changent de classe. Chômage et plein emploi (axe hiérarchie) et
    /// pollution se mesurent dans la commune ; manque d'argent, prélèvements et pénurie de produits, qui
    /// dépendent du marché national et de la richesse de la classe, se mesurent pour toute la classe.
    /// Renvoie les classes nées de ces passages.
    /// </summary>
    public static List<SocialClass> RunSeasonal(
        IReadOnlyList<SocialClass> classes,
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> pollutionByCommune)
    {
        var destinations = new ClassFinder(classes);
        var workplacesByCommune = Communes.All(structures).ToDictionary(
            commune => commune.Id,
            commune => structures.Values.Where(s => s.Jobs > 0 && Communes.CommuneOf(s, structures) == commune.Id).ToList());

        // Toutes les mesures sont prises avant le moindre départ, pour que chaque axe juge la même situation.
        var moves = new List<Move>();
        foreach (var source in destinations.All.Where(c => c.Population > 0))
        {
            foreach (var (communeId, workplaces) in workplacesByCommune)
            {
                var living = source.MembersLivingIn(communeId);
                if (living < 1)
                    continue;

                var employed = workplaces.Sum(s => source.MembersWorkingIn(s.Id));
                var unemployed = Math.Max(0, living - employed);
                moves.AddRange(PlanMoves(source, communeId, living, unemployed / living, unemployed, pollutionByCommune.GetValueOrDefault(communeId)));
            }
        }

        foreach (var move in moves)
            move.Source.TransferMembersTo(destinations.Get(move.Rank, move.Ideology, move.CommuneId), (long)move.Members, move.CommuneId);

        foreach (var socialClass in destinations.All)
        {
            socialClass.WealthPerMemberLastSeason = socialClass.WealthPerMember;
            socialClass.StartNewSeason();
        }

        return destinations.Created;
    }

    private static IEnumerable<Move> PlanMoves(
        SocialClass source, string communeId, double living, double unemploymentRate, double unemployed, double pollution)
    {
        var ideology = source.Ideology;
        Move Switch(IdeologicalAxis axis, Pole pole, double share) =>
            new(source, communeId, source.Rank, ideology.With(axis, pole), living * share);

        // Axe hiérarchie. Chômage : si plus de 20 % de chômeurs et une richesse par membre qui n'augmente plus,
        // 10 % des chômeurs descendent d'un rang (s'ils le peuvent) et deviennent ségrégationnistes.
        // Plein emploi : moins de 5 % de chômeurs ramène 5 % des ségrégationnistes vers l'égalitarisme.
        if (unemploymentRate > UnemploymentThreshold
            && source.WealthPerMemberLastSeason is { } previous
            && source.WealthPerMember <= previous)
        {
            var rank = source.Rank == Rank.Workers ? Rank.Workers : source.Rank - 1;
            var segregationist = ideology.With(IdeologicalAxis.Hierarchy, Pole.B);
            if (rank != source.Rank || segregationist != ideology)
                yield return new Move(source, communeId, rank, segregationist, unemployed * UnemployedShareAffected);
        }
        else if (unemploymentRate < FullEmploymentThreshold && ideology[IdeologicalAxis.Hierarchy] == Pole.B)
        {
            yield return Switch(IdeologicalAxis.Hierarchy, Pole.A, FullEmploymentShareAffected);
        }

        // Axe propriété. Travailler sans pouvoir couvrir ses besoins rend protectionniste ;
        // être comblé mais fortement prélevé rend libéral.
        if (unemploymentRate <= UnemploymentThreshold
            && source.SeasonMoneyShortfall >= MoneyShortfallThreshold
            && ideology[IdeologicalAxis.Property] == Pole.B)
        {
            yield return Switch(IdeologicalAxis.Property, Pole.A, PropertyShareAffected);
        }
        else if (source.NeedsSatisfaction >= FullySatisfiedThreshold
            && source.SeasonTaxRate >= HeavyTaxRateThreshold
            && ideology[IdeologicalAxis.Property] == Pole.A)
        {
            yield return Switch(IdeologicalAxis.Property, Pole.B, PropertyShareAffected);
        }

        // Axe production. Vivre dans une commune polluée rend écologiste ;
        // manquer de produits ou de places (et non d'argent) rend positiviste.
        if (pollution >= Economy.Pollution.PollutedThreshold && ideology[IdeologicalAxis.Production] == Pole.A)
            yield return Switch(IdeologicalAxis.Production, Pole.B, ProductionShareAffected);
        else if (source.SeasonSupplyShortage >= SupplyShortageThreshold && ideology[IdeologicalAxis.Production] == Pole.B)
            yield return Switch(IdeologicalAxis.Production, Pole.A, ProductionShareAffected);
    }

    /// <summary>
    /// Crée un groupe local vide dans une commune ; son nom se forme à partir du rang et de l'idéologie
    /// (« Professions intermédiaires communistes »), le même dans toutes les communes.
    /// </summary>
    public static SocialClass Create(Rank rank, Ideology ideology, string? communeId)
    {
        var ideologyName = ideology.Name.ToLowerInvariant();
        var plural = ideologyName.EndsWith('s') ? ideologyName : ideologyName + "s";
        var socialClass = new SocialClass(
            $"{RankSlug(rank)}-{ideologyName}-{communeId ?? "nulle-part"}", $"{RankName(rank)} {plural}", rank, ideology, 0);
        if (communeId is not null)
            socialClass.SetResidence(communeId, 1);
        return socialClass;
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
