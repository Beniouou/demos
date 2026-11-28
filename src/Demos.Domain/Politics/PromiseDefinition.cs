using Demos.Domain.Population;

namespace Demos.Domain.Politics;

/// <summary>
/// Une Promesse des exilés (décidé le 21 novembre 2026, document des règles, Thème : l'exil et la promesse) : un pôle
/// sur 1, 2 ou 3 axes, et son nom. Les 26 combinaisons possibles ont chacune un nom (game/data/promesses.json, feuille
/// Promesses du tableur), et la première phrase de son serment.
/// </summary>
/// <param name="Id">Les codes des pôles en minuscules, dans l'ordre des axes, séparés par « - » (« eg-ec-co »).</param>
/// <param name="Poles">Les codes des pôles visés (« EG », « EC », « CO »), un par axe choisi.</param>
/// <param name="Opening">La suite de la première phrase du serment, après « Nous, peuple de X, » (décidé le 27 novembre
/// 2026) ; elle suit les axes, pas le nom, si le joueur renomme sa Promesse.</param>
public sealed record PromiseDefinition(string Id, string Name, IReadOnlyList<string> Poles, string? Opening = null)
{
    /// <summary>Les pôles visés, dans l'ordre des axes.</summary>
    public IReadOnlyList<Orientation> Orientations =>
        Poles.Select(Orientation.FromCode).OrderBy(p => p.Axis).ToList();

    /// <summary>Le pôle visé sur un axe, ou null si l'axe est libre.</summary>
    public Orientation? PoleOn(IdeologicalAxis axis) =>
        Orientations.Where(p => p.Axis == axis).Select(p => (Orientation?)p).FirstOrDefault();

    /// <summary>L'identifiant d'une combinaison de pôles.</summary>
    public static string IdOf(IEnumerable<Orientation> poles) =>
        string.Join("-", poles.OrderBy(p => p.Axis).Select(p => p.Code.ToLowerInvariant()));

    /// <summary>Toutes les combinaisons de 1 à 3 axes (26), des plus courtes aux plus longues.</summary>
    public static IEnumerable<IReadOnlyList<Orientation>> AllCombinations()
    {
        var axes = Enum.GetValues<IdeologicalAxis>();
        IEnumerable<IReadOnlyList<Orientation>> combinations = [[]];
        foreach (var axis in axes)
            combinations = combinations.SelectMany(c => new IReadOnlyList<Orientation>[]
            {
                c,
                [.. c, new Orientation(axis, Pole.A)],
                [.. c, new Orientation(axis, Pole.B)],
            });
        return combinations.Where(c => c.Count > 0).OrderBy(c => c.Count).ToList();
    }
}
