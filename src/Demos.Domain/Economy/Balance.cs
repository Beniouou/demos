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

    /// <summary>Part perdue en route dans les échanges entre communes.</summary>
    public double TransportLoss { get; init; } = 0.2;

    /// <summary>Part de la pollution des autres communes qu'une commune reçoit.</summary>
    public double DistantPollutionShare { get; init; } = 0.1;

    /// <summary>Pollution par habitant et par jour au-delà de laquelle une commune est polluée.</summary>
    public double PollutedThreshold { get; init; } = 0.1;

    /// <summary>Dotation d'une nouvelle commune, en jours de salaires de son camp de base.</summary>
    public int FoundingGrantDays { get; init; } = 60;

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
}

/// <summary>Les réglages d'équilibrage en vigueur dans la partie.</summary>
public static class Balance
{
    public static BalanceSettings Current { get; set; } = new();
}
