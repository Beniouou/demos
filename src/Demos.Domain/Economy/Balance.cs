using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;

namespace Demos.Domain.Economy;

/// <summary>
/// Réglages d'équilibrage de l'économie, lus dans game/data/equilibrage.json (le tableur d'équilibrage part des mêmes
/// chiffres). Les valeurs par défaut sont celles du jeu ; ce sont toutes des propositions à régler.
/// </summary>
public sealed record BalanceSettings
{
    /// <summary>
    /// Possession visée par personne des besoins durables (décidé le 20 novembre 2026), selon le rang : un besoin qui y
    /// figure se couvre par un stock d'objets durables, qui s'use, au lieu d'achats consommés chaque jour.
    /// </summary>
    public Dictionary<Rank, Dictionary<NeedCategory, double>> PossessionByRank { get; init; } = new()
    {
        [Rank.Workers] = new() { [NeedCategory.Clothing] = 4, [NeedCategory.Furniture] = 4 },
        [Rank.Intermediate] = new() { [NeedCategory.Clothing] = 7, [NeedCategory.Furniture] = 15 },
        [Rank.Executives] = new() { [NeedCategory.Clothing] = 18, [NeedCategory.Furniture] = 75 },
    };

    /// <summary>Outils visés par emploi productif d'un bâtiment qui a une recette (décidé le 20 novembre 2026 : 0,1).</summary>
    public double ToolsPerJob { get; init; } = 0.1;

    /// <summary>Production perdue par un bâtiment qui n'a aucun outil (0,2) ; en proportion de ce qui lui manque.</summary>
    public double ToolShortagePenalty { get; init; } = 0.2;

    /// <summary>Part du manque d'objets durables qu'un groupe comble au plus chaque jour, en plus de l'usure (1/30).</summary>
    public double PossessionCatchUp { get; init; } = 1.0 / 30;

    /// <summary>Besoins par personne et par jour, selon le rang : les mêmes pour tous, seules les quantités changent.</summary>
    public Dictionary<Rank, Dictionary<NeedCategory, double>> NeedsByRank { get; init; } = new()
    {
        [Rank.Workers] = new()
        {
            [NeedCategory.Food] = 1,
            [NeedCategory.Clothing] = 0.01,
            [NeedCategory.Furniture] = 0.005,
            [NeedCategory.Leisure] = 0.05,
            [NeedCategory.Administration] = 0.01,
            [NeedCategory.Security] = 0.01,
        },
        [Rank.Intermediate] = new()
        {
            [NeedCategory.Food] = 1,
            [NeedCategory.Clothing] = 0.02,
            [NeedCategory.Furniture] = 0.02,
            [NeedCategory.Leisure] = 0.1,
            [NeedCategory.Administration] = 0.02,
            [NeedCategory.Security] = 0.02,
        },
        [Rank.Executives] = new()
        {
            [NeedCategory.Food] = 1,
            [NeedCategory.Clothing] = 0.05,
            [NeedCategory.Furniture] = 0.1,
            [NeedCategory.Leisure] = 0.2,
            [NeedCategory.Administration] = 0.05,
            [NeedCategory.Security] = 0.05,
        },
    };

    /// <summary>Places de logement occupées par personne : les rangs supérieurs ont des logements plus grands.</summary>
    public Dictionary<Rank, double> HousingPlacesByRank { get; init; } = new()
    {
        [Rank.Workers] = 1,
        [Rank.Intermediate] = 2,
        [Rank.Executives] = 4,
    };

    /// <summary>Emplois d'encadrement pour un emploi principal (le rang juste au-dessus).</summary>
    public double SupervisorsPerJob { get; init; } = 0.1;

    /// <summary>Emplois de direction pour un emploi principal (deux rangs au-dessus).</summary>
    public double DirectorsPerJob { get; init; } = 0.01;

    /// <summary>Le salaire d'un emploi est le salaire de base du bâtiment × ce multiplicateur.</summary>
    public Dictionary<Rank, double> WageMultipliers { get; init; } = new()
    {
        [Rank.Workers] = 1,
        [Rank.Intermediate] = 2,
        [Rank.Executives] = 4,
    };

    /// <summary>Bénéfice qu'un bâtiment de référence dégage à plein régime : il fixe les prix de départ.</summary>
    public double Margin { get; init; } = 0.3;

    /// <summary>Une entreprise garde de quoi payer ce nombre de jours de salaires et d'énergie.</summary>
    public int CompanyReserveDays { get; init; } = 30;

    /// <summary>
    /// Réserve prise aux propriétaires au début de la partie, et que doivent couvrir les postes qu'une entreprise rouvre, en
    /// jours de salaires.
    /// </summary>
    public int StartingReserveDays { get; init; } = 30;

    /// <summary>
    /// Part de son bénéfice du jour qu'une entreprise met de côté tant que sa réserve n'est pas pleine ; elle verse le reste
    /// à ses propriétaires. 1 : elle garde tout jusqu'à remplir sa réserve.
    /// </summary>
    public double ReserveSavingsShare { get; init; } = 1;

    /// <summary>Bois ou charbon par emploi ou par place, et par jour, au printemps et en automne.</summary>
    public double HeatingPerUnit { get; init; } = 0.01;

    /// <summary>Eau par emploi ou par place, et par jour.</summary>
    public double WaterPerUnit { get; init; } = 0.1;

    /// <summary>Chauffage de chaque saison, par rapport au chauffage normal.</summary>
    public Dictionary<Season, double> HeatingBySeason { get; init; } = new()
    {
        [Season.Winter] = 2,
        [Season.Spring] = 1,
        [Season.Summer] = 0.5,
        [Season.Autumn] = 1,
    };

    /// <summary>Part de son activité qu'un bâtiment garde sans aucune énergie.</summary>
    public double OutputWithoutEnergy { get; init; } = 0.5;

    /// <summary>Réserve de combustible de chaque bâtiment, en jours de chauffage normal.</summary>
    public double FuelReserveDays { get; init; } = 120;

    /// <summary>Impôt d'État : part de sa richesse que chaque groupe verse chaque jour au trésor.</summary>
    public double StateTaxRate { get; init; } = 0.003;

    /// <summary>Impôt communal de départ de chaque commune, versé à sa caisse.</summary>
    public double CommuneTaxRate { get; init; } = 0.002;

    /// <summary>
    /// TVA des entreprises privées et coopératives versée au trésor : une part de leur valeur ajoutée (leurs ventes moins
    /// les matières qu'elles ont achetées).
    /// </summary>
    public double VatStateRate { get; init; }

    /// <summary>TVA versée à la caisse de la commune de l'entreprise.</summary>
    public double VatCommuneRate { get; init; }

    /// <summary>La TVA en tout : les prix l'incluent.</summary>
    public double VatRate => VatStateRate + VatCommuneRate;

    /// <summary>
    /// Part perdue en route dans les échanges entre communes, à la distance de référence (ou sans position sur la carte).
    /// </summary>
    public double TransportLoss { get; init; } = 0.2;

    /// <summary>
    /// Distance de référence du transport, en km : la perte y vaut <see cref="TransportLoss"/>. La perte suit
    /// TransportLoss × (0,5 + 0,5 × distance ÷ cette distance), bornée par <see cref="TransportLossMin"/> et
    /// <see cref="TransportLossMax"/> (décidé le 10 novembre 2026).
    /// </summary>
    public double TransportReferenceDistance { get; init; } = 50;

    /// <summary>Perte de transport minimale entre deux communes distinctes.</summary>
    public double TransportLossMin { get; init; } = 0.05;

    /// <summary>Perte de transport maximale entre deux communes, même très éloignées.</summary>
    public double TransportLossMax { get; init; } = 0.6;

    /// <summary>Part de la pollution des autres communes qu'une commune reçoit, au contact (distance nulle).</summary>
    public double DistantPollutionShare { get; init; } = 0.1;

    /// <summary>
    /// Distance de dispersion de la pollution, en km : à cette distance, la part reçue est divisée par 2. La part
    /// suit DistantPollutionShare ÷ (1 + (distance ÷ cette distance)²) (décidé le 10 novembre 2026).
    /// </summary>
    public double PollutionDispersionDistance { get; init; } = 25;

    /// <summary>
    /// Distance maximale, en km, entre un bâtiment d'extraction et sa commune : il se pose sur une zone de la carte
    /// qui correspond (forêt, terre fertile, pierre, charbon, fer) à cette distance au plus (décidé le 10 novembre 2026 ; 20 km depuis l'échelle du 12 novembre 2026).
    /// </summary>
    public double ExtractionMaxDistance { get; init; } = 20;

    /// <summary>Ressort d'une commune : le rayon, en km, du territoire qu'elle tient autour d'elle (12 novembre 2026).</summary>
    public double CommuneJurisdictionRadius { get; init; } = 8;

    /// <summary>
    /// Distance minimale, en km, entre une commune qu'on fonde et une autre commune : deux ressorts de commune, pour qu'ils
    /// ne se chevauchent pas (décidé le 12 novembre 2026).
    /// </summary>
    public double CommuneMinDistance { get; init; } = 16;

    /// <summary>
    /// Ressort d'une préfecture : le rayon, en km, de son territoire autour de son chef-lieu ; il s'ajoute aux ressorts
    /// de ses communes (12 novembre 2026).
    /// </summary>
    public double PrefectureJurisdictionRadius { get; init; } = 40;

    // ---- Routes (règle décidée le 10 novembre 2026 ; chiffres validés le 11 novembre 2026, en km depuis le 12) ---------

    /// <summary>Routes : trafic d'un mois entre deux lieux (produits échangés, dans les deux sens) pour ouvrir un sentier.</summary>
    public double RoadTrailTraffic { get; init; } = 1000;

    /// <summary>Routes : trafic d'un mois pour faire d'un sentier un chemin.</summary>
    public double RoadPathTraffic { get; init; } = 4000;

    /// <summary>Routes : trafic d'un mois pour faire d'un chemin une route.</summary>
    public double RoadRoadTraffic { get; init; } = 9000;

    /// <summary>Routes : coût de construction d'un sentier, par km de longueur (comptée avec la montée).</summary>
    public double RoadTrailCost { get; init; } = 20;

    /// <summary>Routes : coût pour faire d'un sentier un chemin, par km.</summary>
    public double RoadPathCost { get; init; } = 100;

    /// <summary>Routes : coût pour faire d'un chemin une route, par km.</summary>
    public double RoadRoadCost { get; init; } = 300;

    /// <summary>Routes : entretien d'un sentier, par km et par an.</summary>
    public double RoadTrailUpkeep { get; init; } = 2;

    /// <summary>Routes : entretien d'un chemin, par km et par an.</summary>
    public double RoadPathUpkeep { get; init; } = 10;

    /// <summary>Routes : entretien d'une route, par km et par an.</summary>
    public double RoadRoadUpkeep { get; init; } = 30;

    /// <summary>Routes : km de longueur comptés en plus pour chaque mètre de montée (coût et entretien) : 0,005, soit 5 m.</summary>
    public double RoadClimbLength { get; init; } = 0.005;

    /// <summary>Routes : part de la perte de transport retirée sur un chemin.</summary>
    public double RoadPathLossCut { get; init; } = 0.25;

    /// <summary>Routes : part de la perte de transport retirée sur une route.</summary>
    public double RoadRoadLossCut { get; init; } = 0.5;

    /// <summary>Transport entre communes : km parcourus par jour sans liaison ou sur un sentier (décidé le 20 novembre 2026 : 20).</summary>
    public double TransportSpeedTrail { get; init; } = 20;

    /// <summary>Transport entre communes : km parcourus par jour sur un chemin (40).</summary>
    public double TransportSpeedPath { get; init; } = 40;

    /// <summary>Transport entre communes : km parcourus par jour sur une route (80).</summary>
    public double TransportSpeedRoad { get; init; } = 80;

    /// <summary>
    /// Capacité d'envoi par jour vers les autres communes d'un bâtiment logistique de niveau 1 (grenier, entrepôt, silo ;
    /// décidé le 20 novembre 2026 : 500 unités).
    /// </summary>
    public double ShippingCapacityLevel1 { get; init; } = 500;

    /// <summary>Capacité d'envoi par jour d'un bâtiment logistique de niveau 2 ou plus (dépôt logistique : 2 000 unités).</summary>
    public double ShippingCapacityLevel2 { get; init; } = 2000;

    /// <summary>Taille d'un bâtiment (décidé le 20 novembre 2026) : emplois ou places au moins, quelle que soit la taille (10).</summary>
    public int BuildingMinUnits { get; init; } = 10;

    /// <summary>Matériaux au moins pour un chantier, quelle que soit la taille du bâtiment (20).</summary>
    public double BuildingMinMaterials { get; init; } = 20;

    /// <summary>Rayon autour d'un bâtiment d'extraction dans lequel on compte la surface de sa zone, en km (5).</summary>
    public double ZoneRadius { get; init; } = 5;

    /// <summary>Emplois par km² qu'une terre fertile supporte dans ce rayon (20).</summary>
    public double ZoneDensityFertile { get; init; } = 20;

    /// <summary>Emplois par km² qu'une forêt supporte (20).</summary>
    public double ZoneDensityForest { get; init; } = 20;

    /// <summary>Emplois par km² qu'un gisement de pierre, de charbon ou de fer supporte (25).</summary>
    public double ZoneDensityDeposit { get; init; } = 25;

    /// <summary>Capacité d'envoi par jour qu'ajoute une charrette gardée par une commune (décidé le 20 novembre 2026 : 50).</summary>
    public double CartCapacity { get; init; } = 50;

    /// <summary>Tracé : pente la plus forte permise, en % (mètres de dénivelé pour 100 m).</summary>
    public double RoadMaxSlope { get; init; } = 12;

    /// <summary>Tracé : un pas coûte sa longueur × (1 + ce poids × la pente en %).</summary>
    public double RoadSlopeWeight { get; init; } = 0.1;

    /// <summary>Tracé : un pas sur une voie déjà tracée coûte cette part de son prix (le réseau se ramifie).</summary>
    public double RoadReuseFactor { get; init; } = 0.5;

    /// <summary>Tracé : taille d'une case de la grille du tracé, en km.</summary>
    public double RoadGridCell { get; init; } = 0.8;

    /// <summary>
    /// Dette de légitimité permise par pôle pour voter une loi à crédit (décidé le 23 novembre 2026, proposition) : le
    /// stock d'un pôle peut descendre jusqu'à −cette valeur.
    /// </summary>
    public double LegitimacyMaxDebt { get; init; } = 100;

    /// <summary>
    /// Usure de la légitimité (décidé le 23 novembre 2026, proposition) : part du stock positif de chaque pôle perdue en un
    /// an, retirée un douzième chaque mois. Une dette ne s'use pas.
    /// </summary>
    public double LegitimacyYearlyErosion { get; init; } = 0.1;

    /// <summary>
    /// Population de la nation à laquelle les coûts fixes valent leur coût de base (fonder une commune, rattacher) :
    /// au-delà, ils grandissent avec elle (décidé le 23 novembre 2026, proposition). Une préfecture se mesure aux
    /// habitants qu'elle regroupe.
    /// </summary>
    public double LegitimacyCostReferencePopulation { get; init; } = 5000;

    /// <summary>
    /// Emplois d'un bâtiment pour lesquels vendre ou nationaliser vaut son coût de base : le coût suit les emplois du
    /// bâtiment, au moins le coût minimal d'une brique (décidé le 23 novembre 2026, proposition).
    /// </summary>
    public double LegitimacyCostReferenceJobs { get; init; } = 200;

    /// <summary>Vendre un bâtiment public au privé : légitimité LI, pour un bâtiment de référence.</summary>
    public double SaleLegitimacyCost { get; init; } = 10;

    /// <summary>Racheter une entreprise en difficulté : légitimité CO, pour un bâtiment de référence.</summary>
    public double NationalizationStrugglingLegitimacyCost { get; init; } = 5;

    /// <summary>Racheter une entreprise qui marche : légitimité CO, pour un bâtiment de référence.</summary>
    public double NationalizationThrivingLegitimacyCost { get; init; } = 25;

    /// <summary>Exproprier : légitimité CO, pour un bâtiment de référence.</summary>
    public double ExpropriationLegitimacyCost { get; init; } = 60;

    /// <summary>Pollution par habitant et par jour au-delà de laquelle une commune est polluée.</summary>
    public double PollutedThreshold { get; init; } = 0.1;

    // Passages entre classes (ClassTransitions ; feuille Passages du tableur). Tous des propositions.

    /// <summary>Points d'éducation qui font monter un membre d'un rang (une année d'études : 365).</summary>
    public double TransitionEducationPointsPerPromotion { get; init; } = 365;

    /// <summary>Part des membres d'une classe qui peuvent monter de rang chaque année.</summary>
    public double TransitionMaxYearlyPromotionShare { get; init; } = 0.02;

    /// <summary>Part de chômeurs au-delà de laquelle le chômage pèse sur une classe.</summary>
    public double TransitionUnemploymentThreshold { get; init; } = 0.2;

    /// <summary>Part des chômeurs d'une classe touchée qui descendent d'un rang ou basculent chaque saison.</summary>
    public double TransitionUnemployedShare { get; init; } = 0.1;

    /// <summary>Part de chômeurs sous laquelle une classe est en plein emploi.</summary>
    public double TransitionFullEmploymentThreshold { get; init; } = 0.05;

    /// <summary>Part des membres d'une classe ségrégationniste en plein emploi qui redeviennent égalitaristes chaque saison.</summary>
    public double TransitionFullEmploymentShare { get; init; } = 0.05;

    /// <summary>Part des besoins non couverte faute d'argent au-delà de laquelle une classe qui travaille manque d'argent.</summary>
    public double TransitionMoneyShortfallThreshold { get; init; } = 0.2;

    /// <summary>Part des revenus de la saison prélevée au-delà de laquelle un prélèvement est lourd.</summary>
    public double TransitionHeavyTaxThreshold { get; init; } = 0.3;

    /// <summary>Besoins couverts au-delà desquels une classe est comblée.</summary>
    public double TransitionFullySatisfiedThreshold { get; init; } = 0.99;

    /// <summary>Part des membres qui basculent chaque saison sur l'axe hiérarchie (manque d'argent, prélèvements).</summary>
    public double TransitionHierarchyShare { get; init; } = 0.05;

    /// <summary>Part des membres concernés qui basculent chaque saison sur l'axe propriété.</summary>
    public double TransitionPropertyShare { get; init; } = 0.05;

    /// <summary>Part des besoins non couverte faute de produits ou de places au-delà de laquelle une classe manque de produits.</summary>
    public double TransitionSupplyShortageThreshold { get; init; } = 0.2;

    /// <summary>Part des membres qui basculent chaque saison sur l'axe production.</summary>
    public double TransitionProductionShare { get; init; } = 0.05;

    /// <summary>Adhésion à partir de laquelle une classe en crise fait confiance aux institutions (solidarité).</summary>
    public double TransitionTrustThreshold { get; init; } = 0.5;

    /// <summary>Part des membres en crise qui vont vers l'égalitarisme quand la crise divise (confiance et déclassement mêlés).</summary>
    public double TransitionCrisisSplit { get; init; } = 0.5;

    /// <summary>Part des élèves de la saison qui rejoignent un pôle de La Promesse (mode avec promesse).</summary>
    public double TransitionPromiseSchoolShare { get; init; } = 0.02;

    /// <summary>Dotation d'une nouvelle commune, en jours de salaires de son camp de base.</summary>
    public int FoundingGrantDays { get; init; } = 60;

    /// <summary>Légitimité que coûte un camp de base (fonder une commune), dans le pôle choisi, qui devient sa charte (décidé).</summary>
    public double BaseCampLegitimacyCost { get; init; } = 20;

    /// <summary>
    /// Proposition : réserve de chantier d'une commune à sa fondation (et de la première commune au départ), en
    /// matériaux de construction. Hors du stockage, elle ne se perd ni ne se vend : elle sert à ses premiers chantiers.
    /// </summary>
    public double FoundingMaterials { get; init; } = 1000;

    /// <summary>Au changement de mois, une entreprise licencie si ses invendus dépassent ce nombre de jours de production.</summary>
    public double LayoffUnsoldDays { get; init; } = 30;

    /// <summary>Elle réembauche si ses invendus sont sous ce nombre de jours de production (et ses salaires tous payés).</summary>
    public double HireUnsoldDays { get; init; } = 10;

    /// <summary>Part de sa capacité qu'une entreprise licencie ou réembauche chaque mois, selon son statut.</summary>
    public Dictionary<Ownership, double> JobChangePerMonth { get; init; } = new()
    {
        [Ownership.Public] = 0.02,
        [Ownership.Cooperative] = 0.05,
        [Ownership.Private] = 0.15,
    };

    /// <summary>Baisse des salaires d'une SCOP en difficulté, par mois, avant de licencier.</summary>
    public double CooperativeWageCutStep { get; init; } = 0.05;

    /// <summary>Baisse maximale des salaires d'une SCOP ; au-delà, elle licencie.</summary>
    public double CooperativeWageCutMax { get; init; } = 0.2;

    /// <summary>
    /// Une brique de loi coûte ce nombre de jours de légitimité au taux du coût (<see cref="LawCostPerMemberPerDay"/>)
    /// pour tous les membres qu'elle touche.
    /// </summary>
    public double LawPaybackDays { get; init; } = 30;

    /// <summary>
    /// Légitimité produite par jour et par membre qui accepte une brique de loi (décidé le 23 novembre 2026, proposition :
    /// 1 point pour 10 000 membres ; avant, 1 pour 1 000, on en gagnait beaucoup trop).
    /// </summary>
    public double LegitimacyPerMemberPerDay { get; init; } = 0.0001;

    /// <summary>
    /// Taux qui sert à calculer le coût d'une loi (membres × ce taux × jours de remboursement) : 0,001, inchangé quand la
    /// production a été divisée par 10 ; une loi acceptée de tous se rembourse donc en 300 jours.
    /// </summary>
    public double LawCostPerMemberPerDay { get; init; } = 0.001;

    /// <summary>Poids des membres du niveau d'une loi que le sujet d'une brique ne vise pas (ceux qu'il vise comptent pour 1).</summary>
    public double UntargetedMemberWeight { get; init; } = 0.5;

    /// <summary>Coût d'une brique verrouillée, par rapport à la même brique sans verrou.</summary>
    public double LockedBrickCostFactor { get; init; } = 1.5;

    /// <summary>Coût d'une loi d'un domaine régalien votée sous l'État, par rapport à la même loi votée par l'État.</summary>
    public double RegalianCostFactor { get; init; } = 2;

    /// <summary>Coût minimal d'une brique qui change, en points de légitimité.</summary>
    public double MinimumBrickCost { get; init; } = 1;

    /// <summary>Extension : population comptée qu'une entité politique gère sans perte (charge 1).</summary>
    public double ExtensionPopulationCapacity { get; init; } = 20_000;

    /// <summary>Extension : bâtiments publics qu'une entité gère en direct sans perte (charge 1).</summary>
    public double ExtensionBuildingCapacity { get; init; } = 40;

    /// <summary>Extension : poids d'un bâtiment autonome (SCOP, privé) par rapport à un bâtiment public géré en direct.</summary>
    public double ExtensionAutonomousWeight { get; init; } = 0.25;

    /// <summary>Extension : part de la charge (hors population) d'une entité contenue qui s'ajoute à celle qui la contient.</summary>
    public double ExtensionChildWeight { get; init; } = 0.1;

    /// <summary>Extension : part de la population comptée par une entité contenue que compte une préfecture ou l'État.</summary>
    public double ExtensionCountedShare { get; init; } = 0.25;

    /// <summary>
    /// Extension : km entre une entité contenue et le chef-lieu (la capitale pour l'État) pour 1 point de charge
    /// (décidé le 10 novembre 2026 : 2 000 unités de carte, soit 200 km ; à 50 km, une seule commune à 80 km faisait
    /// s'effondrer le trésor).
    /// </summary>
    public double ExtensionDistancePerLoad { get; init; } = 200;

    /// <summary>Bureaucratie : perte d'efficacité par préfecture traversée entre l'entité qui décide et la commune.</summary>
    public double BureaucracyLossPerLevel { get; init; } = 0.1;

    /// <summary>Préfecture : emplois ajoutés au bâtiment de la préfecture pour chaque entité qu'elle contient.</summary>
    public int PrefectureJobsPerEntity { get; init; } = 5;

    /// <summary>Préfecture : légitimité payée pour la fonder, dans un pôle choisi par le joueur.</summary>
    public double PrefectureLegitimacyCost { get; init; } = 30;

    /// <summary>Préfecture : légitimité payée pour rattacher une entité ailleurs (gratuit pour celles choisies à la fondation).</summary>
    public double AttachLegitimacyCost { get; init; } = 5;

    /// <summary>Préfecture : nombre minimal d'entités (communes ou préfectures) à regrouper pour la fonder.</summary>
    public int PrefectureMinimumMembers { get; init; } = 2;
}

/// <summary>Les réglages d'équilibrage en vigueur dans la partie.</summary>
public static class Balance
{
    public static BalanceSettings Current { get; set; } = new();
}
