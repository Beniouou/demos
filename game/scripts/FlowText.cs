using System.Linq;
using System.Text;
using Demos.Application;

namespace Demos.Game;

/// <summary>
/// Les mots des flux d'un bâtiment (d'où viennent ses matières, où part sa production), partagés par Structure, Unités
/// de production et la Chaîne de production : noms des besoins, détail de chaque bout de flux, fiche en BBCode.
/// </summary>
public static class FlowText
{
    /// <summary>Le nom d'un besoin des habitants.</summary>
    public static string NeedName(string needId) => needId switch
    {
        "Food" => "Se nourrir",
        "Clothing" => "S'habiller",
        "Furniture" => "Se meubler",
        "Housing" => "Se loger",
        "Administration" => "Administration",
        "Security" => "Sécurité",
        "Energy" => "Énergie (bâtiments)",
        "Leisure" => "Se distraire",
        "Comfort" => "Confort",
        _ => needId,
    };

    /// <summary>
    /// Le détail d'un bout de flux. Pour les habitants, leur besoin ; pour les autres communes, ce que la commune a
    /// envoyé de ce produit le dernier mois fini (depuis le début de la partie tant qu'aucun mois n'est fini).
    /// </summary>
    public static string Detail(GameView view, ProductionHistory history, BuildingFlowView flows, FlowEndView end) => end.Kind switch
    {
        FlowKind.Habitants => $"besoin : {NeedName(end.Detail).ToLowerInvariant()}",
        FlowKind.Export when flows.ProductId is { } productId => SentText(view, history, flows.CommuneId, productId),
        _ => end.Detail,
    };

    /// <summary>« 120 le mois dernier (toute la commune) », ou depuis le début s'il n'y a pas encore de mois fini.</summary>
    private static string SentText(GameView view, ProductionHistory history, string communeId, string productId)
    {
        var snapshots = history.MonthStarts;
        if (snapshots.Count >= 2)
        {
            var sent = ProductFigures.Of(snapshots[^1].Flows, productId, communeId).Since(ProductFigures.Of(snapshots[^2].Flows, productId, communeId)).Sent;
            return $"{sent:N0} envoyés le mois dernier (toute la commune)";
        }
        return $"{ProductFigures.Of(view.ProductFlows(), productId, communeId).Sent:N0} envoyés depuis le début (toute la commune)";
    }

    /// <summary>Un bout de flux en BBCode : le carré du propriétaire, le nom, le produit et le détail ; coloré hors de la commune.</summary>
    public static string Line(GameView view, ProductionHistory history, BuildingFlowView flows, FlowEndView end)
    {
        var name = end.StructureId is { } id ? SheetText.Structure(view, id) : SheetText.Escape(end.Name);
        var where = end.Relation switch
        {
            PlaceRelation.OtherCommune => $" [color=#{UiTheme.OtherCommune.ToHtml(false)}](autre commune)[/color]",
            PlaceRelation.OtherPrefecture => $" [color=#{UiTheme.OtherPrefecture.ToHtml(false)}](autre préfecture)[/color]",
            _ => "",
        };
        return $"{name}{where} · {SheetText.Escape(end.ProductName)} · {SheetText.Muted(Detail(view, history, flows, end))}";
    }

    /// <summary>Les deux sections « D'où viennent ses matières » et « Où part sa production », pour une fiche.</summary>
    public static string Sections(GameView view, ProductionHistory history, BuildingFlowView flows)
    {
        var text = new StringBuilder();
        text.Append(SheetText.Section("D'où viennent ses matières",
            "Un bâtiment prend d'abord ce que ses lignes d'approvisionnement lui livrent, puis achète au marché de sa commune, où vendent les producteurs de la commune."));
        text.AppendLine(flows.Sources.Count == 0
            ? SheetText.Muted(flows.HasMaterials ? "Aucune source." : "Aucune matière : il produit à partir de son seul travail.")
            : string.Join("\n", flows.Sources.Select(s => Line(view, history, flows, s))));
        text.Append(SheetText.Section("Où part sa production",
            "Sa production va d'abord à ses lignes d'approvisionnement, puis au marché de sa commune : les bâtiments qui l'utilisent et les habitants l'y achètent, et les échanges entre communes en emportent une part."));
        text.AppendLine(flows.Destinations.Count == 0
            ? SheetText.Muted(flows.ProductId is null ? "Rien : ce bâtiment n'a pas de recette." : "Personne ne l'utilise ici : elle reste en vente.")
            : string.Join("\n", flows.Destinations.Select(d => Line(view, history, flows, d))));
        return text.ToString();
    }
}
