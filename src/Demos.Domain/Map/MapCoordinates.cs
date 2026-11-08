namespace Demos.Domain.Map;

/// <summary>
/// Coordonnées entières sur la grille topographique.
/// </summary>
public readonly record struct MapCoordinates(int X, int Y)
{
    public double EuclideanDistanceTo(MapCoordinates other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public int ManhattanDistanceTo(MapCoordinates other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public IEnumerable<MapCoordinates> OrthogonalNeighbors()
    {
        yield return new(X + 1, Y);
        yield return new(X - 1, Y);
        yield return new(X, Y + 1);
        yield return new(X, Y - 1);
    }

    public IEnumerable<MapCoordinates> AllNeighbors()
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx != 0 || dy != 0)
                    yield return new(X + dx, Y + dy);
            }
        }
    }

    public override string ToString() => $"({X}, {Y})";
}
