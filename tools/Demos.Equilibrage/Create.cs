using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Demos.Application;
using Demos.Application.Ports;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Infrastructure;

/// <summary>Un nouveau besoin : son nom, et la quantité consommée chaque jour par personne de chaque rang.</summary>
public sealed record NeedDraft(string Id, string Name, string? Description, IReadOnlyDictionary<Rank, double> PerRank);

/// <summary>Un nouveau produit (ressource, bien ou service).</summary>
public sealed record ProductDraft(string Id, string Name, string? NeedId, double BasePrice, bool Stockable, double? ShelfLifeDays);

/// <summary>Une nouvelle recette d'un type de bâtiment : le produit, la production par travailleur et les matières.</summary>
public sealed record RecipeDraft(string TypeId, string ProductId, double QuantityPerWorker, IReadOnlyList<(string ProductId, double Quantity)> Materials);

/// <summary>Un bâtiment de la partie de départ, dans la première commune.</summary>
public sealed record StartingDraft(Ownership Ownership, string? OwnerClassId, int? Jobs, string? ProductId);

/// <summary>Un nouveau type de bâtiment du catalogue, avec sa première recette, et s'il est dans la partie de départ.</summary>
public sealed record BuildingDraft(
    string Id,
    string Name,
    string Category,
    int Level,
    string Description,
    int Tier,
    Rank MainRank,
    int Jobs,
    IReadOnlyDictionary<Rank, double> StaffRatios,
    IReadOnlyDictionary<Rank, double> Wages,
    int HousingPlaces,
    double DailyRent,
    int SchoolPlaces,
    double DailyTuition,
    int StorageCapacity,
    StorageKind StorageKind,
    string? StorageProductId,
    double PollutionPerWorker,
    bool ConstantHeating,
    RecipeDraft? FirstRecipe,
    StartingDraft? Start);

/// <summary>
/// Crée des besoins, produits, recettes et types de bâtiments dans les fichiers de game/data, en vérifiant tout d'abord.
/// Chaque « Add… » renvoie les erreurs (vide si tout va bien) et ne change rien en cas d'erreur ; « Save » vérifie que
/// le jeu démarre avec les nouveaux fichiers avant de les écrire.
/// </summary>
public sealed class ContentCreator
{
    private const string NeedsFile = "besoins.json";
    private const string ProductsFile = "products.json";
    private const string BuildingsFile = "buildings.json";
    private const string BalanceFile = "equilibrage.json";
    private const string ScenarioFile = "partie-depart.json";
    private static readonly string[] Files = [NeedsFile, ProductsFile, BuildingsFile, BalanceFile, ScenarioFile];

    private readonly string _dataDirectory;
    private readonly Dictionary<string, string> _texts;
    private readonly Dictionary<string, JsonNode> _json;

    public ContentCreator(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
        _texts = Files.ToDictionary(f => f, f => File.ReadAllText(Path.Combine(dataDirectory, f)));
        _json = _texts.ToDictionary(t => t.Key, t => JsonNode.Parse(t.Value)!);
        Reload();
        _pricesAtStart = PricesOf(Source());
    }

    // Les prix avant toute création, pour dire ce qui a changé.
    private readonly Dictionary<string, (double Price, bool Calculated)> _pricesAtStart;

    public IReadOnlyList<NeedDefinition> Needs { get; private set; } = [];

    public IReadOnlyList<ProductDefinition> Products { get; private set; } = [];

    public IReadOnlyList<BuildingType> BuildingTypes { get; private set; } = [];

    public ScenarioDefinition Scenario { get; private set; } = null!;

    /// <summary>Les catégories de bâtiments déjà utilisées (Extraction, Production…).</summary>
    public IReadOnlyList<string> Categories => BuildingTypes.Select(t => t.Category).Distinct().ToList();

    /// <summary>Les besoins qu'un produit peut couvrir : tous sauf se loger et l'énergie.</summary>
    public IReadOnlyList<NeedDefinition> PurchasableNeeds =>
        Needs.Where(n => n.Category != NeedCategory.Housing && n.Category != NeedCategory.Energy).ToList();

    /// <summary>Ce qui a été créé depuis l'ouverture, pour le journal.</summary>
    public List<string> Changes { get; } = [];

    private JsonContentSource Source() => new(file => _json.TryGetValue(file, out var node) ? JsonFormat.Write(node, _texts[file]) : _texts[file]);

    private void Reload()
    {
        var source = Source();
        Needs = source.LoadNeeds();
        Products = source.LoadProducts();
        BuildingTypes = source.LoadBuildingTypes();
        Scenario = source.LoadScenario();
    }

    /// <summary>Un identifiant tiré d'un nom : minuscules, sans accents, des tirets à la place des espaces.</summary>
    public static string IdFrom(string name)
    {
        var text = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                text.Append(char.ToLowerInvariant(c));
            else if (text.Length > 0 && text[^1] != '-')
                text.Append('-');
        }

        return text.ToString().Trim('-');
    }

    private static bool IsValidId(string id) => id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private static JsonNode Number(double value) =>
        Math.Abs(value - Math.Round(value)) < 1e-9 ? JsonValue.Create((long)Math.Round(value)) : JsonValue.Create(Math.Round(value, 10));

    private static void CheckId(List<string> errors, string id, IEnumerable<string> existing, string what)
    {
        if (!IsValidId(id))
            errors.Add($"Identifiant « {id} » : seulement des lettres sans accents, des chiffres et des tirets.");
        else if (existing.Contains(id, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Il existe déjà {what} « {id} ».");
    }

    private static void CheckPositive(List<string> errors, double value, string label, bool strict = false)
    {
        if (double.IsNaN(value) || value < 0 || strict && value == 0)
            errors.Add($"{label} : {(strict ? "doit être plus grand que 0" : "ne peut pas être négatif")}.");
    }

    // Besoins -----------------------------------------------------------------------------------------------------

    public List<string> AddNeed(NeedDraft draft)
    {
        var errors = new List<string>();
        CheckId(errors, draft.Id, Needs.Select(n => n.Id), "un besoin");
        if (string.IsNullOrWhiteSpace(draft.Name))
            errors.Add("Le nom est vide.");
        else if (Needs.Any(n => n.Name.Equals(draft.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            errors.Add($"Il existe déjà un besoin nommé « {draft.Name.Trim()} ».");
        foreach (var (rank, quantity) in draft.PerRank)
            CheckPositive(errors, quantity, $"Quantité des {Labels.RankName(rank).ToLowerInvariant()}");
        if (errors.Count > 0)
            return errors;

        var need = new JsonObject { ["id"] = draft.Id, ["name"] = draft.Name.Trim() };
        if (!string.IsNullOrWhiteSpace(draft.Description))
            need["description"] = draft.Description.Trim();
        _json[NeedsFile].AsArray().Add(need);

        // Les quantités par rang vont dans les réglages d'équilibrage (seulement celles qui ne sont pas nulles).
        var byRank = _json[BalanceFile]["needsByRank"] as JsonObject ?? new JsonObject();
        _json[BalanceFile]["needsByRank"] = byRank;
        foreach (var (rank, quantity) in draft.PerRank.Where(p => p.Value > 0))
        {
            if (byRank[rank.ToString()] is not JsonObject rankNeeds)
                byRank[rank.ToString()] = rankNeeds = new JsonObject();
            rankNeeds[draft.Id] = Number(quantity);
        }

        Changes.Add($"Besoin créé : {draft.Name.Trim()} ({draft.Id})");
        Reload();
        return errors;
    }

    // Produits ----------------------------------------------------------------------------------------------------

    public List<string> AddProduct(ProductDraft draft)
    {
        var errors = new List<string>();
        CheckId(errors, draft.Id, Products.Select(p => p.Id), "un produit");
        if (string.IsNullOrWhiteSpace(draft.Name))
            errors.Add("Le nom est vide.");
        if (draft.NeedId is { } needId && PurchasableNeeds.All(n => n.Id != needId))
            errors.Add($"Besoin inconnu « {needId} ».");
        CheckPositive(errors, draft.BasePrice, "Prix de catalogue", strict: true);
        if (draft.ShelfLifeDays is { } life && (life < 1 || Math.Abs(life - Math.Round(life)) > 1e-9))
            errors.Add("Durée de vie : un nombre entier de jours, au moins 1 (ou rien pour un produit qui ne se périme pas).");
        if (errors.Count > 0)
            return errors;

        var product = new JsonObject { ["id"] = draft.Id, ["name"] = draft.Name.Trim() };
        if (draft.NeedId is { } need)
            product["need"] = need;
        product["basePrice"] = Number(draft.BasePrice);
        if (draft.ShelfLifeDays is { } days)
            product["shelfLifeDays"] = Number(days);
        if (!draft.Stockable)
            product["stockable"] = false;
        _json[ProductsFile].AsArray().Add(product);

        Changes.Add($"Produit créé : {draft.Name.Trim()} ({draft.Id})");
        Reload();
        return errors;
    }

    // Recettes ----------------------------------------------------------------------------------------------------

    /// <summary>Vérifie une recette pour un type (existant ou en création, qui n'a encore aucune recette).</summary>
    private List<string> CheckRecipe(RecipeDraft draft, BuildingType? type, int jobs)
    {
        var errors = new List<string>();
        if (jobs <= 0)
            errors.Add("Ce bâtiment n'a pas d'emplois productifs : sa recette ne produirait rien.");
        if (Products.All(p => p.Id != draft.ProductId))
            errors.Add($"Produit inconnu « {draft.ProductId} ».");
        CheckPositive(errors, draft.QuantityPerWorker, "Production par travailleur", strict: true);
        foreach (var (materialId, quantity) in draft.Materials)
        {
            if (Products.All(p => p.Id != materialId))
                errors.Add($"Matière inconnue « {materialId} ».");
            else if (materialId == draft.ProductId)
                errors.Add($"Une recette ne peut pas consommer son propre produit ({materialId}).");
            CheckPositive(errors, quantity, $"Quantité de {materialId}", strict: true);
        }

        if (draft.Materials.Select(m => m.ProductId).Distinct().Count() != draft.Materials.Count)
            errors.Add("La même matière est donnée deux fois.");
        if (draft.Materials.Count > Layout.MaxMaterials)
            errors.Add($"Une recette a {Layout.MaxMaterials} matières au plus (le tableur n'a pas de colonnes pour les suivantes).");

        // Deux recettes d'un même bâtiment ne font pas le même produit avec les mêmes matières.
        var signature = Signature(draft.ProductId, draft.Materials.Select(m => m.ProductId));
        if (type?.AvailableRecipes.FirstOrDefault(r => Signature(r.ProductId, r.Materials.Select(m => m.ProductId)) == signature) is { } twin
            && twin.ProductId is not null)
            errors.Add($"« {type.Id} » a déjà cette recette ({twin.Label}).");
        return errors;
    }

    private static string Signature(string productId, IEnumerable<string> materials) => $"{productId} ← {string.Join(" + ", materials.Order())}";

    private static JsonObject RecipeJson(RecipeDraft draft)
    {
        var recipe = new JsonObject { ["productId"] = draft.ProductId, ["quantityPerWorker"] = Number(draft.QuantityPerWorker) };
        if (draft.Materials.Count > 0)
        {
            var inputs = new JsonArray();
            foreach (var (materialId, quantity) in draft.Materials)
                inputs.Add(new JsonObject { ["productId"] = materialId, ["quantity"] = Number(quantity) });
            recipe["inputs"] = inputs;
        }

        return recipe;
    }

    private JsonObject BuildingJson(string id) =>
        _json[BuildingsFile].AsArray().OfType<JsonObject>().First(b => b["id"]?.GetValue<string>() == id);

    public List<string> AddRecipe(RecipeDraft draft)
    {
        var type = BuildingTypes.FirstOrDefault(t => t.Id == draft.TypeId);
        if (type is null)
            return [$"Type de bâtiment inconnu « {draft.TypeId} »."];

        var errors = CheckRecipe(draft, type, type.Jobs);
        if (errors.Count > 0)
            return errors;

        // Un bâtiment à une seule production (« production ») passe à une liste de recettes ; la première reste celle par défaut.
        var json = BuildingJson(type.Id);
        if (json["recipes"] is not JsonArray recipes)
        {
            recipes = new JsonArray();
            if (json["production"] is JsonObject production)
            {
                json.Remove("production");
                recipes.Add(production);
            }

            InsertAfter(json, "recipes", recipes, ["wages", "staffRatios", "mainRank", "jobs"]);
        }

        recipes.Add(RecipeJson(draft));
        Changes.Add($"Recette créée : {type.Name} fabrique {draft.ProductId}"
            + (draft.Materials.Count > 0 ? $" avec {string.Join(" + ", draft.Materials.Select(m => m.ProductId))}" : ""));
        Reload();
        return errors;
    }

    /// <summary>Ajoute une propriété juste après la première des propriétés données qui existe (sinon à la fin).</summary>
    private static void InsertAfter(JsonObject json, string key, JsonNode value, string[] after)
    {
        var properties = json.ToList();
        var index = after.Select(a => properties.FindIndex(p => p.Key == a)).FirstOrDefault(i => i >= 0, properties.Count - 1);
        json.Clear();
        for (var i = 0; i < properties.Count; i++)
        {
            json[properties[i].Key] = properties[i].Value;
            if (i == index)
                json[key] = value;
        }

        if (!json.ContainsKey(key))
            json[key] = value;
    }

    // Bâtiments ---------------------------------------------------------------------------------------------------

    public List<string> AddBuilding(BuildingDraft draft)
    {
        var errors = new List<string>();
        CheckId(errors, draft.Id, BuildingTypes.Select(t => t.Id), "un type de bâtiment");
        if (string.IsNullOrWhiteSpace(draft.Name))
            errors.Add("Le nom est vide.");
        if (string.IsNullOrWhiteSpace(draft.Category))
            errors.Add("La catégorie est vide.");
        if (draft.Level < 0)
            errors.Add("Le niveau ne peut pas être négatif.");
        if (draft.Tier is < 1 or > 3)
            errors.Add("La liste doit être 1, 2 ou 3.");
        CheckPositive(errors, draft.Jobs, "Emplois productifs");
        foreach (var (rank, ratio) in draft.StaffRatios.Where(r => r.Key != draft.MainRank))
            CheckPositive(errors, ratio, $"{Labels.RankName(rank)} par emploi productif");
        foreach (var (rank, wage) in draft.Wages)
            CheckPositive(errors, wage, $"Salaire des {Labels.RankName(rank).ToLowerInvariant()}");
        CheckPositive(errors, draft.HousingPlaces, "Places de logement");
        CheckPositive(errors, draft.DailyRent, "Loyer par place");
        CheckPositive(errors, draft.SchoolPlaces, "Places d'élèves");
        CheckPositive(errors, draft.DailyTuition, "Frais par élève");
        CheckPositive(errors, draft.StorageCapacity, "Stockage");
        CheckPositive(errors, draft.PollutionPerWorker, "Pollution par travailleur");
        if (draft.StorageCapacity > 0 && draft.StorageKind == StorageKind.Specific && string.IsNullOrEmpty(draft.StorageProductId))
            errors.Add("Un stockage spécifique doit dire quel produit il garde.");
        if (draft.StorageProductId is { Length: > 0 } stored && Products.All(p => p.Id != stored))
            errors.Add($"Produit stocké inconnu « {stored} ».");
        if (draft.FirstRecipe is { } recipe)
            errors.AddRange(CheckRecipe(recipe with { TypeId = draft.Id }, null, draft.Jobs));
        if (draft.Start is { } start)
        {
            if (start.Ownership != Ownership.Public && Scenario.Classes.All(c => c.Id != start.OwnerClassId))
                errors.Add("Un bâtiment privé ou coopératif de la partie de départ doit avoir une classe propriétaire.");
            if (start.Jobs is < 0)
                errors.Add("Emplois dans la partie de départ : ne peut pas être négatif.");
        }

        if (errors.Count > 0)
            return errors;

        var json = new JsonObject
        {
            ["id"] = draft.Id,
            ["name"] = draft.Name.Trim(),
            ["category"] = draft.Category.Trim(),
            ["level"] = draft.Level,
            ["description"] = draft.Description.Trim(),
        };
        if (draft.Jobs > 0)
        {
            json["jobs"] = draft.Jobs;
            var ratios = new JsonObject();
            foreach (var rank in Labels.Ranks.Where(r => r != draft.MainRank && draft.StaffRatios.GetValueOrDefault(r) > 0))
                ratios[rank.ToString()] = Number(draft.StaffRatios[rank]);
            json["staffRatios"] = ratios;
            var wages = new JsonObject();
            foreach (var rank in Labels.Ranks)
                wages[rank.ToString()] = Number(draft.Wages.GetValueOrDefault(rank));
            json["wages"] = wages;
            if (draft.MainRank != Rank.Workers)
                json["mainRank"] = draft.MainRank.ToString();
        }

        if (draft.FirstRecipe is { } first)
            json["recipes"] = new JsonArray(RecipeJson(first));
        if (draft.HousingPlaces > 0)
        {
            json["housingPlaces"] = draft.HousingPlaces;
            json["dailyRent"] = Number(draft.DailyRent);
        }

        if (draft.SchoolPlaces > 0)
        {
            json["schoolPlaces"] = draft.SchoolPlaces;
            json["dailyTuition"] = Number(draft.DailyTuition);
        }

        json["posts"] = 1;
        if (draft.StorageCapacity > 0)
        {
            json["storageCapacity"] = draft.StorageCapacity;
            if (draft.StorageKind != StorageKind.General)
                json["storageKind"] = draft.StorageKind.ToString();
            if (draft.StorageKind == StorageKind.Specific)
                json["storageProductId"] = draft.StorageProductId;
        }

        if (draft.PollutionPerWorker > 0)
            json["pollutionPerWorker"] = Number(draft.PollutionPerWorker);
        if (draft.ConstantHeating)
            json["constantHeating"] = true;
        json["tier"] = draft.Tier;

        // Rangé après le dernier type de la même catégorie, sinon à la fin.
        var buildings = _json[BuildingsFile].AsArray();
        var last = buildings.Select((b, i) => (b, i)).LastOrDefault(p => p.b?["category"]?.GetValue<string>() == draft.Category.Trim());
        if (last.b is not null)
            buildings.Insert(last.i + 1, json);
        else
            buildings.Add(json);
        Changes.Add($"Type de bâtiment créé : {draft.Name.Trim()} ({draft.Id})");

        if (draft.Start is { } starting)
            AddToStartingGame(draft, starting);
        Reload();
        return errors;
    }

    private void AddToStartingGame(BuildingDraft draft, StartingDraft start)
    {
        var buildings = _json[ScenarioFile]["buildings"]!.AsArray();
        var ids = buildings.Select(b => b?["id"]?.GetValue<string>()).ToHashSet();
        var id = draft.Id;
        for (var n = 2; ids.Contains(id); n++)
            id = $"{draft.Id}-{n}";

        var json = new JsonObject
        {
            ["id"] = id,
            ["type"] = draft.Id,
            ["commune"] = PrototypeScenario.FirstCommuneId,
            ["name"] = draft.Name.Trim(),
        };
        if (start.ProductId is { } product)
            json["product"] = product;
        if (start.Ownership != Ownership.Public)
        {
            json["ownership"] = start.Ownership.ToString();
            json["owner"] = start.OwnerClassId;
        }

        if (start.Jobs is { } jobs && jobs != draft.Jobs)
            json["jobs"] = jobs;
        buildings.Add(json);
        Changes.Add($"Ajouté à la partie de départ, dans la première commune : {draft.Name.Trim()}");
    }

    // Prix ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Le prix de chaque produit tel que le jeu le calcule au démarrage : recalculé (30 % de bénéfice) quand un
    /// bâtiment le fabrique, sinon le prix de secours du catalogue.
    /// </summary>
    public static Dictionary<string, (double Price, bool Calculated)> PricesOf(IContentSource content)
    {
        var saved = Balance.Current;
        try
        {
            Balance.Current = content.LoadBalance();
            var catalogue = content.LoadBuildingTypes();
            return Pricing.WithReferencePrices(content.LoadProducts(), catalogue)
                .ToDictionary(p => p.Id, p => (p.BasePrice, Pricing.ReferenceFor(p.Id, catalogue) is not null));
        }
        finally
        {
            Balance.Current = saved;
        }
    }

    /// <summary>Ce que la création a changé aux prix, pour le journal : les nouveaux produits et les prix qui ont bougé.</summary>
    public IEnumerable<string> PriceChanges()
    {
        var names = Products.ToDictionary(p => p.Id, p => p.Name);
        foreach (var (id, (price, calculated)) in PricesOf(Source()))
        {
            var how = calculated ? "calculé" : "de secours : aucun bâtiment ne le fabrique encore";
            if (!_pricesAtStart.TryGetValue(id, out var old))
                yield return $"Prix de {names[id]} : {price:0.##} ({how})";
            else if (Math.Abs(old.Price - price) > 0.005)
                yield return $"Prix de {names[id]} : {old.Price:0.##} → {price:0.##} ({how})";
        }
    }

    // Écriture ----------------------------------------------------------------------------------------------------

    /// <summary>Vérifie que le jeu démarre avec les nouveaux fichiers, puis les écrit. Renvoie les erreurs.</summary>
    public List<string> Save()
    {
        var written = _json.ToDictionary(j => j.Key, j => JsonFormat.Write(j.Value, _texts[j.Key]));

        // Chaque besoin d'un produit doit être dans la liste des besoins.
        var source = new JsonContentSource(file => written[file]);
        var needIds = source.LoadNeeds().Select(n => n.Id).ToHashSet();
        var unknown = source.LoadProducts().Where(p => p.Need is { } need && !needIds.Contains(need.Id)).ToList();
        if (unknown.Count > 0)
            return unknown.Select(p => $"Le produit « {p.Id} » couvre un besoin inconnu « {p.Need} ».").ToList();

        var saved = Balance.Current;
        try
        {
            PrototypeScenario.Create(source).AdvanceDay();
        }
        catch (Exception e)
        {
            return [$"Le jeu ne démarre pas avec ces changements, rien n'a été écrit : {e.Message}"];
        }
        finally
        {
            Balance.Current = saved;
        }

        foreach (var (file, text) in written.Where(w => w.Value != _texts[w.Key]))
        {
            File.WriteAllText(Path.Combine(_dataDirectory, file), text);
            _texts[file] = text;
        }

        return [];
    }
}
