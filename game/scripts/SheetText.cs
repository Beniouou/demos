using System.Collections.Generic;
using System.Linq;
using Demos.Application;
using Demos.Domain.Politics;

namespace Demos.Game;

/// <summary>
/// Mise en forme des fiches (texte BBCode d'un RichTextLabel) : titres, texte discret, pastilles des pôles, et les mots
/// des lois (sujet d'une brique, valeur d'un curseur, sorte de changement).
/// </summary>
public static class SheetText
{
    public static string Heading(string text) => $"[font_size={UiTheme.TitleSize}]{Escape(text)}[/font_size]\n\n";

    public static string Section(string text) => $"\n[color=#{UiTheme.Muted.ToHtml(false)}]{Escape(text.ToUpperInvariant())}[/color]\n";

    public static string Muted(string text) => $"[color=#{UiTheme.Muted.ToHtml(false)}]{Escape(text)}[/color]";

    /// <summary>Un texte discret avec une bulle d'aide au survol.</summary>
    public static string Muted(string text, string hint) => Hint(Muted(text), hint);

    /// <summary>Un titre de section avec une bulle d'aide au survol.</summary>
    public static string Section(string text, string hint) =>
        $"\n{Hint($"[color=#{UiTheme.Muted.ToHtml(false)}]{Escape(text.ToUpperInvariant())}[/color]", hint)}\n";

    /// <summary>
    /// Une bulle d'aide au survol d'un texte BBCode déjà mis en forme. Le texte de la bulle ne peut contenir ni
    /// guillemet ni crochet (ils fermeraient la balise) : ils sont remplacés.
    /// </summary>
    public static string Hint(string bbcode, string hint) =>
        $"[hint=\"{hint.Replace("\"", "'").Replace("[", "(").Replace("]", ")")}\"]{bbcode}[/hint]";

    /// <summary>Le carré de couleur du propriétaire d'un bâtiment (État, SCOP, privé), avec son nom au survol.</summary>
    public static string OwnerSquare(Ownership ownership) =>
        Hint($"[color=#{UiTheme.OwnershipColor(ownership).ToHtml(false)}]■[/color]", ownership switch
        {
            Ownership.Public => "Bâtiment d'État",
            Ownership.Cooperative => "SCOP (coopérative)",
            _ => "Entreprise privée",
        });

    /// <summary>
    /// Le nom affiché d'une structure ; pour un bâtiment, précédé du carré de son propriétaire : « ■ Atelier (Farine) ».
    /// </summary>
    public static string Structure(GameView view, string structureId) =>
        view.OwnershipOf(structureId) is { } ownership
            ? $"{OwnerSquare(ownership)} {Escape(view.NameOf(structureId))}"
            : Escape(view.NameOf(structureId));

    /// <summary>Le logo du pôle (sinon un carré de sa couleur), son nom au survol.</summary>
    public static string Chip(Orientation pole) =>
        Hint(IconRegistry.PoleInline(pole) ?? $"[color=#{UiTheme.PoleColor(pole).ToHtml(false)}]■[/color]",
            $"{pole.Code} : pôle {WorldText.PoleName(pole)}");

    /// <summary>Les logos de plusieurs pôles, suivis de leurs codes : « ■■ EG CO » ; le nom de chaque pôle au survol.</summary>
    public static string Chips(IEnumerable<Orientation> poles)
    {
        var list = poles.Distinct().ToList();
        return list.Count == 0
            ? ""
            : $"{string.Concat(list.Select(Chip))} {string.Join(" ", list.Select(p => Hint(p.Code, $"{p.Code} : pôle {WorldText.PoleName(p)}")))}";
    }

    // Un crochet ouvrant serait lu comme une balise BBCode.
    public static string Escape(string text) => text.Replace("[", "[lb]");

    /// <summary>À qui ou à quoi s'applique une brique, en mots.</summary>
    public static string Subject(LawSubject subject) => subject.Kind switch
    {
        SubjectKind.Everyone => "tout le monde",
        SubjectKind.Rank => string.Join(", ", subject.Values.Select(RankName)),
        SubjectKind.Ownership => "entreprises " + string.Join(", ", subject.Values.Select(OwnershipName)),
        SubjectKind.BuildingType => "bâtiments : " + string.Join(", ", subject.Values),
        SubjectKind.Category => "catégories : " + string.Join(", ", subject.Values),
        SubjectKind.Need => "produits de besoin : " + string.Join(", ", subject.Values.Select(NeedName)),
        _ => subject.Value ?? "",
    };

    /// <summary>La valeur d'un curseur selon la famille de la brique : « autorisé », « +20 % », « 2,1 ».</summary>
    public static string Value(BrickFamily family, double value) => family switch
    {
        BrickFamily.Permission => value >= 0.5 ? "autorisé" : "interdit",
        BrickFamily.Modifier => $"{value * 100:+0;−0;0} %",
        _ => $"{value:0.##}",
    };

    public static string ChangeKind(LawChangeKind kind) => kind switch
    {
        LawChangeKind.Enact => "Nouvelle loi",
        LawChangeKind.Amend => "Modification",
        _ => "Abrogation",
    };

    private static string RankName(string rank) => rank switch
    {
        "Workers" => "ouvriers",
        "Intermediate" => "professions intermédiaires",
        "Executives" => "cadres",
        _ => rank,
    };

    private static string OwnershipName(string ownership) => ownership switch
    {
        "Public" => "publiques",
        "Private" => "privées",
        "Cooperative" => "coopératives (SCOP)",
        _ => ownership,
    };

    private static string NeedName(string need) => need switch
    {
        "Food" => "alimentaire",
        "Clothing" => "vestimentaire",
        "Furniture" => "mobilier",
        "Leisure" => "loisirs",
        _ => need,
    };
}
