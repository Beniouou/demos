namespace Demos.Carte;

/// <summary>
/// Paramètres réglables pour la génération de la topographie de la carte :
/// altitude des massifs, thalweg fluvial, pente générale et pas des courbes de niveau.
/// </summary>
public sealed record TerrainParameters
{
    /// <summary>Largeur du monde en unités de carte (100 m) (défaut 2400.0 - échelle Petit Pays).</summary>
    public double WorldWidth { get; init; } = 2400.0;

    /// <summary>Hauteur du monde en unités de carte (100 m) (défaut 1500.0 - échelle Petit Pays).</summary>
    public double WorldHeight { get; init; } = 1500.0;

    /// <summary>Altitude maximale de référence du sommet principal (en mètres, défaut 700 m).</summary>
    public double MountainMaxAltitude { get; init; } = 700.0;

    /// <summary>Profondeur maximale du creusement de la vallée fluviale (en mètres, défaut 60 m).</summary>
    public double RiverValleyDepth { get; init; } = 60.0;

    /// <summary>Intervalle vertical entre deux isohypses consécutives (en mètres, défaut 25 m).</summary>
    public double ContourInterval { get; init; } = 25.0;

    /// <summary>Facteur de pente générale Nord-Est vers Sud-Ouest (défaut 1.0).</summary>
    public double GradientFactor { get; init; } = 1.0;

    /// <summary>Amplitude de la dépression maritime au Sud-Ouest (en mètres, défaut 140 m).</summary>
    public double SeaDrop { get; init; } = 140.0;

    /// <summary>Altitude maximale de la colline Sud-Est (en mètres, défaut 120 m).</summary>
    public double HillAltitude { get; init; } = 120.0;

    public static TerrainParameters Default => new();
}
