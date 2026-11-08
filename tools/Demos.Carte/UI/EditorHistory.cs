using System;
using System.Collections.Generic;
using Demos.Domain.Map;

namespace Demos.Carte.UI;

/// <summary>
/// Instantané de l'état complet de la carte pour l'historique d'annulation/rétablissement (Undo/Redo).
/// </summary>
public sealed record MapSnapshot(VectorMapDefinition Map, double[,] HeightGrid, ZoneMaskGrid? ZoneGrid);

/// <summary>
/// Gestionnaire d'historique Undo / Redo pour l'éditeur cartographique vectoriel.
/// </summary>
public sealed class EditorHistory
{
    private readonly Stack<MapSnapshot> _undoStack = new();
    private readonly Stack<MapSnapshot> _redoStack = new();

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public void Push(VectorMapDefinition map, Heightmap heightmap, ZoneMaskGrid? zoneGrid)
    {
        if (map is not null && heightmap is not null)
        {
            _undoStack.Push(new MapSnapshot(map, heightmap.CloneGrid(), zoneGrid));
            _redoStack.Clear();
        }
    }

    public bool Undo(
        VectorMapDefinition currentMap,
        Heightmap currentHeightmap,
        ZoneMaskGrid? currentZoneGrid,
        out MapSnapshot previous)
    {
        if (_undoStack.Count > 0)
        {
            _redoStack.Push(new MapSnapshot(currentMap, currentHeightmap.CloneGrid(), currentZoneGrid));
            previous = _undoStack.Pop();
            return true;
        }

        previous = null!;
        return false;
    }

    public bool Redo(
        VectorMapDefinition currentMap,
        Heightmap currentHeightmap,
        ZoneMaskGrid? currentZoneGrid,
        out MapSnapshot next)
    {
        if (_redoStack.Count > 0)
        {
            _undoStack.Push(new MapSnapshot(currentMap, currentHeightmap.CloneGrid(), currentZoneGrid));
            next = _redoStack.Pop();
            return true;
        }

        next = null!;
        return false;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
