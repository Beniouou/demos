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
/// - chaque saison, sur l'axe hiérarchie aussi : une classe qui travaille sans pouvoir couvrir ses besoins devient
///   égalitariste, une classe comblée mais fortement prélevée devient ségrégationniste ;
/// - chaque saison, sur l'axe propriété (décidé le 23 novembre 2026) : l'expérience du travail (un lieu de travail
///   collectif qui va bien rend collectiviste, en difficulté libéral ; l'inverse pour une entreprise privée) et ce que
///   l'on posséde (posséder une entreprise privée retient vers libéral, une SCOP vers collectiviste) ;
/// - chaque saison, sur l'axe production : vivre dans une commune polluée rend écologiste, manquer de
///   produits ou de places rend positiviste.
/// Les passages saisonniers se jugent commune par commune (voir <see cref="RunSeasonal"/>).
/// </summary>
public static class ClassTransitions
{
    /// <summary>Proposition : une année d'études (365 points) fait monter un membre d'un rang.</summary>
    public static double EducationPointsPerPromotion => Economy.Balance.Current.TransitionEducationPointsPerPromotion;

    /// <summary>Proposition : au plus 2 % des membres d'une classe montent de rang chaque année.</summary>
    public static double MaxYearlyPromotionShare => Economy.Balance.Current.TransitionMaxYearlyPromotionShare;

    /// <summary>Proposition : le chômage pèse au-delà de 20 % de chômeurs dans la classe.</summary>
    public static double UnemploymentThreshold => Economy.Balance.Current.TransitionUnemploymentThreshold;

    /// <summary>Proposition : chaque saison, 10 % des chômeurs d'une classe touchée descendent ou basculent.</summary>
    public static double UnemployedShareAffected => Economy.Balance.Current.TransitionUnemployedShare;

    /// <summary>Proposition : une classe est en plein emploi quand moins de 5 % de ses membres sont au chômage.</summary>
    public static double FullEmploymentThreshold => Economy.Balance.Current.TransitionFullEmploymentThreshold;

    /// <summary>Proposition : chaque saison, 5 % des membres d'une classe ségrégationniste en plein emploi redeviennent égalitaristes.</summary>
    public static double FullEmploymentShareAffected => Economy.Balance.Current.TransitionFullEmploymentShare;

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
    public static double MoneyShortfallThreshold => Economy.Balance.Current.TransitionMoneyShortfallThreshold;

    /// <summary>Proposition : un prélèvement est important au-delà de 30 % des revenus de la saison.</summary>
    public static double HeavyTaxRateThreshold => Economy.Balance.Current.TransitionHeavyTaxThreshold;

    /// <summary>Proposition : une classe a ses besoins complètement satisfaits au-delà de 99 %.</summary>
    public static double FullySatisfiedThreshold => Economy.Balance.Current.TransitionFullySatisfiedThreshold;

    /// <summary>Proposition : chaque saison, 5 % des membres d'une classe concernée basculent sur l'axe propriété.</summary>
    public static double PropertyShareAffected => Economy.Balance.Current.TransitionPropertyShare;

    /// <summary>Proposition : une classe manque de produits quand 20 % de ses besoins restent non couverts faute de produits ou de places.</summary>
    public static double SupplyShortageThreshold => Economy.Balance.Current.TransitionSupplyShortageThreshold;

    /// <summary>Proposition : chaque saison, 5 % des membres concernés basculent sur l'axe production.</summary>
    public static double ProductionShareAffected => Economy.Balance.Current.TransitionProductionShare;

    /// <summary>Proposition : chaque saison, 5 % des membres basculent sur l'axe hiérarchie par le manque d'argent ou les prélèvements.</summary>
    public static double HierarchyShareAffected => Economy.Balance.Current.TransitionHierarchyShare;

    /// <summary>Proposition : une classe en crise fait confiance aux institutions à partir de 0,5 d'adhésion.</summary>
    public static double TrustThreshold => Economy.Balance.Current.TransitionTrustThreshold;

    /// <summary>Proposition : quand la crise divise, la moitié des membres concernés va vers l'égalitarisme, l'autre vers le ségrégationnisme.</summary>
    public static double CrisisSplit => Economy.Balance.Current.TransitionCrisisSplit;

    /// <summary>Proposition : chaque saison, 2 % des élèves rejoignent un pôle de La Promesse.</summary>
    public static double PromiseSchoolShare => Economy.Balance.Current.TransitionPromiseSchoolShare;

    /// <summary>Un départ prévu : des membres d'une classe, pris dans une commune, vers une autre classe.</summary>
    private sealed record Move(SocialClass Source, string CommuneId, Rank Rank, Ideology Ideology, double Members);

    /// <summary>
    /// Calcul saisonnier, commune par commune : là où vivent des membres d'une classe, on juge leur situation
    /// et seuls les membres de cette commune changent de classe. Chômage et plein emploi (axe hiérarchie) et
    /// pollution se mesurent dans la commune ; manque d'argent, prélèvements et pénurie de produits, qui
    /// dépendent du marché national et de la richesse de la classe, se mesurent pour toute la classe.
    /// Renvoie les classes nées de ces passages.
    /// </summary>
    /// <param name="isStruggling">
    /// Un bâtiment est-il en difficulté (moins de 80 % des salaires payés sur 30 jours) ? Sans lui, l'expérience du
    /// travail ne joue pas sur l'axe propriété (seule la propriété des entreprises compte).
    /// </param>
    public static List<SocialClass> RunSeasonal(
        IReadOnlyList<SocialClass> classes,
        IReadOnlyDictionary<string, Structure> structures,
        IReadOnlyDictionary<string, double> pollutionByCommune,
        Func<string, bool>? isStruggling = null,
        PromiseDefinition? promise = null)
    {
        var destinations = new ClassFinder(classes);
        var workplacesByCommune = Communes.All(structures).ToDictionary(
            commune => commune.Id,
            commune => structures.Values.Where(s => s.Jobs > 0 && Communes.CommuneOf(s, structures) == commune.Id).ToList());

        // Toutes les mesures sont prises avant le moindre départ, pour que chaque axe juge la même situation.
        var moves = new List<Move>();
        foreach (var source in destinations.All.Where(c => c.Population > 0))
        {
            var ownership = OwnershipOf(source, structures);
            foreach (var (communeId, workplaces) in workplacesByCommune)
            {
                var living = source.MembersLivingIn(communeId);
                if (living < 1)
                    continue;

                var employed = workplaces.Sum(s => source.MembersWorkingIn(s.Id));
                var unemployed = Math.Max(0, living - employed);
                var work = isStruggling is null ? default : WorkExperienceOf(source, workplaces, isStruggling);
                moves.AddRange(PlanMoves(source, communeId, living, unemployed / living, unemployed, pollutionByCommune.GetValueOrDefault(communeId),
                    work, ownership, promise));
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

    /// <summary>
    /// Les membres d'une classe que leur travail pousse sur l'axe propriété, dans une commune : vers collectiviste
    /// (un lieu public ou une SCOP qui va bien, une entreprise privée en difficulté), vers libéral (l'inverse).
    /// </summary>
    private readonly record struct WorkExperience(double TowardCollective, double TowardPrivate);

    /// <summary>Ce que possède une classe : des entreprises privées, des SCOP.</summary>
    private readonly record struct Holdings(bool OwnsPrivate, bool OwnsCooperative);

    private static WorkExperience WorkExperienceOf(SocialClass source, IEnumerable<Structure> workplaces, Func<string, bool> isStruggling)
    {
        double collective = 0, @private = 0;
        foreach (var workplace in workplaces)
        {
            var members = source.MembersWorkingIn(workplace.Id);
            if (members <= 0)
                continue;

            var goesWell = !isStruggling(workplace.Id);
            if ((workplace.Ownership == Ownership.Private) == goesWell)
                @private += members;
            else
                collective += members;
        }

        return new WorkExperience(collective, @private);
    }

    private static Holdings OwnershipOf(SocialClass source, IReadOnlyDictionary<string, Structure> structures) => new(
        structures.Values.Any(s => s.Ownership == Ownership.Private && s.OwnerClassId == source.Id),
        structures.Values.Any(s => s.Ownership == Ownership.Cooperative && s.OwnerClassId == source.Id));

    private static IEnumerable<Move> PlanMoves(
        SocialClass source, string communeId, double living, double unemploymentRate, double unemployed, double pollution,
        WorkExperience work = default, Holdings holdings = default, PromiseDefinition? promise = null)
    {
        var ideology = source.Ideology;
        Move Switch(IdeologicalAxis axis, Pole pole, double share) =>
            new(source, communeId, source.Rank, ideology.With(axis, pole), living * share);

        // Axe hiérarchie (crise : solidarité ou repli, décidé le 23 novembre 2026). Une classe en crise — le chômage
        // (plus de 20 % de chômeurs et une richesse par membre qui ne monte plus) ou le manque d'argent en travaillant —
        // bascule selon la confiance (son adhésion) et le déclassement (richesse par membre en baisse sur la saison, ou
        // descente de rang) : confiance sans déclassement, solidarité (vers l'égalitarisme) ; défiance et déclassement,
        // repli (vers le ségrégationnisme) ; sinon la crise divise. Le chômage fait aussi descendre d'un rang (10 % des
        // chômeurs), le manque d'argent fait basculer 5 % des membres. Hors crise : le plein emploi ramène 5 % des
        // ségrégationnistes vers l'égalitarisme, et être comblé mais fortement prélevé rend ségrégationniste.
        var trusting = source.Adhesion >= TrustThreshold;
        var poorer = source.WealthPerMemberLastSeason is { } previous && source.WealthPerMember < previous;
        var unemploymentCrisis = unemploymentRate > UnemploymentThreshold
            && source.WealthPerMemberLastSeason is { } last && source.WealthPerMember <= last;
        var moneyCrisis = !unemploymentCrisis
            && unemploymentRate <= UnemploymentThreshold && source.SeasonMoneyShortfall >= MoneyShortfallThreshold;

        if (unemploymentCrisis || moneyCrisis)
        {
            var rank = unemploymentCrisis && source.Rank != Rank.Workers ? source.Rank - 1 : source.Rank;
            var members = unemploymentCrisis ? unemployed * UnemployedShareAffected : living * HierarchyShareAffected;
            var declassed = poorer || rank != source.Rank;
            var towardEgalitarian = trusting && !declassed ? 1 : !trusting && declassed ? 0 : CrisisSplit;
            foreach (var (pole, part) in new[] { (Pole.A, towardEgalitarian), (Pole.B, 1 - towardEgalitarian) })
            {
                var target = ideology.With(IdeologicalAxis.Hierarchy, pole);
                if (part > 0 && (rank != source.Rank || target != ideology))
                    yield return new Move(source, communeId, rank, target, members * part);
            }
        }
        else if (ideology[IdeologicalAxis.Hierarchy] == Pole.B && unemploymentRate < FullEmploymentThreshold)
        {
            yield return Switch(IdeologicalAxis.Hierarchy, Pole.A, FullEmploymentShareAffected);
        }
        else if (ideology[IdeologicalAxis.Hierarchy] == Pole.A
            && source.NeedsSatisfaction >= FullySatisfiedThreshold
            && source.SeasonTaxRate >= HeavyTaxRateThreshold)
        {
            yield return Switch(IdeologicalAxis.Hierarchy, Pole.B, HierarchyShareAffected);
        }

        // Axe propriété (décidé le 23 novembre 2026). L'expérience du travail : les membres d'un lieu collectif qui va
        // bien, ou d'une entreprise privée en difficulté, tirent vers le collectivisme ; l'inverse vers le libéralisme.
        // Ce que l'on possède retient (ou pousse) toute la classe : une entreprise privée vers le libéralisme, une SCOP
        // vers le collectivisme. Chaque membre compte une fois ; 5 % des membres poussés basculent.
        var ownersPull = living * ((holdings.OwnsCooperative ? 1 : 0) - (holdings.OwnsPrivate ? 1 : 0));
        if (ideology[IdeologicalAxis.Property] == Pole.B)
        {
            var pushed = Math.Clamp(work.TowardCollective + ownersPull, 0, living);
            if (pushed >= 1)
                yield return new Move(source, communeId, source.Rank, ideology.With(IdeologicalAxis.Property, Pole.A), pushed * PropertyShareAffected);
        }
        else
        {
            var pushed = Math.Clamp(work.TowardPrivate - ownersPull, 0, living);
            if (pushed >= 1)
                yield return new Move(source, communeId, source.Rank, ideology.With(IdeologicalAxis.Property, Pole.B), pushed * PropertyShareAffected);
        }

        // Axe production. Vivre dans une commune polluée rend écologiste ;
        // manquer de produits ou de places (et non d'argent) rend positiviste.
        if (pollution >= Economy.Pollution.PollutedThreshold && ideology[IdeologicalAxis.Production] == Pole.A)
            yield return Switch(IdeologicalAxis.Production, Pole.B, ProductionShareAffected);
        else if (source.SeasonSupplyShortage >= SupplyShortageThreshold && ideology[IdeologicalAxis.Production] == Pole.B)
            yield return Switch(IdeologicalAxis.Production, Pole.A, ProductionShareAffected);

        // L'école enseigne La Promesse (décidé le 23 novembre 2026, mode avec promesse) : 2 % des élèves de la saison
        // rejoignent un pôle de La Promesse, sur le premier axe où la classe s'en écarte (un axe à la fois).
        var missing = promise?.Orientations.Where(p => ideology[p.Axis] != p.Pole).ToList() ?? [];
        if (missing.Count > 0 && source.SeasonPupils > 0 && source.Population > 0)
        {
            var wanted = missing[0];
            var pupilsHere = source.SeasonPupils * living / source.Population;
            yield return new Move(source, communeId, source.Rank, ideology.With(wanted.Axis, wanted.Pole), pupilsHere * PromiseSchoolShare);
        }
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
