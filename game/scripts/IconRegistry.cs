using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Registre et chargeur centralisé des logos et insignes vectoriels officiels de Projet Démos.
/// Fait le pont entre les identifiants textuels du jeu et les assets SVG, avec mise en cache mémoire.
/// </summary>
public static class IconRegistry
{
    /// <summary>Facteur d'échelle global pour la taille des icônes (+50 % demandé).</summary>
    public const float ScaleMultiplier = 1.5f;

    public static int Scaled(int baseSize) => (int)System.Math.Round(baseSize * ScaleMultiplier);

    private static readonly Dictionary<(string, int), Texture2D> _cache = new();

    // ---- Menus principaux ----
    private static readonly Dictionary<string, string> MenuPaths = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["Carte"] = "res://assets/icons/menus/menu-carte.svg",
        ["Lois"] = "res://assets/icons/menus/menu-lois.svg",
        ["Structure"] = "res://assets/icons/menus/menu-structure.svg",
        ["Population"] = "res://assets/icons/menus/menu-population.svg",
        ["Communes"] = "res://assets/icons/menus/menu-production.svg",
        ["Production"] = "res://assets/icons/menus/menu-production.svg",
        ["Recherche"] = "res://assets/icons/menus/menu-recherche.svg",
        ["Ressources"] = "res://assets/icons/menus/menu-ressources.svg",
        ["Aide"] = "res://assets/icons/menus/menu-aide.svg",
    };

    // ---- Rangs sociaux ----
    private static readonly Dictionary<string, string> RankPaths = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["ouvriers"] = "res://assets/icons/classes/classe-ouvriers.svg",
        ["workers"] = "res://assets/icons/classes/classe-ouvriers.svg",
        ["intermediaires"] = "res://assets/icons/classes/classe-intermediaires.svg",
        ["intermediate"] = "res://assets/icons/classes/classe-intermediaires.svg",
        ["cadres"] = "res://assets/icons/classes/classe-cadres.svg",
        ["executives"] = "res://assets/icons/classes/classe-cadres.svg",
        ["managers"] = "res://assets/icons/classes/classe-cadres.svg",
    };

    // ---- Pôles idéologiques / politiques ----
    private static readonly Dictionary<string, string> PolePaths = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["EG"] = "res://assets/icons/classes/pole-eg-egalitariste.svg",
        ["egalitariste"] = "res://assets/icons/classes/pole-eg-egalitariste.svg",
        ["SE"] = "res://assets/icons/classes/pole-se-segregationniste.svg",
        ["segregationniste"] = "res://assets/icons/classes/pole-se-segregationniste.svg",
        ["PO"] = "res://assets/icons/classes/pole-po-positiviste.svg",
        ["positiviste"] = "res://assets/icons/classes/pole-po-positiviste.svg",
        ["EC"] = "res://assets/icons/classes/pole-ec-ecologiste.svg",
        ["ecologiste"] = "res://assets/icons/classes/pole-ec-ecologiste.svg",
        ["PR"] = "res://assets/icons/classes/pole-pr-protectionniste.svg",
        ["protectionniste"] = "res://assets/icons/classes/pole-pr-protectionniste.svg",
        ["LI"] = "res://assets/icons/classes/pole-li-liberaux.svg",
        ["liberal"] = "res://assets/icons/classes/pole-li-liberaux.svg",
        ["liberaux"] = "res://assets/icons/classes/pole-li-liberaux.svg",
    };

    // ---- Catégories de bâtiments ----
    private static readonly Dictionary<string, string> CategoryPaths = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["Administration"] = "res://assets/icons/batiments/cat-administration.svg",
        ["Extraction"] = "res://assets/icons/batiments/cat-extraction.svg",
        ["Production"] = "res://assets/icons/batiments/cat-production.svg",
        ["Énergie"] = "res://assets/icons/batiments/cat-energie.svg",
        ["Energie"] = "res://assets/icons/batiments/cat-energie.svg",
        ["Services"] = "res://assets/icons/batiments/cat-services.svg",
        ["Logistique"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["Habitat"] = "res://assets/icons/batiments/cat-habitat.svg",
        ["Eau"] = "res://assets/icons/batiments/cat-eau.svg",
        ["Culture"] = "res://assets/icons/batiments/cat-culture.svg",
    };

    // ---- Bâtiments individuels remarquables ----
    private static readonly Dictionary<string, string> BuildingPaths = new(System.StringComparer.OrdinalIgnoreCase)
    {
        // Administration
        ["camp-de-base"] = "res://assets/icons/batiments/bat-camp-de-base.svg",
        ["camp de base"] = "res://assets/icons/batiments/bat-camp-de-base.svg",
        ["mairie"] = "res://assets/icons/batiments/bat-mairie.svg",
        ["prefecture"] = "res://assets/icons/batiments/bat-prefecture.svg",
        ["préfecture"] = "res://assets/icons/batiments/bat-prefecture.svg",
        ["palais"] = "res://assets/icons/batiments/bat-mairie.svg",

        // Extraction
        ["ferme"] = "res://assets/icons/batiments/bat-ferme.svg",
        ["entreprise-agricole"] = "res://assets/icons/batiments/bat-ferme.svg",
        ["entreprise agricole"] = "res://assets/icons/batiments/bat-ferme.svg",
        ["carriere"] = "res://assets/icons/batiments/bat-carriere.svg",
        ["carrière"] = "res://assets/icons/batiments/bat-carriere.svg",
        ["mine"] = "res://assets/icons/batiments/bat-mine.svg",
        ["mine-souterraine"] = "res://assets/icons/batiments/bat-mine.svg",
        ["mine souterraine"] = "res://assets/icons/batiments/bat-mine.svg",
        ["camp-forestier"] = "res://assets/icons/batiments/bat-camp-forestier.svg",
        ["camp forestier"] = "res://assets/icons/batiments/bat-camp-forestier.svg",
        ["camp-de-bucheron"] = "res://assets/icons/batiments/bat-camp-forestier.svg",
        ["camp de bucheron"] = "res://assets/icons/batiments/bat-camp-forestier.svg",
        ["camp de bûcheron"] = "res://assets/icons/batiments/bat-camp-forestier.svg",
        ["scierie"] = "res://assets/icons/batiments/bat-camp-forestier.svg",
        ["tour-de-forage"] = "res://assets/icons/batiments/cat-extraction.svg",
        ["tour de forage"] = "res://assets/icons/batiments/cat-extraction.svg",

        // Production
        ["atelier"] = "res://assets/icons/batiments/bat-atelier.svg",
        ["local-transformation"] = "res://assets/icons/batiments/bat-atelier.svg",
        ["local de transformation"] = "res://assets/icons/batiments/bat-atelier.svg",
        ["local de transformation alimentaire"] = "res://assets/icons/batiments/bat-atelier.svg",
        ["usine-agroalimentaire"] = "res://assets/icons/batiments/bat-atelier.svg",
        ["usine agroalimentaire"] = "res://assets/icons/batiments/bat-atelier.svg",
        ["forge"] = "res://assets/icons/batiments/bat-forge.svg",
        ["haut-fourneau"] = "res://assets/icons/batiments/bat-forge.svg",
        ["haut fourneau"] = "res://assets/icons/batiments/bat-forge.svg",
        ["usine"] = "res://assets/icons/batiments/cat-production.svg",
        ["raffinerie"] = "res://assets/icons/batiments/cat-production.svg",

        // Énergie
        ["centrale-charbon"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale à charbon"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale charbon"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale-gaz"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale à gaz"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale gaz"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale-fioul"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale à fioul"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale fioul"] = "res://assets/icons/batiments/cat-energie.svg",
        ["panneau-solaire"] = "res://assets/icons/batiments/cat-energie.svg",
        ["panneau solaire"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale-solaire"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale solaire"] = "res://assets/icons/batiments/cat-energie.svg",
        ["eolienne"] = "res://assets/icons/batiments/cat-energie.svg",
        ["éolienne"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale-geothermique"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale géothermique"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale geothermique"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale-nucleaire"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale nucléaire"] = "res://assets/icons/batiments/cat-energie.svg",
        ["centrale nucleaire"] = "res://assets/icons/batiments/cat-energie.svg",

        // Services
        ["dispensaire"] = "res://assets/icons/batiments/cat-services.svg",
        ["clinique"] = "res://assets/icons/batiments/cat-services.svg",
        ["hopital"] = "res://assets/icons/batiments/cat-services.svg",
        ["hôpital"] = "res://assets/icons/batiments/cat-services.svg",
        ["ecole"] = "res://assets/icons/batiments/bat-ecole.svg",
        ["école"] = "res://assets/icons/batiments/bat-ecole.svg",
        ["lycee"] = "res://assets/icons/batiments/bat-ecole.svg",
        ["lycée"] = "res://assets/icons/batiments/bat-ecole.svg",
        ["universite"] = "res://assets/icons/batiments/bat-ecole.svg",
        ["université"] = "res://assets/icons/batiments/bat-ecole.svg",
        ["caserne-pompiers"] = "res://assets/icons/batiments/cat-services.svg",
        ["caserne de pompiers"] = "res://assets/icons/batiments/cat-services.svg",
        ["pompiers"] = "res://assets/icons/batiments/cat-services.svg",
        ["poste-police"] = "res://assets/icons/batiments/cat-services.svg",
        ["poste de police"] = "res://assets/icons/batiments/cat-services.svg",
        ["police"] = "res://assets/icons/batiments/cat-services.svg",

        // Logistique
        ["marche"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["marché"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["magasin"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["centre-commercial"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["centre commercial"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["entrepot"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["entrepôt"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["depot-logistique"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["dépôt logistique"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["depot logistique"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["grenier"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["silo"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["route"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["voie-ferree"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["voie ferrée"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["voie ferree"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["autoroute"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["gare"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["port"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["aeroport"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["aéroport"] = "res://assets/icons/batiments/cat-logistique.svg",

        // Habitat
        ["logements"] = "res://assets/icons/batiments/cat-habitat.svg",
        ["logement"] = "res://assets/icons/batiments/cat-habitat.svg",

        // Eau
        ["puits"] = "res://assets/icons/batiments/cat-eau.svg",
        ["chateau-d-eau"] = "res://assets/icons/batiments/cat-eau.svg",
        ["château d'eau"] = "res://assets/icons/batiments/cat-eau.svg",
        ["chateau d eau"] = "res://assets/icons/batiments/cat-eau.svg",
        ["pompe-eau-souterraine"] = "res://assets/icons/batiments/cat-eau.svg",
        ["pompe à eau souterraine"] = "res://assets/icons/batiments/cat-eau.svg",
        ["pompe a eau souterraine"] = "res://assets/icons/batiments/cat-eau.svg",

        // Culture
        ["theatre"] = "res://assets/icons/batiments/bat-theatre.svg",
        ["théâtre"] = "res://assets/icons/batiments/bat-theatre.svg",
    };

    // ---- Ressources & Objets manufacturés ----
    private static readonly Dictionary<string, string> ProductPaths = new(System.StringComparer.OrdinalIgnoreCase)
    {
        // Matières premières & ressources
        ["ble"] = "res://assets/icons/ressources/res-ble.svg",
        ["blé"] = "res://assets/icons/ressources/res-ble.svg",
        ["bois"] = "res://assets/icons/ressources/res-bois.svg",
        ["pierre"] = "res://assets/icons/ressources/res-pierre.svg",
        ["charbon"] = "res://assets/icons/ressources/res-charbon.svg",
        ["minerai-fer"] = "res://assets/icons/ressources/res-minerai-fer.svg",
        ["minerai de fer"] = "res://assets/icons/ressources/res-minerai-fer.svg",
        ["minerai-cuivre"] = "res://assets/icons/ressources/res-minerai-cuivre.svg",
        ["minerai de cuivre"] = "res://assets/icons/ressources/res-minerai-cuivre.svg",
        ["or"] = "res://assets/icons/ressources/res-or.svg",
        ["minerai-or"] = "res://assets/icons/ressources/res-or.svg",
        ["minerai d'or"] = "res://assets/icons/ressources/res-or.svg",
        ["sable"] = "res://assets/icons/ressources/res-sable.svg",
        ["argile"] = "res://assets/icons/ressources/res-argile.svg",
        ["marbre"] = "res://assets/icons/ressources/res-marbre.svg",
        ["lin"] = "res://assets/icons/ressources/res-lin.svg",
        ["carcasse"] = "res://assets/icons/ressources/res-carcasse.svg",
        ["eau"] = "res://assets/icons/ressources/res-eau.svg",
        ["petrole"] = "res://assets/icons/ressources/res-petrole.svg",
        ["pétrole"] = "res://assets/icons/ressources/res-petrole.svg",
        ["farine"] = "res://assets/icons/ressources/res-farine.svg",
        ["cuir"] = "res://assets/icons/ressources/res-cuir.svg",
        ["tresor"] = "res://assets/icons/ressources/res-tresor.svg",
        ["trésor"] = "res://assets/icons/ressources/res-tresor.svg",

        // Objets manufacturés & services
        ["pain"] = "res://assets/icons/objets/obj-pain.svg",
        ["viande"] = "res://assets/icons/objets/obj-viande.svg",
        ["vetements"] = "res://assets/icons/objets/obj-vetements.svg",
        ["vêtements"] = "res://assets/icons/objets/obj-vetements.svg",
        ["meubles"] = "res://assets/icons/objets/obj-meubles.svg",
        ["poterie"] = "res://assets/icons/objets/obj-poterie.svg",
        ["alcool"] = "res://assets/icons/objets/obj-alcool.svg",
        ["administration"] = "res://assets/icons/objets/obj-administration.svg",
        ["services administratifs"] = "res://assets/icons/objets/obj-administration.svg",
        ["services-administratifs"] = "res://assets/icons/objets/obj-administration.svg",
        ["securite"] = "res://assets/icons/objets/obj-securite.svg",
        ["sécurité"] = "res://assets/icons/objets/obj-securite.svg",
        ["services de securite"] = "res://assets/icons/objets/obj-securite.svg",
        ["services de sécurité"] = "res://assets/icons/objets/obj-securite.svg",
        ["services-de-securite"] = "res://assets/icons/objets/obj-securite.svg",
        ["energie"] = "res://assets/icons/objets/obj-energie.svg",
        ["énergie"] = "res://assets/icons/objets/obj-energie.svg",
        ["tissus"] = "res://assets/icons/objets/obj-tissus.svg",
        ["planche"] = "res://assets/icons/objets/obj-planche.svg",
        ["lingot-fer"] = "res://assets/icons/objets/obj-lingot-fer.svg",
        ["lingot de fer"] = "res://assets/icons/objets/obj-lingot-fer.svg",
        ["lingot-cuivre"] = "res://assets/icons/objets/obj-lingot-cuivre.svg",
        ["lingot de cuivre"] = "res://assets/icons/objets/obj-lingot-cuivre.svg",
        ["plaque-de-fer"] = "res://assets/icons/objets/obj-plaque-de-fer.svg",
        ["plaque de fer"] = "res://assets/icons/objets/obj-plaque-de-fer.svg",
        ["outils"] = "res://assets/icons/objets/obj-outils.svg",
        ["charrette"] = "res://assets/icons/batiments/cat-logistique.svg",
        ["sculpture"] = "res://assets/icons/objets/obj-sculpture.svg",
        ["piece-de-theatre"] = "res://assets/icons/objets/obj-piece-de-theatre.svg",
        ["pièce de théâtre"] = "res://assets/icons/objets/obj-piece-de-theatre.svg",
        ["piece de theatre"] = "res://assets/icons/objets/obj-piece-de-theatre.svg",
        ["ciment"] = "res://assets/icons/objets/obj-ciment.svg",
        ["materiaux-de-construction"] = "res://assets/icons/objets/obj-materiaux-de-construction.svg",
        ["matériaux de construction"] = "res://assets/icons/objets/obj-materiaux-de-construction.svg",
        ["materiaux-de-construction-technique"] = "res://assets/icons/objets/obj-materiaux-de-construction-technique.svg",
        ["matériaux de construction technique"] = "res://assets/icons/objets/obj-materiaux-de-construction-technique.svg",
    };

    public static Texture2D? GetMenuIcon(string? menuTitle, int size = 24)
    {
        if (menuTitle != null && MenuPaths.TryGetValue(menuTitle, out var path))
            return LoadTexture(path, size);
        return null;
    }

    public static Texture2D? GetCategoryIcon(string? category, int size = 20)
    {
        if (category != null && CategoryPaths.TryGetValue(category, out var path))
            return LoadTexture(path, size);
        return null;
    }

    public static Texture2D? GetBuildingIcon(string? buildingId, string? category = null, int size = 20)
    {
        if (!string.IsNullOrWhiteSpace(buildingId))
        {
            var trimmed = buildingId.Trim();
            if (BuildingPaths.TryGetValue(trimmed, out var path))
                return LoadTexture(path, size);

            // Retirer le numéro éventuel à la fin (ex. "Atelier 2" -> "Atelier")
            var strippedNumber = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+\d+$", "");
            if (BuildingPaths.TryGetValue(strippedNumber, out var pathStripped))
                return LoadTexture(pathStripped, size);

            var normalized = StripAccents(strippedNumber).ToLowerInvariant().Replace(' ', '-').Replace("'", "");
            if (BuildingPaths.TryGetValue(normalized, out var pathNorm))
                return LoadTexture(pathNorm, size);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var trimmedCat = category.Trim();
            if (CategoryPaths.TryGetValue(trimmedCat, out var catPath))
                return LoadTexture(catPath, size);

            var normalizedCat = StripAccents(trimmedCat).ToLowerInvariant().Replace(' ', '-').Replace("'", "");
            if (CategoryPaths.TryGetValue(normalizedCat, out var normCatPath))
                return LoadTexture(normCatPath, size);
        }

        return null;
    }

    public static Texture2D? GetRankIcon(Rank rank, int size = 20) =>
        GetRankIcon(rank.ToString(), size);

    public static Texture2D? GetRankIcon(string? rankName, int size = 20)
    {
        if (rankName != null && RankPaths.TryGetValue(rankName, out var path))
            return LoadTexture(path, size);
        return null;
    }

    public static Texture2D? GetPoleIcon(Orientation pole, int size = 18) =>
        GetPoleIcon(pole.Code, size);

    public static Texture2D? GetPoleIcon(string? poleCodeOrName, int size = 18)
    {
        if (poleCodeOrName != null && PolePaths.TryGetValue(poleCodeOrName, out var path))
            return LoadTexture(path, size);
        return null;
    }

    public static Texture2D? GetPolesIcon(IReadOnlyList<Orientation>? poles, int size = 18)
    {
        if (poles == null || poles.Count == 0)
            return GetMenuIcon("Lois", size);

        var distinctPoles = poles.Distinct().ToList();
        if (distinctPoles.Count == 0)
            return GetMenuIcon("Lois", size);
        if (distinctPoles.Count == 1)
            return GetPoleIcon(distinctPoles[0], size);

        var targetSize = Scaled(size);
        var keyStr = string.Join("+", distinctPoles.Select(p => p.Code));
        var cacheKey = ($"poles:{keyStr}", targetSize);
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var spacing = 2;
        var totalWidth = distinctPoles.Count * targetSize + (distinctPoles.Count - 1) * spacing;
        var combined = Image.CreateEmpty(totalWidth, targetSize, false, Image.Format.Rgba8);

        for (var i = 0; i < distinctPoles.Count; i++)
        {
            var tex = GetPoleIcon(distinctPoles[i], size);
            if (tex is not null)
            {
                var img = tex.GetImage();
                if (img is not null && !img.IsEmpty())
                {
                    // Même format que l'image commune, sinon la copie échoue (logos importés par Godot).
                    img = (Image)img.Duplicate();
                    if (img.IsCompressed())
                        img.Decompress();
                    img.Convert(Image.Format.Rgba8);
                    combined.BlitRect(img, new Rect2I(0, 0, img.GetWidth(), img.GetHeight()), new Vector2I(i * (targetSize + spacing), 0));
                }
            }
        }

        var result = ImageTexture.CreateFromImage(combined);
        _cache[cacheKey] = result;
        return result;
    }

    /// <summary>
    /// Le chemin d'un logo pour la balise BBCode [img] des fiches (RichTextLabel) : celui du fichier importé, sinon un
    /// chemin inventé sous lequel l'image chargée est mise en cache par Godot (la balise lit d'abord ce cache). Le
    /// registre garde l'image, ce qui la garde dans le cache. Null sans logo.
    /// </summary>
    public static string? InlinePath(Texture2D? icon, string name)
    {
        if (icon is null)
            return null;
        if (!string.IsNullOrEmpty(icon.ResourcePath))
            return icon.ResourcePath;
        var path = $"res://_icones_jeu/{name}.png";
        icon.TakeOverPath(path);
        return path;
    }

    /// <summary>Le logo d'un pôle dans le texte d'une fiche : [img] à la taille du texte, ou null sans logo.</summary>
    public static string? PoleInline(Orientation pole, int size = 18)
    {
        var path = InlinePath(GetPoleIcon(pole, 14), $"pole-{pole.Code}-{Scaled(14)}");
        return path is null ? null : $"[img={size}x{size}]{path}[/img]";
    }

    /// <summary>
    /// Un carré de couleur (le propriétaire, par exemple) suivi du logo, dans une seule image : une case d'arbre n'a
    /// qu'une icône. Sans logo, le carré seul.
    /// </summary>
    public static Texture2D WithSquare(Texture2D? icon, Color color, int squareSize = 12)
    {
        if (icon is null)
            return UiTheme.Square(color, squareSize, squareSize);

        var cacheKey = ($"carre:{icon.GetRid().Id}:{color.ToHtml()}", squareSize);
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var img = icon.GetImage();
        if (img is null || img.IsEmpty())
            return UiTheme.Square(color, squareSize, squareSize);
        img = (Image)img.Duplicate();
        if (img.IsCompressed())
            img.Decompress();
        img.Convert(Image.Format.Rgba8);

        const int gap = 4;
        var height = System.Math.Max(img.GetHeight(), squareSize);
        var combined = Image.CreateEmpty(squareSize + gap + img.GetWidth(), height, false, Image.Format.Rgba8);
        combined.FillRect(new Rect2I(0, (height - squareSize) / 2, squareSize, squareSize), color);
        combined.BlitRect(img, new Rect2I(0, 0, img.GetWidth(), img.GetHeight()),
            new Vector2I(squareSize + gap, (height - img.GetHeight()) / 2));

        var result = ImageTexture.CreateFromImage(combined);
        _cache[cacheKey] = result;
        return result;
    }

    public static Texture2D? GetProductIcon(string? productId, int size = 20)
    {
        if (string.IsNullOrWhiteSpace(productId))
            return null;

        // 1. Recherche directe exacte
        if (ProductPaths.TryGetValue(productId, out var path))
            return LoadTexture(path, size);

        // 2. Recherche normalisée en minuscules avec tirets
        var trimmed = productId.Trim();
        var normalized = trimmed.ToLowerInvariant().Replace(' ', '-').Replace("'", "");
        if (ProductPaths.TryGetValue(normalized, out path))
            return LoadTexture(path, size);

        // 3. Recherche sans accents
        var stripped = StripAccents(trimmed).ToLowerInvariant();
        if (ProductPaths.TryGetValue(stripped, out path))
            return LoadTexture(path, size);

        var strippedNorm = stripped.Replace(' ', '-').Replace("'", "");
        if (ProductPaths.TryGetValue(strippedNorm, out path))
            return LoadTexture(path, size);

        var withoutD = strippedNorm.Replace("-de-", "-").Replace("-d-", "-");
        if (ProductPaths.TryGetValue(withoutD, out path))
            return LoadTexture(path, size);

        return null;
    }

    private static string StripAccents(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    public static Texture2D? GetResourceIcon(string? resourceId, int size = 20) =>
        GetProductIcon(resourceId, size);

    public static Texture2D? GetTreasuryIcon(int size = 18) =>
        GetResourceIcon("tresor", size);

    private static Texture2D? LoadTexture(string resPath, int size)
    {
        var targetSize = Scaled(size);
        var key = (resPath, targetSize);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        Texture2D? result = null;

        // 1. Chargement via ResourceLoader / GD.Load
        try
        {
            if (ResourceLoader.Exists(resPath))
            {
                var loaded = GD.Load<Texture2D>(resPath);
                if (loaded != null)
                {
                    if (loaded.GetWidth() == targetSize && loaded.GetHeight() == targetSize)
                    {
                        result = loaded;
                    }
                    else
                    {
                        var img = loaded.GetImage();
                        if (img != null && !img.IsEmpty())
                        {
                            img.Resize(targetSize, targetSize, Image.Interpolation.Lanczos);
                            result = ImageTexture.CreateFromImage(img);
                        }
                        else
                        {
                            result = loaded;
                        }
                    }
                }
            }
        }
        catch
        {
            // Repli sur le chargement direct
        }

        // 2. Repli : lecture directe du fichier SVG et rendu Image
        if (result == null)
        {
            try
            {
                var globalPath = ProjectSettings.GlobalizePath(resPath);
                if (File.Exists(globalPath))
                {
                    var img = Image.LoadFromFile(globalPath);
                    if (img != null && !img.IsEmpty())
                    {
                        if (img.GetWidth() != targetSize || img.GetHeight() != targetSize)
                        {
                            img.Resize(targetSize, targetSize, Image.Interpolation.Lanczos);
                        }
                        result = ImageTexture.CreateFromImage(img);
                    }
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[IconRegistry] Impossible de charger {resPath} : {ex.Message}");
            }
        }

        if (result != null)
        {
            _cache[key] = result;
        }

        return result;
    }
}
