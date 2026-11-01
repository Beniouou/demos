using Demos.Domain.Politics;

namespace Demos.Domain.Population;

/// <summary>
/// Groupe d'individus partageant un rang, une idéologie et les mêmes besoins.
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

    /// <summary>Les dirigeants ne prennent pas les postes des bâtiments.</summary>
    public bool TakesJobs => Rank != Rank.Leaders;

    /// <summary>Part des lois qui s'appliquent à la classe qu'elle accepte, de 0 à 1.</summary>
    public double Adhesion { get; set; } = 1;

    /// <summary>Part des besoins couverte au dernier achat, de 0 à 1. Fixée par la consommation.</summary>
    public double NeedsSatisfaction { get; set; } = 0.5;

    public IReadOnlyDictionary<string, double> Residence => _residence;

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

    /// <summary>Poids politique = population × modificateur (multiplicateur de rang, ajusté par les lois).</summary>
    public double PoliticalWeight(double rankMultiplier) => Population * rankMultiplier;
}
