namespace Demos.Domain.Population;

/// <summary>
/// Groupe d'individus partageant un rang, une idéologie et les mêmes besoins.
/// </summary>
public sealed class SocialClass(string id, string name, Rank rank, Ideology ideology, long population)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public Rank Rank { get; } = rank;
    public Ideology Ideology { get; set; } = ideology;
    public long Population { get; private set; } = population;
    public double Wealth { get; set; }

    /// <summary>Soutien au régime, de 0 à 100.</summary>
    public double Approval { get; private set; } = 50;

    /// <summary>Part des besoins couverte au dernier cycle, de 0 à 1. Fixée par la consommation.</summary>
    public double NeedsSatisfaction { get; set; } = 0.5;

    /// <summary>
    /// L'approbation se rapproche de 10 % de sa cible à chaque cycle mensuel :
    /// cible = 50 × satisfaction + 50 × accord idéologique.
    /// </summary>
    public void UpdateApproval(double ideologicalAgreement)
    {
        var target = 50 * NeedsSatisfaction + 50 * ideologicalAgreement;
        Approval += 0.1 * (target - Approval);
    }

    /// <summary>
    /// Croissance mensuelle : +0,2 % à 100 % de satisfaction, nulle à 50 %, -0,2 % à 0 %.
    /// </summary>
    public void Grow()
    {
        var rate = 0.004 * (NeedsSatisfaction - 0.5);
        Population = (long)Math.Round(Population * (1 + rate));
    }

    /// <summary>Poids politique = population × modificateur (multiplicateur de rang, ajusté par les lois).</summary>
    public double PoliticalWeight(double rankMultiplier) => Population * rankMultiplier;
}
