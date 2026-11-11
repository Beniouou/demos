using Godot;
using System;

namespace Demos.Game.Map.Layers;

/// <summary>Ce qui peut changer et obliger un calque à se redessiner.</summary>
[Flags]
public enum MapChange
{
    None = 0,
    /// <summary>La caméra : zoom, déplacement, taille de la carte à l'écran.</summary>
    Camera = 1,
    /// <summary>Les filtres d'information cochés dans le menu Carte.</summary>
    Filters = 2,
    /// <summary>La partie : communes, habitants, préfectures, capitale, bâtiments posés, chantiers.</summary>
    Game = 4,
}

/// <summary>
/// Un calque de la carte : il dit ce qu'il dessine (<see cref="Content"/>) et ce qui l'oblige à se redessiner
/// (<see cref="RedrawOn"/>), et garde ses propres calculs. Ses écritures (noms, cotes, cartouches) ne sont pas dessinées
/// avec lui mais par <see cref="DrawLabels"/>, que le calque des étiquettes appelle au-dessus de tout.
/// </summary>
public abstract class MapLayer
{
    /// <summary>Le nom du calque, pour le joueur.</summary>
    public abstract string Name { get; }

    /// <summary>Ce que le calque dessine.</summary>
    public abstract string Content { get; }

    /// <summary>Ce qui oblige le calque à se redessiner.</summary>
    public abstract MapChange RedrawOn { get; }

    /// <summary>Dessine le calque, sans ses écritures.</summary>
    public abstract void Draw(MapDrawContext c);

    /// <summary>Dessine les écritures du calque (appelé par le calque des étiquettes, au-dessus de tous les calques).</summary>
    public virtual void DrawLabels(MapDrawContext c)
    {
    }

    /// <summary>
    /// Où le calque est rendu : avec le fond fixe (gardé en cache, refait seulement quand il change), ou dans la scène
    /// animée, sous le fond (l'eau) ou au-dessus (fumées, nuages), refaite à chaque image par la carte graphique.
    /// </summary>
    public virtual MapLayerPlace Place => MapLayerPlace.Fixed;

    /// <summary>
    /// Crée une fois les nœuds propres du calque (shader, MultiMesh) sous son nœud de dessin <paramref name="node"/> ;
    /// le calque peut aussi donner un matériau à ce nœud.
    /// </summary>
    public virtual void CreateNodes(Node2D node, MapDrawContext c)
    {
    }

    /// <summary>Met à jour ces nœuds (caméra, filtres, partie) quand le calque doit se redessiner.</summary>
    public virtual void UpdateNodes(MapDrawContext c)
    {
    }
}

/// <summary>Où un calque est rendu (voir <see cref="MapLayer.Place"/>).</summary>
public enum MapLayerPlace
{
    /// <summary>Dans le fond fixe, gardé en cache.</summary>
    Fixed,
    /// <summary>Dans la scène animée, sous le fond fixe (l'eau).</summary>
    AnimatedBelow,
    /// <summary>Dans la scène animée, au-dessus du fond fixe (fumées, nuages).</summary>
    AnimatedAbove,
}
