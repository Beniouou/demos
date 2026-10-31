namespace Demos.Domain.Population;

/// <summary>
/// Position sur les trois axes idéologiques, chacun de -100 (pôle A) à +100 (pôle B).
/// </summary>
public readonly record struct Ideology
{
    public const double Min = -100;
    public const double Max = 100;

    private static readonly double MaxDistance = Math.Sqrt(3) * (Max - Min);

    public Ideology(double hierarchy, double production, double property)
    {
        Hierarchy = Math.Clamp(hierarchy, Min, Max);
        Production = Math.Clamp(production, Min, Max);
        Property = Math.Clamp(property, Min, Max);
    }

    /// <summary>-100 = EG égalitariste, +100 = SE ségrégationniste.</summary>
    public double Hierarchy { get; }

    /// <summary>-100 = PO positiviste, +100 = EC écologiste.</summary>
    public double Production { get; }

    /// <summary>-100 = PR protectionniste, +100 = LI libéraux.</summary>
    public double Property { get; }

    /// <summary>Nom de l'idéologie selon le pôle de chaque axe.</summary>
    public string Name => (Hierarchy <= 0, Production <= 0, Property <= 0) switch
    {
        (true, true, true) => "Communiste",
        (true, true, false) => "Libertaire",
        (true, false, true) => "Phalanstérien",
        (true, false, false) => "Communautariste",
        (false, true, true) => "Féodaliste",
        (false, true, false) => "Capitaliste",
        (false, false, true) => "Caste",
        (false, false, false) => "Primitiviste",
    };

    /// <summary>Accord idéologique de 0 (opposés) à 1 (identiques).</summary>
    public double AgreementWith(Ideology other)
    {
        var dh = Hierarchy - other.Hierarchy;
        var dp = Production - other.Production;
        var dr = Property - other.Property;
        var distance = Math.Sqrt(dh * dh + dp * dp + dr * dr);
        return 1 - distance / MaxDistance;
    }
}
