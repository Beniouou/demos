using System.Numerics;

namespace Demos.Domain.Population;

/// <summary>Axes idéologiques. Ajouter un axe double le nombre d'idéologies possibles.</summary>
public enum IdeologicalAxis
{
    /// <summary>Pôle A = EG égalitariste, pôle B = SE ségrégationniste.</summary>
    Hierarchy,

    /// <summary>Pôle A = PO positiviste, pôle B = EC écologiste.</summary>
    Production,

    /// <summary>Pôle A = PR protectionniste, pôle B = LI libéraux.</summary>
    Property,
}

/// <summary>Chaque axe est binaire : une classe est soit au pôle A (-1), soit au pôle B (+1).</summary>
public enum Pole
{
    A = -1,
    B = 1,
}

/// <summary>
/// Pôle choisi sur chaque axe idéologique, stocké comme un masque de bits (bit à 1 = pôle B).
/// Deux idéologies identiques sont égales, ce qui permet de fusionner les classes.
/// </summary>
public readonly record struct Ideology
{
    public static readonly int AxisCount = Enum.GetValues<IdeologicalAxis>().Length;

    private readonly int _poleBMask;

    private Ideology(int poleBMask) => _poleBMask = poleBMask;

    public Ideology(Pole hierarchy, Pole production, Pole property)
        : this(MaskOf(IdeologicalAxis.Hierarchy, hierarchy)
            | MaskOf(IdeologicalAxis.Production, production)
            | MaskOf(IdeologicalAxis.Property, property))
    {
    }

    public Pole this[IdeologicalAxis axis] => (_poleBMask & (1 << (int)axis)) != 0 ? Pole.B : Pole.A;

    /// <summary>La même idéologie, avec un autre pôle sur un axe (utile pour les scissions).</summary>
    public Ideology With(IdeologicalAxis axis, Pole pole) =>
        new((_poleBMask & ~(1 << (int)axis)) | MaskOf(axis, pole));

    /// <summary>Part des axes où les deux idéologies sont au même pôle, de 0 à 1.</summary>
    public double AgreementWith(Ideology other)
    {
        var differentAxes = BitOperations.PopCount((uint)(_poleBMask ^ other._poleBMask));
        return 1 - (double)differentAxes / AxisCount;
    }

    /// <summary>Nom de l'idéologie selon le pôle de chaque axe.</summary>
    public string Name => (this[IdeologicalAxis.Hierarchy], this[IdeologicalAxis.Production], this[IdeologicalAxis.Property]) switch
    {
        (Pole.A, Pole.A, Pole.A) => "Communiste",
        (Pole.A, Pole.A, Pole.B) => "Libertaire",
        (Pole.A, Pole.B, Pole.A) => "Phalanstérien",
        (Pole.A, Pole.B, Pole.B) => "Communautariste",
        (Pole.B, Pole.A, Pole.A) => "Féodaliste",
        (Pole.B, Pole.A, Pole.B) => "Capitaliste",
        (Pole.B, Pole.B, Pole.A) => "Caste",
        _ => "Primitiviste",
    };

    private static int MaskOf(IdeologicalAxis axis, Pole pole) => pole == Pole.B ? 1 << (int)axis : 0;
}
