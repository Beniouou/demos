using Demos.Domain.Politics;

namespace Demos.Domain.Population;

/// <summary>
/// Groupe d'individus partageant un rang, une idéologie et les mêmes besoins.
/// </summary>
public sealed class SocialClass(string id, string name, Rank rank, Ideology ideology, long population)
{
    // Part des membres soumis à chaque structure locale (où ils vivent ou travaillent), de 0 à 1.
    private readonly Dictionary<string, double> _sharesByStructure = [];

    public string Id { get; } = id;
    public string Name { get; } = name;
    public Rank Rank { get; } = rank;
    public Ideology Ideology { get; set; } = ideology;
    public long Population { get; private set; } = population;
    public double Wealth { get; set; }

    /// <summary>Part des lois qui s'appliquent à la classe qu'elle accepte, de 0 à 1.</summary>
    public double Adhesion { get; set; } = 1;

    /// <summary>Part des besoins couverte, de 0 à 1. Fixée par la consommation.</summary>
    public double NeedsSatisfaction { get; set; } = 0.5;

    /// <summary>
    /// Fixe la part des membres soumis à une structure locale. Exemple : 50 membres sur 200
    /// vivent dans le village A → 0,25 ; 10 d'entre eux travaillent dans l'usine B → 0,05.
    /// </summary>
    public void SetShareSubjectTo(string structureId, double share) =>
        _sharesByStructure[structureId] = Math.Clamp(share, 0, 1);

    /// <summary>Part des membres soumis aux lois d'une structure. L'État couvre toute la classe.</summary>
    public double ShareSubjectTo(Structure structure) =>
        structure.IsNational ? 1 : _sharesByStructure.GetValueOrDefault(structure.Id);

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
