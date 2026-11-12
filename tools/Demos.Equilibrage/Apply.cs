using System.Globalization;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using Demos.Application;
using Demos.Application.Ports;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;
using Demos.Infrastructure;

/// <summary>
/// Commande « appliquer » : lit les cellules jaunes du tableur et les reporte dans les fichiers de game/data.
/// Chaque réglage est retrouvé par sa clé (colonne « Clé (ne pas modifier) », ou l'identifiant du bâtiment ou du
/// produit). Seules les valeurs changées sont écrites. Si une valeur est fausse, rien n'est écrit.
/// </summary>
public static class BalanceApplier
{
    private static readonly Rank[] Ranks = [Rank.Workers, Rank.Intermediate, Rank.Executives];

    private const string BalanceFile = "equilibrage.json";
    private const string BuildingsFile = "buildings.json";
    private const string ProductsFile = "products.json";
    private const string ScenarioFile = "partie-depart.json";

    // Les constantes reportées : clé du tableur, chemin dans equilibrage.json, valeur actuelle.
    private static readonly (string Key, string[] Path, Func<BalanceSettings, double> Current)[] Constants =
    [
        ("Marge", ["margin"], b => b.Margin),
        ("Encadrement", ["supervisorsPerJob"], b => b.SupervisorsPerJob),
        ("Direction", ["directorsPerJob"], b => b.DirectorsPerJob),
        ("MultOuvriers", ["wageMultipliers", "Workers"], b => b.WageMultipliers[Rank.Workers]),
        ("MultPI", ["wageMultipliers", "Intermediate"], b => b.WageMultipliers[Rank.Intermediate]),
        ("MultCadres", ["wageMultipliers", "Executives"], b => b.WageMultipliers[Rank.Executives]),
        ("Chauffage", ["heatingPerUnit"], b => b.HeatingPerUnit),
        ("Eau", ["waterPerUnit"], b => b.WaterPerUnit),
        ("Hiver", ["heatingBySeason", "Winter"], b => b.HeatingBySeason[Season.Winter]),
        ("Printemps", ["heatingBySeason", "Spring"], b => b.HeatingBySeason[Season.Spring]),
        ("Ete", ["heatingBySeason", "Summer"], b => b.HeatingBySeason[Season.Summer]),
        ("Automne", ["heatingBySeason", "Autumn"], b => b.HeatingBySeason[Season.Autumn]),
        ("SansEnergie", ["outputWithoutEnergy"], b => b.OutputWithoutEnergy),
        ("ReserveCombustible", ["fuelReserveDays"], b => b.FuelReserveDays),
        ("ReserveEntreprise", ["companyReserveDays"], b => b.CompanyReserveDays),
        ("ReserveDepart", ["startingReserveDays"], b => b.StartingReserveDays),
        ("EpargneReserve", ["reserveSavingsShare"], b => b.ReserveSavingsShare),
        ("ImpotEtat", ["stateTaxRate"], b => b.StateTaxRate),
        ("ImpotCommune", ["communeTaxRate"], b => b.CommuneTaxRate),
        ("TvaEtat", ["vatStateRate"], b => b.VatStateRate),
        ("TvaCommune", ["vatCommuneRate"], b => b.VatCommuneRate),
        ("Transport", ["transportLoss"], b => b.TransportLoss),
        ("TransportDistance", ["transportReferenceDistance"], b => b.TransportReferenceDistance),
        ("TransportMin", ["transportLossMin"], b => b.TransportLossMin),
        ("TransportMax", ["transportLossMax"], b => b.TransportLossMax),
        ("PollutionLoin", ["distantPollutionShare"], b => b.DistantPollutionShare),
        ("PollutionDistance", ["pollutionDispersionDistance"], b => b.PollutionDispersionDistance),
        ("ExtractionDistance", ["extractionMaxDistance"], b => b.ExtractionMaxDistance),
        ("RessortCommune", ["communeJurisdictionRadius"], b => b.CommuneJurisdictionRadius),
        ("RessortPrefecture", ["prefectureJurisdictionRadius"], b => b.PrefectureJurisdictionRadius),
        ("RouteSeuilSentier", ["roadTrailTraffic"], b => b.RoadTrailTraffic),
        ("RouteSeuilChemin", ["roadPathTraffic"], b => b.RoadPathTraffic),
        ("RouteSeuilRoute", ["roadRoadTraffic"], b => b.RoadRoadTraffic),
        ("RouteCoutSentier", ["roadTrailCost"], b => b.RoadTrailCost),
        ("RouteCoutChemin", ["roadPathCost"], b => b.RoadPathCost),
        ("RouteCoutRoute", ["roadRoadCost"], b => b.RoadRoadCost),
        ("RouteEntretienSentier", ["roadTrailUpkeep"], b => b.RoadTrailUpkeep),
        ("RouteEntretienChemin", ["roadPathUpkeep"], b => b.RoadPathUpkeep),
        ("RouteEntretienRoute", ["roadRoadUpkeep"], b => b.RoadRoadUpkeep),
        ("RouteMontee", ["roadClimbLength"], b => b.RoadClimbLength),
        ("RouteBaisseChemin", ["roadPathLossCut"], b => b.RoadPathLossCut),
        ("RouteBaisseRoute", ["roadRoadLossCut"], b => b.RoadRoadLossCut),
        ("TracePenteMax", ["roadMaxSlope"], b => b.RoadMaxSlope),
        ("TracePoidsPente", ["roadSlopeWeight"], b => b.RoadSlopeWeight),
        ("TraceReutilisation", ["roadReuseFactor"], b => b.RoadReuseFactor),
        ("TraceCase", ["roadGridCell"], b => b.RoadGridCell),
        ("SeuilPollution", ["pollutedThreshold"], b => b.PollutedThreshold),
        ("Dotation", ["foundingGrantDays"], b => b.FoundingGrantDays),
        ("StockFondation", ["foundingMaterials"], b => b.FoundingMaterials),
        ("LicenciementInvendus", ["layoffUnsoldDays"], b => b.LayoffUnsoldDays),
        ("EmbaucheInvendus", ["hireUnsoldDays"], b => b.HireUnsoldDays),
        ("VitessePublic", ["jobChangePerMonth", "Public"], b => b.JobChangePerMonth[Ownership.Public]),
        ("VitesseScop", ["jobChangePerMonth", "Cooperative"], b => b.JobChangePerMonth[Ownership.Cooperative]),
        ("VitessePrive", ["jobChangePerMonth", "Private"], b => b.JobChangePerMonth[Ownership.Private]),
        ("BaisseSalaireScop", ["cooperativeWageCutStep"], b => b.CooperativeWageCutStep),
        ("BaisseSalaireScopMax", ["cooperativeWageCutMax"], b => b.CooperativeWageCutMax),
        ("LoiRemboursement", ["lawPaybackDays"], b => b.LawPaybackDays),
        ("LoiNonVises", ["untargetedMemberWeight"], b => b.UntargetedMemberWeight),
        ("LoiVerrou", ["lockedBrickCostFactor"], b => b.LockedBrickCostFactor),
        ("LoiRegalien", ["regalianCostFactor"], b => b.RegalianCostFactor),
        ("LoiMinimum", ["minimumBrickCost"], b => b.MinimumBrickCost),
        ("ExtensionPopulation", ["extensionPopulationCapacity"], b => b.ExtensionPopulationCapacity),
        ("ExtensionBatiments", ["extensionBuildingCapacity"], b => b.ExtensionBuildingCapacity),
        ("ExtensionAutonomes", ["extensionAutonomousWeight"], b => b.ExtensionAutonomousWeight),
        ("ExtensionContenues", ["extensionChildWeight"], b => b.ExtensionChildWeight),
        ("ExtensionPartComptee", ["extensionCountedShare"], b => b.ExtensionCountedShare),
        ("ExtensionDistance", ["extensionDistancePerLoad"], b => b.ExtensionDistancePerLoad),
        ("BureaucratieEchelon", ["bureaucracyLossPerLevel"], b => b.BureaucracyLossPerLevel),
        ("PrefectureEmplois", ["prefectureJobsPerEntity"], b => b.PrefectureJobsPerEntity),
        ("PrefectureLegitimite", ["prefectureLegitimacyCost"], b => b.PrefectureLegitimacyCost),
        ("RattachementLegitimite", ["attachLegitimacyCost"], b => b.AttachLegitimacyCost),
        ("PrefectureMinimum", ["prefectureMinimumMembers"], b => b.PrefectureMinimumMembers),
    ];

    public static int Apply(string workbookPath, string dataDirectory, IContentSource content)
    {
        if (!File.Exists(workbookPath))
        {
            Console.WriteLine($"Tableur introuvable : {workbookPath}. Lancez d'abord generer-equilibrage.bat.");
            return 1;
        }

        XLWorkbook workbook;
        try
        {
            // Lecture partagée : le tableur peut rester ouvert dans Excel (c'est la version enregistrée qui compte).
            using var stream = new FileStream(workbookPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            workbook = new XLWorkbook(stream);
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            Console.WriteLine($"Impossible de lire le tableur : {e.Message}");
            return 1;
        }

        var texts = new[] { BalanceFile, BuildingsFile, ProductsFile, ScenarioFile }
            .ToDictionary(f => f, f => File.ReadAllText(Path.Combine(dataDirectory, f)));
        var applier = new Applier(workbook, content,
            JsonNode.Parse(texts[BalanceFile])!.AsObject(),
            JsonNode.Parse(texts[BuildingsFile])!.AsArray(),
            JsonNode.Parse(texts[ProductsFile])!.AsArray(),
            JsonNode.Parse(texts[ScenarioFile])!.AsObject());

        applier.Run();
        using (workbook) { }

        if (applier.Errors.Count > 0)
        {
            Console.WriteLine($"{applier.Errors.Count} erreur(s) dans le tableur ; rien n'a été écrit :");
            foreach (var error in applier.Errors)
                Console.WriteLine($"  - {error}");
            return 1;
        }

        if (applier.Changes.Count == 0)
        {
            Console.WriteLine("Aucun changement : le jeu a déjà les réglages du tableur.");
            return 0;
        }

        var written = new Dictionary<string, string>
        {
            [BalanceFile] = JsonFormat.Write(applier.BalanceJson, texts[BalanceFile]),
            [BuildingsFile] = JsonFormat.Write(applier.BuildingsJson, texts[BuildingsFile]),
            [ProductsFile] = JsonFormat.Write(applier.ProductsJson, texts[ProductsFile]),
            [ScenarioFile] = JsonFormat.Write(applier.ScenarioJson, texts[ScenarioFile]),
        };

        // Essai à blanc : le jeu doit pouvoir charger les nouveaux fichiers et créer la partie de départ.
        var saved = Balance.Current;
        try
        {
            PrototypeScenario.Create(new JsonContentSource(file =>
                written.TryGetValue(file, out var text) ? text : File.ReadAllText(Path.Combine(dataDirectory, file))));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Les nouveaux réglages empêchent le jeu de démarrer ; rien n'a été écrit : {e.Message}");
            return 1;
        }
        finally
        {
            Balance.Current = saved;
        }

        foreach (var (file, text) in written.Where(w => w.Value != texts[w.Key]))
            File.WriteAllText(Path.Combine(dataDirectory, file), text);

        Console.WriteLine($"{applier.Changes.Count} changement(s) reporté(s) dans le jeu :");
        foreach (var change in applier.Changes)
            Console.WriteLine($"  - {change}");
        Console.WriteLine("Lancez simuler.bat pour voir l'effet, ou generer-equilibrage.bat pour revoir les prix recalculés.");
        return 0;
    }

    private sealed class Applier(XLWorkbook workbook, IContentSource content, JsonObject balanceJson, JsonArray buildingsJson,
        JsonArray productsJson, JsonObject scenarioJson)
    {
        public List<string> Errors { get; } = [];

        public List<string> Changes { get; } = [];

        public JsonObject BalanceJson => balanceJson;

        public JsonArray BuildingsJson => buildingsJson;

        public JsonArray ProductsJson => productsJson;

        public JsonObject ScenarioJson => scenarioJson;

        private readonly BalanceSettings _balance = content.LoadBalance();
        private readonly Dictionary<string, BuildingType> _types = content.LoadBuildingTypes().ToDictionary(t => t.Id);
        private readonly Dictionary<string, ProductDefinition> _products = content.LoadProducts().ToDictionary(p => p.Id);
        private readonly ScenarioDefinition _scenario = content.LoadScenario();
        private readonly HashSet<string> _needs = content.LoadNeeds().Select(n => n.Id).ToHashSet();

        public void Run()
        {
            ApplyConstants();
            ApplyNeeds();
            ApplyBuildings();
            ApplyRecipes();
            ApplyProducts();
            ApplyStartingGame();
        }

        private IXLWorksheet? Sheet(string name)
        {
            if (workbook.TryGetWorksheet(name, out var sheet))
                return sheet;

            Errors.Add($"la feuille « {name} » manque : régénérez le tableur.");
            return null;
        }

        // Lecture des cellules ----------------------------------------------------------------------------------------

        private static string Where(IXLCell cell) => $"{cell.Worksheet.Name}!{cell.Address}";

        private static string Text(IXLCell cell) => cell.Value.IsBlank ? "" : cell.Value.ToString(CultureInfo.InvariantCulture).Trim();

        /// <summary>Un nombre positif ou nul (entier si demandé) ; null et une erreur sinon, ou null sans erreur si la case peut être vide.</summary>
        private double? Number(IXLCell cell, bool integer = false, bool optional = false)
        {
            double value;
            var cellValue = cell.Value;
            if (cellValue.IsBlank || cellValue.IsText && string.IsNullOrWhiteSpace(cellValue.GetText()))
            {
                if (!optional)
                    Errors.Add($"{Where(cell)} : la case est vide.");
                return null;
            }

            if (cellValue.IsNumber)
                value = cellValue.GetNumber();
            else if (cellValue.IsText && (double.TryParse(cellValue.GetText().Replace(" ", ""), NumberStyles.Float, CultureInfo.GetCultureInfo("fr-FR"), out value)
                         || double.TryParse(cellValue.GetText(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)))
            {
            }
            else
            {
                Errors.Add($"{Where(cell)} : « {cellValue} » n'est pas un nombre.");
                return null;
            }

            if (value < 0)
            {
                Errors.Add($"{Where(cell)} : {Show(value)} est négatif.");
                return null;
            }

            if (integer && Math.Abs(value - Math.Round(value)) > 1e-9)
            {
                Errors.Add($"{Where(cell)} : {Show(value)} doit être un nombre entier.");
                return null;
            }

            // Arrondi : Excel garde parfois 0,15 sous la forme 0,1499999999…
            return Math.Round(value, 10);
        }

        /// <summary>Une valeur choisie dans un menu déroulant ; null et une erreur si elle n'est pas dans le menu.</summary>
        private T? Pick<T>(IXLCell cell, Func<string, T?> parse) where T : struct
        {
            var text = Text(cell);
            if (parse(text) is { } value)
                return value;

            Errors.Add($"{Where(cell)} : « {text} » n'est pas dans le menu déroulant.");
            return null;
        }

        /// <summary>Les lignes d'une feuille, de la première jusqu'à la première case vide dans la colonne clé.</summary>
        private static IEnumerable<IXLRow> Rows(IXLWorksheet sheet, int first, string keyColumn)
        {
            for (var row = first; !sheet.Cell(row, keyColumn).Value.IsBlank; row++)
                yield return sheet.Row(row);
        }

        /// <summary>
        /// Vérifie que la colonne clé est à sa place : un tableur généré avant un changement de colonnes (plus de
        /// matières par recette, par exemple) serait sinon lu de travers.
        /// </summary>
        private bool KeyColumnAt(IXLWorksheet sheet, int headerRow, string keyColumn) => HeaderAt(sheet, headerRow, keyColumn, KeyHeader);

        private bool HeaderAt(IXLWorksheet sheet, int headerRow, string column, string title)
        {
            if (Text(sheet.Cell(headerRow, column)) == title)
                return true;
            Errors.Add($"{sheet.Name} : la colonne {column} n'est pas « {title} » : le tableur date d'une ancienne version, régénérez-le.");
            return false;
        }

        private const string KeyHeader = "Clé (ne pas modifier)";

        // Écriture dans les fichiers ----------------------------------------------------------------------------------

        private static bool Same(double a, double b) => Math.Abs(a - b) < 1e-9;

        public static string Show(double value) => value.ToString("0.##########", CultureInfo.GetCultureInfo("fr-FR"));

        private static JsonNode Json(double value) =>
            Math.Abs(value - Math.Round(value)) < 1e-9 && Math.Abs(value) < 1e15
                ? JsonValue.Create((long)Math.Round(value))
                : JsonValue.Create(value);

        private static JsonObject ObjectAt(JsonObject root, IEnumerable<string> path)
        {
            var node = root;
            foreach (var key in path)
            {
                if (node[key] is not JsonObject child)
                {
                    child = new JsonObject();
                    node[key] = child;
                }

                node = child;
            }

            return node;
        }

        /// <summary>Reporte une valeur si elle a changé, et le note dans le journal.</summary>
        private void Set(JsonObject target, string key, double? value, double current, string label, bool removeWhenZero = false)
        {
            if (value is not { } v || Same(v, current))
                return;

            if (removeWhenZero && v == 0)
                target.Remove(key);
            else
                target[key] = Json(v);
            Changes.Add($"{label} : {Show(current)} → {Show(v)}");
        }

        private JsonObject? BuildingJson(string id) =>
            buildingsJson.OfType<JsonObject>().FirstOrDefault(b => b["id"]?.GetValue<string>() == id);

        // Feuilles ------------------------------------------------------------------------------------------------------

        private void ApplyConstants()
        {
            if (Sheet("Constantes") is not { } sheet)
                return;

            var rows = Rows(sheet, 2, "A").ToDictionary(r => Text(r.Cell("D")), r => r);
            foreach (var (key, path, current) in Constants)
            {
                if (!rows.TryGetValue(key, out var row))
                {
                    Errors.Add($"Constantes : la clé « {key} » manque : régénérez le tableur.");
                    continue;
                }

                var integer = key is "ReserveCombustible" or "ReserveEntreprise" or "ReserveDepart" or "Dotation";
                var value = Number(row.Cell("B"), integer);

                // Une part (vitesse de licenciement, baisse des salaires) ne dépasse pas 100 %.
                if (key.StartsWith("Vitesse") || key.StartsWith("BaisseSalaire") || key.StartsWith("Tva") || key == "EpargneReserve")
                {
                    if (value > 1)
                    {
                        Errors.Add($"{Where(row.Cell("B"))} : {Show(value.Value)} dépasse 1 (100 %).");
                        value = null;
                    }
                }

                Set(ObjectAt(balanceJson, path[..^1]), path[^1], value, current(_balance), $"constante « {Text(row.Cell("A"))} »");
            }
        }

        private void ApplyNeeds()
        {
            if (Sheet("Besoins") is not { } sheet)
                return;

            foreach (var row in Rows(sheet, 2, "G"))
            {
                var key = Text(row.Cell("G"));
                var label = Text(row.Cell("A"));
                for (var r = 0; r < Ranks.Length; r++)
                {
                    var rank = Ranks[r];
                    var cell = row.Cell(4 + r);
                    var value = Number(cell);
                    if (key == "Housing")
                    {
                        if (value == 0)
                        {
                            Errors.Add($"{Where(cell)} : il faut au moins une place de logement par personne.");
                            continue;
                        }

                        Set(ObjectAt(balanceJson, ["housingPlacesByRank"]), rank.ToString(), value, Housing.PlacesPerPerson(rank),
                            $"besoin « {label} » des {RankLabel(rank)}");
                    }
                    else if (_needs.Contains(key) && new NeedCategory(key) is var need)
                    {
                        Set(ObjectAt(balanceJson, ["needsByRank", rank.ToString()]), need.ToString(), value,
                            _balance.NeedsByRank.GetValueOrDefault(rank)?.GetValueOrDefault(need) ?? 0,
                            $"besoin « {label} » des {RankLabel(rank)}", removeWhenZero: true);
                    }
                    else
                    {
                        Errors.Add($"{Where(row.Cell("G"))} : besoin inconnu « {key} ».");
                        break;
                    }
                }
            }
        }

        private void ApplyBuildings()
        {
            if (Sheet("Bâtiments") is not { } sheet)
                return;
            // Sans les colonnes de construction (ancien tableur), les coûts de construction seraient effacés.
            if (!HeaderAt(sheet, 1, "AF", "Matériau de construction"))
                return;

            foreach (var row in Rows(sheet, 2, "A"))
            {
                var id = Text(row.Cell("A"));
                if (!_types.TryGetValue(id, out var type) || BuildingJson(id) is not { } json)
                {
                    Errors.Add($"{Where(row.Cell("A"))} : type de bâtiment inconnu « {id} ».");
                    continue;
                }

                var tier = Number(row.Cell("E"), integer: true);
                if (tier is < 1 or > 3)
                {
                    Errors.Add($"{Where(row.Cell("E"))} : la liste doit être 1, 2 ou 3.");
                    tier = null;
                }

                Set(json, "tier", tier, type.Tier, $"{id} : liste");
                Set(json, "jobs", Number(row.Cell("H"), integer: true), type.Jobs, $"{id} : emplois productifs");

                // Chauffage constant (la forge) : oui ou non.
                if (Pick<bool>(row.Cell("F"), text => text == Labels.Yes ? true : text == Labels.No ? false : null) is { } constant
                    && constant != type.ConstantHeating)
                {
                    if (constant)
                        json["constantHeating"] = true;
                    else
                        json.Remove("constantHeating");
                    Changes.Add($"{id} : chauffage constant {(type.ConstantHeating ? Labels.Yes : Labels.No)} → {(constant ? Labels.Yes : Labels.No)}");
                }

                // Rang productif : celui qui produit ; les autres rangs encadrent.
                var mainRank = Pick(row.Cell("G"), Labels.RankOf) ?? type.MainRank;
                var mainRankChanged = mainRank != type.MainRank;
                if (mainRankChanged)
                {
                    if (mainRank == Rank.Workers)
                        json.Remove("mainRank");
                    else
                        json["mainRank"] = mainRank.ToString();
                    Changes.Add($"{id} : rang productif {Labels.RankName(type.MainRank)} → {Labels.RankName(mainRank)}");
                }

                // Proportions d'emplois des rangs autres que le rang productif : toutes écrites dès que l'une change.
                // Quand le rang productif change, l'ancien (qui comptait 1) prend la proportion par défaut : à revoir
                // après avoir régénéré le tableur.
                var ratios = Ranks.Where(r => r != mainRank)
                    .Select(r => (Rank: r,
                        Value: mainRankChanged && r == type.MainRank ? Staffing.ShareOf(mainRank, r) : Number(row.Cell(9 + Array.IndexOf(Ranks, r))),
                        Current: Staffing.RatioOf(type.MainRank, type.StaffRatios, r)))
                    .ToList();
                if (ratios.All(r => r.Value is not null) && (mainRankChanged || ratios.Any(r => !Same(r.Value!.Value, r.Current))))
                {
                    var target = ObjectAt(json, ["staffRatios"]);
                    target.Remove(mainRank.ToString());
                    foreach (var (rank, value, current) in ratios)
                    {
                        // Un rang sans emploi n'a pas besoin d'être écrit (les rangs sous le rang productif).
                        if (value == 0 && target[rank.ToString()] is null)
                            continue;
                        target[rank.ToString()] = Json(value!.Value);
                        if (!Same(value.Value, current))
                            Changes.Add($"{id} : {RankLabel(rank)} par emploi productif : {Show(current)} → {Show(value.Value)}");
                    }
                }

                // Salaires : les trois écrits dès que l'un change.
                var wages = Ranks.Select((r, i) => (Rank: r, Value: Number(row.Cell(12 + i)), Current: Staffing.WageOf(type.DailyWage, type.Wages, r))).ToList();
                if (wages.All(w => w.Value is not null) && wages.Any(w => !Same(w.Value!.Value, w.Current)))
                {
                    var target = ObjectAt(json, ["wages"]);
                    foreach (var (rank, value, current) in wages)
                    {
                        target[rank.ToString()] = Json(value!.Value);
                        if (!Same(value.Value, current))
                            Changes.Add($"{id} : salaire des {RankLabel(rank)} : {Show(current)} → {Show(value.Value)}");
                    }
                }

                Set(json, "housingPlaces", Number(row.Cell("T"), integer: true), type.HousingPlaces, $"{id} : places de logement");
                Set(json, "dailyRent", Number(row.Cell("U")), type.DailyRent, $"{id} : loyer par place");
                Set(json, "schoolPlaces", Number(row.Cell("V"), integer: true), type.SchoolPlaces, $"{id} : places d'élèves");
                Set(json, "dailyTuition", Number(row.Cell("W")), type.DailyTuition, $"{id} : frais par élève");
                Set(json, "storageCapacity", Number(row.Cell("X"), integer: true), type.StorageCapacity, $"{id} : stockage");
                Set(json, "pollutionPerWorker", Number(row.Cell("Z")), type.PollutionPerWorker, $"{id} : pollution par travailleur");

                // Sorte de stockage, et produit stocké pour un stockage spécifique (le château d'eau garde l'eau).
                var kind = Pick(row.Cell("Y"), Labels.StorageKindOf) ?? type.StorageKind;
                var storedId = Text(row.Cell("AE"));
                if (storedId != "" && !_products.ContainsKey(storedId))
                {
                    Errors.Add($"{Where(row.Cell("AE"))} : produit inconnu « {storedId} ».");
                    continue;
                }

                if (kind == StorageKind.Specific && storedId == "" && type.StorageCapacity > 0
                    && (kind != type.StorageKind || type.StorageProductId is not null))
                {
                    Errors.Add($"{Where(row.Cell("AE"))} : un stockage spécifique doit dire quel produit il garde.");
                    continue;
                }

                if (kind != StorageKind.Specific)
                    storedId = "";
                if (kind != type.StorageKind)
                {
                    if (kind == StorageKind.General)
                        json.Remove("storageKind");
                    else
                        json["storageKind"] = kind.ToString();
                    Changes.Add($"{id} : sorte de stockage {Labels.StorageKindName(type.StorageKind)} → {Labels.StorageKindName(kind)}");
                }

                if (storedId != (type.StorageProductId ?? ""))
                {
                    if (storedId == "")
                        json.Remove("storageProductId");
                    else
                        json["storageProductId"] = storedId;
                    Changes.Add($"{id} : produit stocké {type.StorageProductId ?? "aucun"} → {(storedId == "" ? "aucun" : storedId)}");
                }

                ApplyConstruction(row, id, type, json);
            }
        }

        /// <summary>
        /// Coût de construction : un matériau, sa quantité et la durée du chantier, les trois ensemble ; trois cases
        /// vides pour un type qui ne se construit pas.
        /// </summary>
        private void ApplyConstruction(IXLRow row, string id, BuildingType type, JsonObject json)
        {
            var materialCell = row.Cell("AF");
            var materialId = Text(materialCell);
            var quantity = Number(row.Cell("AG"), optional: true);
            var days = Number(row.Cell("AH"), integer: true, optional: true);
            ConstructionCost? cost = null;
            if (materialId != "" || quantity is not null || days is not null)
            {
                if (materialId == "" || !_products.ContainsKey(materialId))
                {
                    Errors.Add($"{Where(materialCell)} : matériau de construction inconnu « {materialId} ».");
                    return;
                }

                if (quantity is not > 0 || days is not >= 1)
                {
                    Errors.Add($"{Where(row.Cell("AG"))} : un coût de construction a une quantité et une durée d'au moins un jour (videz les trois cases pour un type qui ne se construit pas).");
                    return;
                }

                cost = new ConstructionCost(materialId, quantity.Value, (int)days.Value);
            }

            if (cost == type.Construction)
                return;
            if (cost is null)
                json.Remove("construction");
            else
                json["construction"] = new JsonObject { ["productId"] = cost.ProductId, ["quantity"] = Json(cost.Quantity), ["days"] = cost.Days };
            Changes.Add($"{id} : construction {Describe(type.Construction)} → {Describe(cost)}");

            static string Describe(ConstructionCost? c) => c is null ? "impossible" : $"{Show(c.Quantity)} {c.ProductId} en {c.Days} jours";
        }

        private void ApplyRecipes()
        {
            if (Sheet("Recettes") is not { } sheet)
                return;

            // Recettes déjà vues, par bâtiment : produit et matières (sans les quantités) → cellule de la ligne.
            var seen = new Dictionary<(string TypeId, string Signature), IXLCell>();
            var keyColumn = Layout.Letter(Layout.RecipeKey);
            if (!KeyColumnAt(sheet, 1, keyColumn))
                return;
            foreach (var row in Rows(sheet, 2, keyColumn))
            {
                var key = Text(row.Cell(keyColumn));
                var parts = key.Split('#');
                if (parts.Length != 2 || !_types.TryGetValue(parts[0], out var type) || !int.TryParse(parts[1], out var index)
                    || index < 0 || index >= type.AvailableRecipes.Count || BuildingJson(type.Id) is not { } building)
                {
                    Errors.Add($"{Where(row.Cell(keyColumn))} : recette inconnue « {key} » : régénérez le tableur.");
                    continue;
                }

                var recipe = type.AvailableRecipes[index];
                var json = building["recipes"] is JsonArray recipes ? recipes[index]!.AsObject() : building["production"]!.AsObject();
                var label = $"{type.Id}, recette {recipe.Label}";

                // Le produit d'une recette fait partie de la définition du bâtiment (buildings.json) : il ne se change pas ici.
                if (Text(row.Cell("G")) != recipe.ProductId)
                    Errors.Add($"{Where(row.Cell("G"))} : le produit d'une recette ne se change pas dans le tableur (remettre « {recipe.ProductId} »).");

                Set(json, "quantityPerWorker", Number(row.Cell("H")), recipe.QuantityPerWorker, $"{label} : production par travailleur");

                // Matières : autant que de colonnes dans le tableur (Layout.MaxMaterials) ; les suivantes, s'il y en a, sont gardées.
                var materials = new List<(string ProductId, double Quantity)>();
                var valid = true;
                for (var m = 0; m < Layout.MaxMaterials; m++)
                {
                    var idCell = row.Cell(Layout.RecipeMaterial(m));
                    var quantityCell = row.Cell(Layout.RecipeQuantity(m));
                    var materialId = Text(idCell);
                    if (materialId == "" && quantityCell.Value.IsBlank)
                        continue;
                    if (materialId == "" || !_products.ContainsKey(materialId))
                    {
                        Errors.Add($"{Where(idCell)} : produit inconnu « {materialId} ».");
                        valid = false;
                        continue;
                    }

                    if (Number(quantityCell) is { } quantity)
                        materials.Add((materialId, quantity));
                    else
                        valid = false;
                }

                var old = recipe.Materials.Take(Layout.MaxMaterials).Select(i => (i.ProductId, i.Quantity)).ToList();

                // Deux recettes du même bâtiment ne peuvent pas faire le même produit avec les mêmes matières.
                var finalMaterials = (valid ? materials.Select(m => m.ProductId) : old.Select(m => m.ProductId))
                    .Concat(recipe.Materials.Skip(Layout.MaxMaterials).Select(m => m.ProductId)).Order();
                var signature = $"{recipe.ProductId} ← {string.Join(" + ", finalMaterials)}";
                if (seen.TryGetValue((type.Id, signature), out var twin))
                {
                    Errors.Add($"{Where(row.Cell("I"))} : cette recette de « {type.Id} » devient identique à celle de la ligne {twin.Address.RowNumber} ({signature}).");
                    valid = false;
                }
                else
                    seen[(type.Id, signature)] = row.Cell("I");

                if (!valid || materials.Count == old.Count && materials.Zip(old).All(p => p.First.ProductId == p.Second.ProductId && Same(p.First.Quantity, p.Second.Quantity)))
                    continue;

                var inputs = new JsonArray();
                foreach (var (materialId, quantity) in materials)
                    inputs.Add(new JsonObject { ["productId"] = materialId, ["quantity"] = Json(quantity) });
                foreach (var extra in (json["inputs"] as JsonArray)?.Skip(Layout.MaxMaterials).ToList() ?? [])
                    inputs.Add(extra!.DeepClone());
                if (inputs.Count == 0)
                    json.Remove("inputs");
                else
                    json["inputs"] = inputs;
                Changes.Add($"{label} : matières {Describe(old)} → {Describe(materials)}");
            }
        }

        private static string Describe(List<(string ProductId, double Quantity)> materials) =>
            materials.Count == 0 ? "aucune" : string.Join(" + ", materials.Select(m => $"{Show(m.Quantity)} {m.ProductId}"));

        private void ApplyProducts()
        {
            if (Sheet("Produits") is not { } sheet)
                return;

            foreach (var row in Rows(sheet, 2, "A"))
            {
                var id = Text(row.Cell("A"));
                var json = productsJson.OfType<JsonObject>().FirstOrDefault(p => p["id"]?.GetValue<string>() == id);
                if (json is null || !_products.TryGetValue(id, out var product))
                {
                    Errors.Add($"{Where(row.Cell("A"))} : produit inconnu « {id} ».");
                    continue;
                }

                // Durée de vie : une case vide veut dire « ne se périme pas ».
                var cell = row.Cell("E");
                var life = Number(cell, integer: true, optional: true);
                if (life == 0)
                {
                    Errors.Add($"{Where(cell)} : une durée de vie est d'au moins un jour (videz la case pour un produit qui ne se périme pas).");
                    continue;
                }

                if (life is null && cell.Value.IsBlank && product.ShelfLifeDays is { } old)
                {
                    json.Remove("shelfLifeDays");
                    Changes.Add($"{id} : durée de vie {Show(old)} jours → ne se périme plus");
                }
                else if (life is { } days && (product.ShelfLifeDays is not { } current || !Same(days, current)))
                {
                    json["shelfLifeDays"] = Json(days);
                    Changes.Add($"{id} : durée de vie {(product.ShelfLifeDays is { } c ? $"{Show(c)} jours" : "aucune")} → {Show(days)} jours");
                }
            }
        }

        private void ApplyStartingGame()
        {
            if (Sheet("Partie de départ") is not { } sheet)
                return;

            // Population par rang : partagée entre les classes du rang au prorata de leur population.
            var classesJson = scenarioJson["classes"]!.AsArray().OfType<JsonObject>().ToList();
            for (var row = 2; row <= 4; row++)
            {
                if (!Enum.TryParse<Rank>(Text(sheet.Cell(row, "C")), out var rank))
                {
                    Errors.Add($"{Where(sheet.Cell(row, "C"))} : rang inconnu : régénérez le tableur.");
                    continue;
                }

                var classes = _scenario.Classes.Where(c => c.Rank == rank).ToList();
                var current = classes.Sum(c => c.Population);
                if (Number(sheet.Cell(row, "B"), integer: true) is not { } total || Same(total, current))
                    continue;
                if (classes.Count == 0)
                {
                    Errors.Add($"{Where(sheet.Cell(row, "B"))} : la partie de départ n'a pas de classe de ce rang.");
                    continue;
                }

                var left = (long)total;
                for (var i = 0; i < classes.Count; i++)
                {
                    var share = i == classes.Count - 1
                        ? left
                        : current > 0 ? (long)Math.Round(total * classes[i].Population / current) : (long)total / classes.Count;
                    left -= share;
                    classesJson.First(c => c["id"]?.GetValue<string>() == classes[i].Id)["population"] = share;
                }

                Changes.Add($"partie de départ : population des {RankLabel(rank)} : {Show(current)} → {Show(total)}");
            }

            // Emplois productifs des bâtiments de départ, retrouvés par leur identifiant (colonne de la clé).
            var keyColumn = Layout.Letter(Layout.StartKey);
            var ownerColumn = Layout.Letter(Layout.StartOwnerClass);
            if (!KeyColumnAt(sheet, 7, keyColumn))
                return;
            var buildingsJsonList = scenarioJson["buildings"]!.AsArray().OfType<JsonObject>().ToList();
            foreach (var row in Rows(sheet, 8, keyColumn))
            {
                var id = Text(row.Cell(keyColumn));
                var building = _scenario.Buildings.FirstOrDefault(b => b.Id == id);
                var json = buildingsJsonList.FirstOrDefault(b => b["id"]?.GetValue<string>() == id);
                if (building is null || json is null || !_types.TryGetValue(building.Type, out var type))
                {
                    Errors.Add($"{Where(row.Cell(keyColumn))} : bâtiment de départ inconnu « {id} ».");
                    continue;
                }

                var label = $"partie de départ, {building.Name ?? id}";
                Set(json, "jobs", Number(row.Cell("F"), integer: true), building.Jobs ?? type.Jobs, $"{label} : emplois productifs");

                // Produit fabriqué : une des recettes du type.
                var productCell = row.Cell("H");
                var productId = Text(productCell);
                var currentProduct = building.Product ?? type.DefaultProduction?.ProductId;
                if (productId != "" && productId != currentProduct)
                {
                    if (type.AvailableRecipes.All(r => r.ProductId != productId))
                        Errors.Add($"{Where(productCell)} : un bâtiment « {type.Id} » ne sait pas fabriquer « {productId} ».");
                    else
                    {
                        json["product"] = productId;
                        Changes.Add($"{label} : produit {currentProduct} → {productId}");
                    }
                }

                // Propriétaire : l'État, ou une coopérative ou une entreprise privée et sa classe propriétaire.
                if (Pick(row.Cell("C"), Labels.OwnershipOf) is not { } ownership)
                    continue;
                var ownerName = Text(row.Cell(ownerColumn));
                string? ownerId = null;
                if (ownership != Ownership.Public)
                {
                    ownerId = _scenario.Classes.FirstOrDefault(c => c.Name == ownerName)?.Id;
                    if (ownerId is null)
                    {
                        Errors.Add(ownerName == ""
                            ? $"{Where(row.Cell(ownerColumn))} : un bâtiment {(ownership == Ownership.Private ? "privé" : "coopératif")} doit avoir une classe propriétaire."
                            : $"{Where(row.Cell(ownerColumn))} : classe inconnue « {ownerName} ».");
                        continue;
                    }
                }

                if (ownership == building.Ownership && ownerId == (ownership == Ownership.Public ? null : building.Owner))
                    continue;
                if (ownership == Ownership.Public)
                {
                    json.Remove("ownership");
                    json.Remove("owner");
                }
                else
                {
                    json["ownership"] = ownership.ToString();
                    json["owner"] = ownerId;
                }

                string Describe(Ownership o, string? owner) =>
                    o == Ownership.Public ? Labels.OwnershipName(o)
                    : $"{Labels.OwnershipName(o)} ({_scenario.Classes.FirstOrDefault(c => c.Id == owner)?.Name ?? owner})";
                Changes.Add($"{label} : propriétaire {Describe(building.Ownership, building.Owner)} → {Describe(ownership, ownerId)}");
            }
        }

        private static string RankLabel(Rank rank) => rank switch
        {
            Rank.Workers => "ouvriers",
            Rank.Intermediate => "prof. intermédiaires",
            _ => "cadres",
        };
    }
}

/// <summary>
/// Écrit les fichiers de game/data dans leur forme habituelle : une ligne par bâtiment, produit, classe ou loi, et les
/// accents tels quels.
/// </summary>
public static class JsonFormat
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <param name="original">Le texte d'origine du fichier, dont on garde les lignes vides entre les éléments.</param>
    public static string Write(JsonNode root, string original)
    {
        var text = new System.Text.StringBuilder();
        if (root is JsonArray array)
        {
            // Une liste de premier niveau (bâtiments, produits) : une ligne par élément, et les lignes vides d'origine,
            // retrouvées par l'identifiant de l'élément qui les suit. Un nouvel élément qui ouvre une nouvelle catégorie
            // en reçoit une aussi.
            var (known, blankBefore) = BlankLinesBefore(original);
            text.Append("[\n");
            for (var i = 0; i < array.Count; i++)
            {
                var id = array[i]?["id"]?.ToString() ?? "";
                var category = array[i]?["category"]?.ToString();
                var newGroup = i > 0 && !known.Contains(id) && category is not null && category != array[i - 1]?["category"]?.ToString();
                if (blankBefore.Contains(id) || newGroup)
                    text.Append('\n');
                text.Append("  ").Append(Compact(array[i])).Append(i < array.Count - 1 ? ",\n" : "\n");
            }

            text.Append("]\n");
            return text.ToString();
        }

        var properties = root.AsObject().ToList();
        text.Append("{\n");
        for (var i = 0; i < properties.Count; i++)
        {
            var (key, value) = properties[i];
            text.Append($"  \"{key}\": ");
            if (value is JsonArray list)
                text.Append(list.Count == 0 ? "[]" : "[\n" + string.Join(",\n", list.Select(e => "    " + Compact(e))) + "\n  ]");
            else if (value is JsonObject obj && obj.Count > 0 && obj.All(p => p.Value is JsonObject))
                text.Append("{\n" + string.Join(",\n", obj.Select(p => $"    \"{p.Key}\": {Compact(p.Value)}")) + "\n  }");
            else
                text.Append(Compact(value));
            text.Append(i < properties.Count - 1 ? ",\n" : "\n");
        }

        text.Append("}\n");
        return text.ToString();
    }

    /// <summary>
    /// Les identifiants des éléments du fichier d'origine, et ceux précédés d'une ligne vide (un élément par ligne).
    /// </summary>
    private static (HashSet<string> Known, HashSet<string> BlankBefore) BlankLinesBefore(string original)
    {
        var known = new HashSet<string>();
        var result = new HashSet<string>();
        var lines = original.Replace("\r", "").Split('\n').Skip(1).TakeWhile(l => l.Trim() != "]").ToList();
        var blank = false;
        foreach (var line in lines)
        {
            if (line.Trim() == "")
            {
                blank = true;
                continue;
            }

            var match = System.Text.RegularExpressions.Regex.Match(line, "\"id\": \"([^\"]*)\"");
            var id = match.Success ? match.Groups[1].Value : "";
            known.Add(id);
            if (blank)
                result.Add(id);
            blank = false;
        }

        return (known, result);
    }

    private static string Compact(JsonNode? node, string? key = null) => node switch
    {
        JsonObject obj when obj.Count == 0 => "{}",
        JsonObject obj => "{ " + string.Join(", ", obj.Select(p => $"\"{p.Key}\": {Compact(p.Value, p.Key)}")) + " }",
        JsonArray list when list.Count == 0 => "[]",
        // Les matières d'une recette s'écrivent serrées : [{ … }].
        JsonArray list when key == "inputs" => "[" + string.Join(", ", list.Select(e => Compact(e))) + "]",
        JsonArray list => "[ " + string.Join(", ", list.Select(e => Compact(e))) + " ]",
        null => "null",
        _ => node.ToJsonString(Options),
    };
}
