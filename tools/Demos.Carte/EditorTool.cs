namespace Demos.Carte;

/// <summary>
/// Outils interactifs de dessin et d'arpentage disponibles dans l'Éditeur de Carte.
/// </summary>
public enum EditorTool
{
    /// <summary>Mode navigation libre (survol, coordonnées, zoom et panoramique).</summary>
    Navigate,

    /// <summary>Pinceau de terrain unifié : Clic gauche = Surélever, Clic droit = Creuser (devient mer sous 0 m).</summary>
    ElevateBrush,

    /// <summary>Pinceau adoucisseur : Clic gauche = Lisser les pentes (flou gaussien), Clic droit = Aplanir en plateau.</summary>
    SmoothBrush,

    /// <summary>Nommer un lieu / toponymie : poser un point d'intérêt avec nom et logo sans déformer le relief.</summary>
    NamePlace,

    /// <summary>Alias rétrocompatible pour NamePlace.</summary>
    PlacePeak = NamePlace,

    /// <summary>Tracer ou redéfinir le fleuve point par point avec creusement automatique du thalweg.</summary>
    DrawRiver,

    /// <summary>Peindre des biomes et milieux naturels (forêt, terre fertile, marais, plage, falaise, désert, glacier).</summary>
    DrawBiome,

    /// <summary>Alias rétrocompatible pour DrawBiome.</summary>
    DrawForest = DrawBiome,

    /// <summary>Poser un gisement naturel (pierre, charbon, bois, blé, lin, fer...).</summary>
    PlaceResource,

    /// <summary>Poser ou gérer les communes et villages du pays (100+ villages, capitales, bourgs).</summary>
    MoveCommune,

    /// <summary>Alias pour MoveCommune.</summary>
    PlaceCommune = MoveCommune
}
