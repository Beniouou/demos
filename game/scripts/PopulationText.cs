using Demos.Domain.Population;

namespace Demos.Game;

/// <summary>
/// Les mots du menu Population : noms des rangs, et les bulles d'aide qui disent la règle derrière chaque chiffre
/// (croissance, chômage, migrations, passages entre classes, éducation, adhésion). Les nombres viennent des constantes
/// du moteur.
/// </summary>
public static class PopulationText
{
    public static string RankName(Rank rank) => ClassTransitions.RankName(rank).ToLowerInvariant();

    public const string Members =
        "Les membres des groupes locaux de la commune. Ils peuvent différer un peu des habitants, qui comptent les membres "
        + "selon leur commune de résidence.";

    public const string Housing = "Les places de logement des bâtiments de la commune.";

    public const string Jobs = "Les postes pourvus sur les postes ouverts des bâtiments de la commune.";

    public static string Unemployed =>
        "Les membres sans emploi, d'après la dernière répartition des emplois. Chaque saison, "
        + $"{Migration.SeasonalShareOfUnemployed:P0} des chômeurs de chaque rang partent vers la commune qui offre le plus d'emplois libres de leur rang. "
        + $"Au-delà de {ClassTransitions.UnemploymentThreshold:P0} de chômeurs dans un groupe, {ClassTransitions.UnemployedShareAffected:P0} d'entre eux "
        + "descendent d'un rang (sauf les ouvriers) et les égalitaristes deviennent ségrégationnistes. Sous "
        + $"{ClassTransitions.FullEmploymentThreshold:P0} de chômeurs, {ClassTransitions.FullEmploymentShareAffected:P0} des membres d'un groupe "
        + "ségrégationniste redeviennent égalitaristes.";

    public const string Needs =
        "La part des besoins couverte : moyenne de chacun de ses besoins (se nourrir, s'habiller, se meubler, se distraire, "
        + "être administré, être protégé, se loger). Chaque saison, un groupe grandit de 0,6 % à 100 %, reste stable à 50 % "
        + "et diminue de 0,6 % à 0 %.";

    public const string NeedsTable =
        "Pour chaque besoin et chaque rang, la part couverte au dernier jour, en moyenne sur les membres des groupes (vert : "
        + "tout couvert, rouge : rien). Un besoin durable (vêtements, meubles) est couvert à hauteur de ce que les membres "
        + "possèdent par rapport à la possession visée de leur rang.";

    /// <summary>La croissance d'une saison pour une satisfaction des besoins : « +0,3 % », « stable », « −0,4 % ».</summary>
    public static string Growth(double satisfaction)
    {
        var rate = 0.012 * (satisfaction - 0.5);
        return System.Math.Abs(rate) < 0.0005 ? "population stable" : $"population {rate:+0.0 %;−0.0 %}";
    }

    /// <summary>La bulle d'un besoin : ce qui le couvre.</summary>
    public static string NeedHint(string needId) => needId switch
    {
        "Food" => "Pain ou viande, achetés chaque jour.",
        "Clothing" => "Des vêtements possédés, qui s'usent : le groupe rachète l'usure.",
        "Furniture" => "Des meubles ou de la poterie possédés, qui s'usent : le groupe rachète l'usure.",
        "Leisure" => "Alcool ou pièces de théâtre, achetés chaque jour.",
        "Administration" => "Les services administratifs (mairie, préfecture, palais), consommés le jour même.",
        "Security" => "Les services de sécurité (caserne de pompiers, poste de police), consommés le jour même.",
        "Housing" => "Une place de logement ; manquer de places n'est pas manquer d'argent.",
        _ => "",
    };

    public const string Housed = "La part des membres logés au dernier jour.";

    public const string Adhesion =
        "La part des lois qui s'appliquent au groupe qu'il accepte : il juge chaque brique en vigueur selon ses pôles. "
        + "Une brique acceptée produit de la légitimité de son pôle.";

    public const string Wealth = "L'argent du groupe ; les impôts d'État et de commune se prélèvent dessus chaque jour.";

    public static string Rank(Rank rank) => rank switch
    {
        Demos.Domain.Population.Rank.Workers => "Ouvriers : emplois de production.",
        Demos.Domain.Population.Rank.Intermediate => "Professions intermédiaires : emplois d'encadrement (techniciens, contremaîtres, instituteurs…).",
        _ => "Cadres : emplois de direction.",
    };

    public const string Poles =
        "Chaque groupe a un pôle sur chacun des trois axes : hiérarchie (EG / SE), production (PO / EC), propriété (CO / LI). "
        + "Les lois favorisent des pôles ; un groupe accepte les briques de ses pôles.";

    public static string Ideology(string name) =>
        $"{name} : le nom de l'idéologie que forment ses trois pôles. Un groupe local est une classe (rang et idéologie) dans une commune.";

    public static string Education =>
        "Points d'éducation de base (école, lycée), dépensés chaque année pour passer d'ouvrier à profession intermédiaire : "
        + $"{ClassTransitions.EducationPointsPerPromotion:N0} points font monter un membre, au plus {ClassTransitions.MaxYearlyPromotionShare:P0} "
        + "des membres par an. L'éducation avancée (université) fait passer de profession intermédiaire à cadre.";

    public static string MoneyShortfall =>
        "La part moyenne des besoins non couverte faute d'argent depuis le début de la saison. Au-delà de "
        + $"{ClassTransitions.MoneyShortfallThreshold:P0}, un groupe ségrégationniste qui travaille devient égalitariste "
        + $"({ClassTransitions.PropertyShareAffected:P0} de ses membres par saison).";

    public static string SupplyShortage =>
        "La part moyenne des besoins non couverte faute de produits ou de places depuis le début de la saison. Au-delà de "
        + $"{ClassTransitions.SupplyShortageThreshold:P0}, le groupe devient positiviste ({ClassTransitions.ProductionShareAffected:P0} "
        + "de ses membres par saison) ; vivre dans une commune polluée rend écologiste.";

    public static string Taxes =>
        "Les revenus reçus depuis le début de la saison (salaires, ventes, loyers, frais perçus) et les impôts payés. Un groupe "
        + $"comblé (besoins au-delà de {ClassTransitions.FullySatisfiedThreshold:P0}) prélevé de plus de {ClassTransitions.HeavyTaxRateThreshold:P0} "
        + $"de ses revenus devient ségrégationniste ({ClassTransitions.PropertyShareAffected:P0} de ses membres par saison).";

    public const string Pressures =
        "Chaque saison, des membres changent de pôle selon ce qu'ils vivent, groupe local par groupe local (dans chaque commune) : "
        + "le chômage, le manque d'argent ou de produits, les impôts, la pollution, leur lieu de travail et ce qu'ils possèdent. Seules les pressions qui peuvent faire "
        + "changer ce groupe sont montrées ; la jauge est à moitié pleine au seuil, rouge quand il est passé quelque part.";

    public const string HousingShortage = "La part des membres restés sans logement au dernier jour faute de places (et non faute d'argent).";

    public static string Possessions =>
        "Les objets durables (vêtements, meubles, poterie) ne se consomment pas : le groupe en garde un stock, qui s'use chaque "
        + "jour selon la durée d'usage de chaque objet. Le besoin est couvert dans la mesure où ce stock atteint la possession "
        + $"visée de son rang. Chaque jour, le groupe rachète l'usure et comble au plus {Demos.Domain.Economy.Balance.Current.PossessionCatchUp:P1} "
        + "de son manque, dans la limite de son argent.";

    public const string Workplaces = "Les bâtiments et organismes où travaillent ses membres, d'après la dernière répartition des emplois.";

    public const string History =
        "Un relevé chaque jour joué, pris par l'interface pendant la partie (un par mois au-delà d'un an) : il repart de zéro à chaque lancement. Passez la souris sur la courbe pour lire les chiffres d'un jour.";

    /// <summary>Une cellule de tableau BBCode ; l'en-tête est en gris.</summary>
    public static string Cell(string text, bool header = false) =>
        header
            ? $"[cell padding=0,0,16,4][color=#{UiTheme.Muted.ToHtml(false)}]{text}[/color][/cell]"
            : $"[cell padding=0,0,16,2]{text}[/cell]";
}
