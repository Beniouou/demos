using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Domain.Population;

/// <summary>
/// Groupe d'individus partageant un rang, une idéologie et les mêmes besoins. Dans le jeu, c'est un groupe local :
/// la partie d'une classe qui vit dans une commune, avec sa propre richesse, ses produits, ses points d'éducation
/// et son bilan de saison. La classe nationale regroupe les groupes locaux de même nom.
/// </summary>
public sealed class SocialClass(string id, string name, Rank rank, Ideology ideology, long population)
{
    // Part des membres qui vivent dans chaque commune ; la somme fait 100 %.
    private readonly Dictionary<string, double> _residence = [];

    // Part des membres qui travaillent dans chaque structure, recalculée chaque jour.
    private readonly Dictionary<string, double> _employment = [];

    public string Id { get; } = id;
    public string Name { get; } = name;
    public Rank Rank { get; } = rank;
    public Ideology Ideology { get; set; } = ideology;
    public long Population { get; private set; } = population;
    public double Wealth { get; set; }

    /// <summary>Produits reçus en paiement en nature, consommés avant d'acheter sur le marché.</summary>
    public Inventory Goods { get; } = new();

    /// <summary>Part des besoins de base couverte, de 0 à 1 : moyenne de se nourrir, s'habiller et se loger.</summary>
    public double NeedsSatisfaction { get; set; } = 0.5;

    /// <summary>Part des membres logés au dernier jour, de 0 à 1.</summary>
    public double HousingCoverage { get; set; } = 1;

    /// <summary>Part des membres restés sans logement au dernier jour faute de places (et non faute d'argent).</summary>
    public double HousingPlaceShortage { get; set; }

    /// <summary>Part des lois qui s'appliquent à la classe qu'elle accepte, de 0 à 1.</summary>
    public double Adhesion { get; set; } = 1;

    /// <summary>Points d'éducation de base (école, lycée), dépensés pour passer d'ouvrier à profession intermédiaire.</summary>
    public double EducationPoints { get; set; }

    /// <summary>Points d'éducation avancée (université), dépensés pour passer de profession intermédiaire à cadre.</summary>
    public double AdvancedEducationPoints { get; set; }

    /// <summary>Richesse par membre relevée à la saison précédente ; null avant la première saison.</summary>
    public double? WealthPerMemberLastSeason { get; set; }

    public double WealthPerMember => Population > 0 ? Wealth / Population : 0;

    // Bilan de la saison en cours, remis à zéro à chaque changement de saison.
    private double _seasonShortfallSum;
    private int _seasonShortfallDays;
    private double _seasonSupplyShortageSum;
    private int _seasonSupplyShortageDays;

    /// <summary>Revenus reçus depuis le début de la saison : salaires (en argent ou en nature), ventes, loyers et frais perçus.</summary>
    public double SeasonIncome { get; private set; }

    /// <summary>Impôts payés depuis le début de la saison.</summary>
    public double SeasonTaxes { get; private set; }

    /// <summary>Part des revenus de la saison prélevée par l'impôt.</summary>
    public double SeasonTaxRate => SeasonIncome > 0 ? SeasonTaxes / SeasonIncome : 0;

    /// <summary>Part moyenne des besoins non couverte faute d'argent depuis le début de la saison, de 0 à 1.</summary>
    public double SeasonMoneyShortfall => _seasonShortfallDays > 0 ? _seasonShortfallSum / _seasonShortfallDays : 0;

    public void ReceiveIncome(double amount)
    {
        Wealth += amount;
        SeasonIncome += amount;
    }

    /// <summary>Un salaire payé en produits compte dans les revenus, sans changer la richesse en argent.</summary>
    public void ReceiveIncomeInKind(double value) => SeasonIncome += value;

    public void PayTax(double amount)
    {
        Wealth -= amount;
        SeasonTaxes += amount;
    }

    /// <summary>Part moyenne des besoins non couverte faute de produits ou de places depuis le début de la saison, de 0 à 1.</summary>
    public double SeasonSupplyShortage => _seasonSupplyShortageDays > 0 ? _seasonSupplyShortageSum / _seasonSupplyShortageDays : 0;

    public void RecordMoneyShortfall(double share)
    {
        _seasonShortfallSum += share;
        _seasonShortfallDays++;
    }

    public void RecordSupplyShortage(double share)
    {
        _seasonSupplyShortageSum += share;
        _seasonSupplyShortageDays++;
    }

    public void StartNewSeason()
    {
        SeasonIncome = 0;
        SeasonTaxes = 0;
        _seasonShortfallSum = 0;
        _seasonShortfallDays = 0;
        _seasonSupplyShortageSum = 0;
        _seasonSupplyShortageDays = 0;
    }

    public IReadOnlyDictionary<string, double> Residence => _residence;

    /// <summary>
    /// Commune du groupe local : la première où il a des membres, ou sa commune d'origine s'il s'est vidé ;
    /// null s'il n'a jamais eu de commune.
    /// </summary>
    public string? CommuneId => _residence.FirstOrDefault(r => r.Value > 0).Key ?? _residence.Keys.FirstOrDefault();

    public void SetResidence(string communeId, double share) => _residence[communeId] = Math.Clamp(share, 0, 1);

    public double MembersLivingIn(string communeId) => Population * _residence.GetValueOrDefault(communeId);

    /// <summary>Déplace une part des membres d'une commune à une autre.</summary>
    public void MoveResidents(string fromCommuneId, string toCommuneId, double share)
    {
        var moved = Math.Min(share, _residence.GetValueOrDefault(fromCommuneId));
        _residence[fromCommuneId] = _residence.GetValueOrDefault(fromCommuneId) - moved;
        _residence[toCommuneId] = _residence.GetValueOrDefault(toCommuneId) + moved;
    }

    public void ClearEmployment() => _employment.Clear();

    public void SetEmployment(string structureId, double members) =>
        _employment[structureId] = Population > 0 ? Math.Clamp(members / Population, 0, 1) : 0;

    public double MembersWorkingIn(string structureId) => Population * _employment.GetValueOrDefault(structureId);

    /// <summary>Membres sans emploi, d'après la dernière répartition des emplois.</summary>
    public double UnemployedMembers => Math.Max(0, Population * (1 - _employment.Values.Sum()));

    /// <summary>
    /// Part des membres soumis aux lois d'une structure : toute la classe pour l'État,
    /// les habitants pour une commune, les travailleurs pour un bâtiment ou un organisme.
    /// Exemple : 50 membres sur 200 vivent dans le village A → 0,25 ; 10 travaillent dans l'usine B → 0,05.
    /// </summary>
    public double ShareSubjectTo(Structure structure) =>
        structure.IsNational ? 1
        : _residence.TryGetValue(structure.Id, out var residence) ? residence
        : _employment.GetValueOrDefault(structure.Id);

    /// <summary>
    /// Calcul saisonnier : +0,6 % à 100 % de satisfaction, nulle à 50 %, -0,6 % à 0 %.
    /// </summary>
    public void Grow()
    {
        var rate = 0.012 * (NeedsSatisfaction - 0.5);
        Population = (long)Math.Round(Population * (1 + rate));
    }

    /// <summary>
    /// Fait passer des membres dans une autre classe. Ils emportent leur part de richesse et gardent leur commune :
    /// la répartition entre communes de cette classe ne change pas, celle de la classe d'arrivée est recalculée.
    /// </summary>
    public void TransferMembersTo(SocialClass target, long count)
    {
        count = Math.Min(count, Population);
        if (count <= 0)
            return;

        var share = (double)count / Population;
        var wealth = Wealth * share;
        Wealth -= wealth;
        target.Wealth += wealth;

        var targetResidents = target._residence.ToDictionary(r => r.Key, r => r.Value * target.Population);
        foreach (var (communeId, residence) in _residence)
            targetResidents[communeId] = targetResidents.GetValueOrDefault(communeId) + residence * count;

        Population -= count;
        target.Population += count;
        foreach (var (communeId, residents) in targetResidents)
            target._residence[communeId] = residents / target.Population;
    }

    /// <summary>
    /// Fait passer dans une autre classe des membres qui vivent dans une commune précise (là où la condition du
    /// passage est vraie). Ils emportent leur part de richesse et restent dans cette commune, sauf si une commune
    /// d'arrivée est donnée (migration) : ils y emportent alors aussi leur part de points d'éducation.
    /// </summary>
    public void TransferMembersTo(SocialClass target, long count, string fromCommuneId, string? toCommuneId = null)
    {
        count = Math.Min(count, (long)MembersLivingIn(fromCommuneId));
        if (count <= 0)
            return;

        var share = (double)count / Population;
        var wealth = Wealth * share;
        Wealth -= wealth;
        target.Wealth += wealth;

        if (toCommuneId is not null)
        {
            var education = EducationPoints * share;
            var advanced = AdvancedEducationPoints * share;
            EducationPoints -= education;
            AdvancedEducationPoints -= advanced;
            target.EducationPoints += education;
            target.AdvancedEducationPoints += advanced;
        }

        var arrival = toCommuneId ?? fromCommuneId;
        var residents = _residence.ToDictionary(r => r.Key, r => r.Value * Population);
        residents[fromCommuneId] -= count;
        var targetResidents = target._residence.ToDictionary(r => r.Key, r => r.Value * target.Population);
        targetResidents[arrival] = targetResidents.GetValueOrDefault(arrival) + count;

        Population -= count;
        target.Population += count;
        foreach (var (communeId, members) in residents)
            _residence[communeId] = Population > 0 ? members / Population : 0;
        foreach (var (communeId, members) in targetResidents)
            target._residence[communeId] = members / target.Population;
    }

    /// <summary>Poids politique = population × modificateur (multiplicateur de rang, ajusté par les lois).</summary>
    public double PoliticalWeight(double rankMultiplier) => Population * rankMultiplier;
}
