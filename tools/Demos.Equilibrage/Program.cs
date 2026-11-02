using ClosedXML.Excel;
using Demos.Application;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;
using Demos.Domain.Shared;
using Demos.Infrastructure;

// Génère le tableur d'équilibrage à partir des données du jeu (game/data) et de la partie de départ.
// Usage : dotnet run --project tools/Demos.Equilibrage -- [dossier game/data] [fichier .xlsx]
// Les cellules jaunes sont des réglages ; tout le reste est calculé par des formules à partir d'elles.

var dataDirectory = args.Length > 0 ? args[0] : FindDataDirectory();
var outputPath = args.Length > 1
    ? args[1]
    : Path.Combine(Directory.GetParent(Directory.GetParent(dataDirectory)!.FullName)!.Parent!.FullName, "equilibrage demos.xlsx");

var content = new JsonContentSource(file => File.ReadAllText(Path.Combine(dataDirectory, file)));
var world = PrototypeScenario.Create(content).World;
var balance = Balance.Current;

using var workbook = new XLWorkbook();
var catalogueWoodPrice = content.LoadProducts().First(p => p.Id == Energy.DefaultHeating).BasePrice;
var book = new Book(workbook, world, balance, catalogueWoodPrice);
book.WriteReadMe();
book.WriteConstants();
book.WriteBuildings();
book.WriteProducts();
book.WriteRecipes();
book.WriteNeeds();
book.WriteStartingGame();

// Les bâtiments sont écrits avant les produits (les prix s'y réfèrent), mais rangés après.
workbook.Worksheet("Produits").Position = 3;
workbook.SaveAs(outputPath);
Console.WriteLine($"Tableur d'équilibrage écrit : {outputPath}");

static string FindDataDirectory()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "game", "data");
        if (Directory.Exists(candidate))
            return candidate;
    }

    throw new DirectoryNotFoundException("Dossier game/data introuvable : lancez l'outil depuis le dépôt, ou donnez le chemin.");
}

/// <summary>Écrit les feuilles du tableur et garde l'adresse des cellules utiles aux formules.</summary>
internal sealed class Book(XLWorkbook workbook, World world, BalanceSettings balance, double catalogueWoodPrice)
{
    private static readonly XLColor InputColor = XLColor.FromHtml("#FFF4C2");
    private static readonly XLColor HeaderColor = XLColor.FromHtml("#DCE6F1");

    // Adresse absolue des constantes, par clé (par exemple « Marge » → Constantes!$B$3).
    private readonly Dictionary<string, string> _constants = [];

    // Feuille Produits : ligne de chaque produit, et plages pour les recherches.
    private readonly Dictionary<string, int> _productRows = [];
    private string _productIds = "";
    private string _productPrices = "";

    // Feuille Besoins : ligne de chaque besoin, et produit principal de chaque besoin.
    private readonly Dictionary<NeedCategory, int> _needRows = [];
    private readonly Dictionary<NeedCategory, string> _mainProductOfNeed = [];

    private static readonly Rank[] Ranks = [Rank.Workers, Rank.Intermediate, Rank.Executives];

    private static string RankName(Rank rank) => rank switch
    {
        Rank.Workers => "Ouvriers",
        Rank.Intermediate => "Prof. intermédiaires",
        _ => "Cadres",
    };

    private static string NeedName(NeedCategory need) => need switch
    {
        NeedCategory.Food => "Se nourrir",
        NeedCategory.Clothing => "S'habiller",
        NeedCategory.Furniture => "Se meubler",
        NeedCategory.Leisure => "Se distraire",
        NeedCategory.Administration => "Être administré",
        NeedCategory.Security => "Être protégé",
        NeedCategory.Comfort => "Confort",
        _ => need.ToString(),
    };

    private string C(string key) => _constants[key];

    private string Price(string productCell) => $"IFERROR(INDEX({_productPrices},MATCH({productCell},{_productIds},0)),0)";

    // Feuille Bâtiments : dernière ligne, pour les recherches par identifiant de type.
    private int _buildingLast;

    private string BuildingColumn(string column) => $"'Bâtiments'!${column}$2:${column}${_buildingLast}";

    /// <summary>Valeur d'une colonne de la feuille Bâtiments pour un type (par son identifiant).</summary>
    private string Lookup(string typeIdCell, string column) =>
        $"IFERROR(INDEX({BuildingColumn(column)},MATCH({typeIdCell},{BuildingColumn("A")},0)),0)";

    // Colonnes de la feuille Bâtiments : proportion d'emplois et salaire de chaque rang, salaires par emploi productif.
    private static readonly string[] RatioColumns = ["I", "J", "K"];
    private static readonly string[] WageColumns = ["L", "M", "N"];
    private const string WageBillColumn = "O";

    /// <summary>Énergie d'une unité (emploi ou place) sur l'année, pour un bâtiment de la liste 1.</summary>
    private string EnergyPerUnitFormula(int tier, bool constantHeating, string productCell)
    {
        if (tier != 1)
            return "0";

        // Comme le jeu : le bois et l'eau comptent leur chauffage au prix de catalogue du bois, pour éviter une boucle.
        var factor = constantHeating ? "1" : C("FacteurMoyen");
        var fuelPrice = $"IF(OR({productCell}=\"{Energy.DefaultHeating}\",{productCell}=\"{Energy.Water}\"),{C("PrixCatalogueBois")},{C("PrixCombustible")})";
        return $"{C("Chauffage")}*{factor}*{fuelPrice}+IF({productCell}=\"{Energy.Water}\",0,{C("Eau")}*{C("PrixEau")})";
    }

    private static void Header(IXLWorksheet sheet, int row, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = titles[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderColor;
            cell.Style.Alignment.WrapText = true;
        }
    }

    private static void Input(IXLCell cell, XLCellValue value)
    {
        cell.Value = value;
        cell.Style.Fill.BackgroundColor = InputColor;
    }

    private static void Finish(IXLWorksheet sheet, int headerRow)
    {
        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns().AdjustToContents(headerRow, headerRow + 200, 8, 40);
        sheet.Row(headerRow).Height = 32;
    }

    public void WriteReadMe()
    {
        var sheet = workbook.AddWorksheet("Lisez-moi");
        string[] lines =
        [
            "Tableur d'équilibrage de Projet Démos",
            "",
            "Généré depuis les données du jeu (game/data : produits, bâtiments, equilibrage.json) et la partie de départ.",
            "Les cellules jaunes sont des réglages : changez-les pour voir l'effet ; tout le reste se recalcule par formules.",
            "Pour reporter un réglage dans le jeu, il faut le modifier dans game/data (le tableur ne réécrit pas encore les fichiers).",
            "",
            "Feuilles :",
            "- Constantes : marge, salaires, encadrement, énergie, saisons, impôts, réserves…",
            "- Produits : prix recalculé avec la règle des 30 % (salaires, énergie, matières), comparé au prix du jeu.",
            "- Bâtiments : tous les types du catalogue, avec leurs emplois, coûts, places, stockage et énergie.",
            "- Recettes : chaque recette à plein régime : production, valeur, coûts et bénéfice par jour.",
            "- Besoins : quantités par rang, coût de la vie par jour comparé au salaire de chaque rang.",
            "- Partie de départ : bâtiments de départ, production comparée à la demande, emplois comparés à la population.",
            "",
            "Les prix recalculés suivent les mêmes règles que le jeu ; la colonne Écart doit rester à 0 tant que rien n'est changé.",
            "",
            "Pour régénérer : dotnet run --project tools/Demos.Equilibrage (depuis le dossier demos).",
        ];
        for (var i = 0; i < lines.Length; i++)
            sheet.Cell(i + 1, 1).Value = lines[i];
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Column(1).Width = 110;
    }

    public void WriteConstants()
    {
        var sheet = workbook.AddWorksheet("Constantes");
        Header(sheet, 1, "Réglage", "Valeur", "Explication");

        var averageRent = world.Structures.Values.Where(s => s.HousingPlaces > 0) is var housing && housing.Sum(s => s.HousingPlaces) > 0
            ? housing.Sum(s => s.HousingPlaces * s.DailyRent) / housing.Sum(s => s.HousingPlaces)
            : 0;

        (string Key, string Label, object Value, string Note)[] rows =
        [
            ("Marge", "Marge", balance.Margin, "Bénéfice d'un bâtiment de référence à plein régime : fixe les prix."),
            ("Encadrement", "Encadrement par emploi (par défaut)", balance.SupervisorsPerJob, "Pour un bâtiment sans proportions propres ; voir la feuille Bâtiments."),
            ("Direction", "Direction par emploi (par défaut)", balance.DirectorsPerJob, "Pour un bâtiment sans proportions propres."),
            ("MultOuvriers", "Multiplicateur de salaire : ouvriers (par défaut)", balance.WageMultipliers[Rank.Workers], "Pour un bâtiment sans salaires propres ; voir la feuille Bâtiments."),
            ("MultPI", "Multiplicateur de salaire : prof. intermédiaires (par défaut)", balance.WageMultipliers[Rank.Intermediate], ""),
            ("MultCadres", "Multiplicateur de salaire : cadres (par défaut)", balance.WageMultipliers[Rank.Executives], ""),
            ("Chauffage", "Chauffage par emploi ou place et par jour", balance.HeatingPerUnit, "Bois ou charbon, au printemps et en automne."),
            ("Eau", "Eau par emploi ou place et par jour", balance.WaterPerUnit, ""),
            ("Hiver", "Chauffage en hiver (×)", balance.HeatingBySeason[Season.Winter], ""),
            ("Printemps", "Chauffage au printemps (×)", balance.HeatingBySeason[Season.Spring], ""),
            ("Ete", "Chauffage en été (×)", balance.HeatingBySeason[Season.Summer], ""),
            ("Automne", "Chauffage en automne (×)", balance.HeatingBySeason[Season.Autumn], ""),
            ("FacteurMoyen", "Chauffage moyen sur l'année (×)", "=MOYENNE", "Calculé."),
            ("SansEnergie", "Activité sans énergie", balance.OutputWithoutEnergy, "Part de l'activité gardée sans chauffage ni eau."),
            ("ReserveCombustible", "Réserve de combustible (jours)", balance.FuelReserveDays, "Jours de chauffage normal gardés dans chaque bâtiment."),
            ("ReserveEntreprise", "Réserve des entreprises (jours)", balance.CompanyReserveDays, "Jours de salaires et d'énergie gardés en caisse."),
            ("ImpotEtat", "Impôt d'État (par jour)", balance.StateTaxRate, "Part de la richesse versée chaque jour au trésor."),
            ("ImpotCommune", "Impôt communal (par jour)", balance.CommuneTaxRate, "Part de la richesse versée chaque jour à la caisse de la commune."),
            ("Transport", "Perte en route entre communes", balance.TransportLoss, ""),
            ("PollutionLoin", "Part de la pollution des autres communes", balance.DistantPollutionShare, ""),
            ("SeuilPollution", "Seuil de pollution (par habitant et par jour)", balance.PollutedThreshold, ""),
            ("Dotation", "Dotation d'une nouvelle commune (jours)", balance.FoundingGrantDays, "Jours de salaires du camp de base."),
            ("PrixCombustible", "Prix du combustible de référence (bois)", world.PriceOf(Energy.DefaultHeating), "Prix du jeu ; sert au coût du chauffage."),
            ("PrixEau", "Prix de l'eau", world.PriceOf(Energy.Water), "Prix du jeu ; sert au coût de l'eau."),
            ("PrixCatalogueBois", "Prix de catalogue du bois", catalogueWoodPrice,
                "Le bois et l'eau comptent leur chauffage à ce prix, comme le jeu (pour éviter une boucle de calcul)."),
            ("LoyerMoyen", "Loyer moyen d'une place (partie de départ)", averageRent, "Sert au coût du logement dans la feuille Besoins."),
        ];

        for (var i = 0; i < rows.Length; i++)
        {
            var (key, label, value, note) = rows[i];
            var row = i + 2;
            sheet.Cell(row, 1).Value = label;
            if (key == "FacteurMoyen")
                sheet.Cell(row, 2).FormulaA1 = $"AVERAGE({C("Hiver")},{C("Printemps")},{C("Ete")},{C("Automne")})";
            else
                Input(sheet.Cell(row, 2), Convert.ToDouble(value));
            sheet.Cell(row, 3).Value = note;
            _constants[key] = $"Constantes!$B${row}";
        }

        Finish(sheet, 1);
    }

    public void WriteProducts()
    {
        var sheet = workbook.AddWorksheet("Produits");
        Header(sheet, 1, "Id", "Nom", "Besoin", "Stockable", "Durée de vie (jours)", "Bâtiment de référence", "Liste",
            "Rang productif", "Salaires par emploi productif (feuille Bâtiments)", "Production par travailleur", "Matière 1",
            "Quantité 1", "Matière 2", "Quantité 2", "Coût salarial par emploi", "Énergie par emploi", "Coût des matières",
            "Prix recalculé", "Prix du jeu", "Écart");

        var products = world.Products;
        for (var i = 0; i < products.Count; i++)
            _productRows[products[i].Id] = i + 2;
        var last = products.Count + 1;
        _productIds = $"Produits!$A$2:$A${last}";
        _productPrices = $"Produits!$R$2:$R${last}";

        foreach (var product in products)
        {
            var row = _productRows[product.Id];
            sheet.Cell(row, 1).Value = product.Id;
            sheet.Cell(row, 2).Value = product.Name;
            sheet.Cell(row, 3).Value = product.Need is { } need ? NeedName(need) : "";
            sheet.Cell(row, 4).Value = product.Stockable ? "oui" : "non";
            if (product.ShelfLifeDays is { } life)
                Input(sheet.Cell(row, 5), life);
            sheet.Cell(row, 19).Value = world.PriceOf(product.Id);

            if (Pricing.ReferenceFor(product.Id, world.BuildingTypes.Values) is not { } reference)
            {
                sheet.Cell(row, 6).Value = "—";
                sheet.Cell(row, 18).FormulaA1 = $"S{row}";
                sheet.Cell(row, 20).FormulaA1 = $"R{row}-S{row}";
                continue;
            }

            var (type, recipe) = reference;
            sheet.Cell(row, 6).Value = type.Id;
            sheet.Cell(row, 7).Value = type.Tier;
            sheet.Cell(row, 8).Value = RankName(type.MainRank);
            sheet.Cell(row, 9).FormulaA1 = Lookup($"F{row}", WageBillColumn);
            Input(sheet.Cell(row, 10), recipe.QuantityPerWorker);
            for (var m = 0; m < Math.Min(2, recipe.Materials.Count); m++)
            {
                sheet.Cell(row, 11 + 2 * m).Value = recipe.Materials[m].ProductId;
                Input(sheet.Cell(row, 12 + 2 * m), recipe.Materials[m].Quantity);
            }

            sheet.Cell(row, 15).FormulaA1 = $"I{row}";
            sheet.Cell(row, 16).FormulaA1 = EnergyPerUnitFormula(type.Tier, type.ConstantHeating, $"A{row}");
            sheet.Cell(row, 17).FormulaA1 = $"L{row}*{Price($"K{row}")}+N{row}*{Price($"M{row}")}";
            sheet.Cell(row, 18).FormulaA1 = $"(1+{C("Marge")})*((O{row}+P{row})/J{row}+Q{row})";
            sheet.Cell(row, 20).FormulaA1 = $"R{row}-S{row}";
        }

        sheet.Range(2, 15, last, 20).Style.NumberFormat.Format = "0.00";
        Finish(sheet, 1);
    }

    public void WriteBuildings()
    {
        var sheet = workbook.AddWorksheet("Bâtiments");
        Header(sheet, 1, "Id", "Nom", "Catégorie", "Niveau", "Liste", "Chauffage constant", "Rang productif", "Emplois productifs",
            "Ouvriers par emploi productif", "Prof. intermédiaires par emploi productif", "Cadres par emploi productif",
            "Salaire : ouvriers", "Salaire : prof. intermédiaires", "Salaire : cadres", "Salaires par emploi productif",
            "Emplois d'ouvriers", "Emplois de prof. intermédiaires", "Emplois de cadres", "Salaires par jour",
            "Places de logement", "Loyer par place", "Places d'élèves", "Frais par élève", "Stockage", "Sorte de stockage",
            "Pollution par travailleur", "Unités d'énergie", "Chauffage par jour (moyen)", "Eau par jour", "Recettes");

        var types = world.BuildingTypes.Values.OrderBy(t => t.Category).ThenBy(t => t.Level).ToList();
        _buildingLast = types.Count + 1;
        var row = 2;
        foreach (var type in types)
        {
            sheet.Cell(row, 1).Value = type.Id;
            sheet.Cell(row, 2).Value = type.Name;
            sheet.Cell(row, 3).Value = type.Category;
            sheet.Cell(row, 4).Value = type.Level;
            Input(sheet.Cell(row, 5), type.Tier);
            sheet.Cell(row, 6).Value = type.ConstantHeating ? "oui" : "";
            sheet.Cell(row, 7).Value = RankName(type.MainRank);
            Input(sheet.Cell(row, 8), type.Jobs);
            for (var r = 0; r < Ranks.Length; r++)
            {
                // Le rang productif compte 1 par emploi productif ; les autres rangs se règlent.
                var ratio = Staffing.RatioOf(type.MainRank, type.StaffRatios, Ranks[r]);
                if (Ranks[r] == type.MainRank)
                    sheet.Cell(row, 9 + r).Value = ratio;
                else
                    Input(sheet.Cell(row, 9 + r), ratio);
                Input(sheet.Cell(row, 12 + r), Staffing.WageOf(type.DailyWage, type.Wages, Ranks[r]));
                sheet.Cell(row, 16 + r).FormulaA1 = $"H{row}*{RatioColumns[r]}{row}";
            }

            sheet.Cell(row, 15).FormulaA1 = $"I{row}*L{row}+J{row}*M{row}+K{row}*N{row}";
            sheet.Cell(row, 19).FormulaA1 = $"H{row}*O{row}";
            Input(sheet.Cell(row, 20), type.HousingPlaces);
            Input(sheet.Cell(row, 21), type.DailyRent);
            Input(sheet.Cell(row, 22), type.SchoolPlaces);
            Input(sheet.Cell(row, 23), type.DailyTuition);
            Input(sheet.Cell(row, 24), type.StorageCapacity);
            sheet.Cell(row, 25).Value = type.StorageCapacity > 0
                ? type.StorageKind + (type.StorageProductId is { } stored ? $" ({stored})" : "")
                : "";
            Input(sheet.Cell(row, 26), type.PollutionPerWorker);
            sheet.Cell(row, 27).FormulaA1 = $"H{row}+T{row}+V{row}";
            var factor = type.ConstantHeating ? "1" : C("FacteurMoyen");
            sheet.Cell(row, 28).FormulaA1 = $"IF(E{row}=1,AA{row}*{C("Chauffage")}*{factor},0)";
            var producesWater = type.AvailableRecipes.Any(r => r.ProductId == Energy.Water);
            sheet.Cell(row, 29).FormulaA1 = producesWater ? "0" : $"IF(E{row}=1,AA{row}*{C("Eau")},0)";
            sheet.Cell(row, 30).Value = string.Join(" ; ", type.AvailableRecipes.Select(r => $"{r.Label} ({r.QuantityPerWorker})"));
            row++;
        }

        sheet.Range(2, 15, row - 1, 19).Style.NumberFormat.Format = "0.00";
        sheet.Range(2, 28, row - 1, 29).Style.NumberFormat.Format = "0.00";
        Finish(sheet, 1);
    }

    public void WriteRecipes()
    {
        var sheet = workbook.AddWorksheet("Recettes");
        Header(sheet, 1, "Bâtiment", "Niveau", "Liste", "Rang productif", "Emplois productifs",
            "Salaires par emploi productif (feuille Bâtiments)", "Produit",
            "Production par travailleur", "Matière 1", "Quantité 1", "Matière 2", "Quantité 2", "Production par jour",
            "Valeur par jour", "Matières par jour", "Salaires par jour", "Énergie par jour", "Bénéfice par jour", "Marge réelle");

        var row = 2;
        foreach (var type in world.BuildingTypes.Values.Where(t => t.Jobs > 0).OrderBy(t => t.Category).ThenBy(t => t.Level))
        {
            foreach (var recipe in type.AvailableRecipes)
            {
                sheet.Cell(row, 1).Value = type.Id;
                sheet.Cell(row, 2).Value = type.Level;
                sheet.Cell(row, 3).Value = type.Tier;
                sheet.Cell(row, 4).Value = RankName(type.MainRank);
                Input(sheet.Cell(row, 5), type.Jobs);
                sheet.Cell(row, 6).FormulaA1 = Lookup($"A{row}", WageBillColumn);
                sheet.Cell(row, 7).Value = recipe.ProductId;
                Input(sheet.Cell(row, 8), recipe.QuantityPerWorker);
                for (var m = 0; m < Math.Min(2, recipe.Materials.Count); m++)
                {
                    sheet.Cell(row, 9 + 2 * m).Value = recipe.Materials[m].ProductId;
                    Input(sheet.Cell(row, 10 + 2 * m), recipe.Materials[m].Quantity);
                }

                sheet.Cell(row, 13).FormulaA1 = $"E{row}*H{row}";
                sheet.Cell(row, 14).FormulaA1 = $"M{row}*{Price($"G{row}")}";
                sheet.Cell(row, 15).FormulaA1 = $"M{row}*(J{row}*{Price($"I{row}")}+L{row}*{Price($"K{row}")})";
                sheet.Cell(row, 16).FormulaA1 = $"E{row}*F{row}";
                sheet.Cell(row, 17).FormulaA1 = $"E{row}*({EnergyPerUnitFormula(type.Tier, type.ConstantHeating, $"G{row}")})";
                sheet.Cell(row, 18).FormulaA1 = $"N{row}-O{row}-P{row}-Q{row}";
                sheet.Cell(row, 19).FormulaA1 = $"IFERROR(R{row}/(O{row}+P{row}+Q{row}),0)";
                row++;
            }
        }

        sheet.Range(2, 13, row - 1, 18).Style.NumberFormat.Format = "0.00";
        sheet.Range(2, 19, row - 1, 19).Style.NumberFormat.Format = "0%";
        Finish(sheet, 1);
    }

    public void WriteNeeds()
    {
        var sheet = workbook.AddWorksheet("Besoins");
        Header(sheet, 1, "Besoin", "Produit principal (le moins cher produit au départ)", "Prix", RankName(Rank.Workers), RankName(Rank.Intermediate),
            RankName(Rank.Executives));

        var needs = Consumption.AllNeeds.ToList();
        var row = 2;
        foreach (var need in needs)
        {
            // Le produit principal : le moins cher parmi ceux que la partie de départ produit, sinon le moins cher de tous.
            var produced = world.Structures.Values.Select(s => s.Production?.ProductId).ToHashSet();
            var candidates = world.Products.Where(p => p.Need == need).OrderBy(p => world.PriceOf(p.Id)).ToList();
            var main = candidates.FirstOrDefault(p => produced.Contains(p.Id)) ?? candidates.FirstOrDefault();
            _needRows[need] = row;
            if (main is not null)
                _mainProductOfNeed[need] = main.Id;

            sheet.Cell(row, 1).Value = NeedName(need);
            sheet.Cell(row, 2).Value = main?.Id ?? "aucun";
            sheet.Cell(row, 3).FormulaA1 = Price($"B{row}");
            for (var r = 0; r < Ranks.Length; r++)
                Input(sheet.Cell(row, 4 + r), Consumption.NeedsOf(Ranks[r]).GetValueOrDefault(need));
            row++;
        }

        sheet.Cell(row, 1).Value = "Se loger (places)";
        sheet.Cell(row, 2).Value = "loyer moyen";
        sheet.Cell(row, 3).FormulaA1 = C("LoyerMoyen");
        for (var r = 0; r < Ranks.Length; r++)
            Input(sheet.Cell(row, 4 + r), Housing.PlacesPerPerson(Ranks[r]));
        var lastNeedRow = row;
        row += 2;

        sheet.Cell(row, 1).Value = "Coût des besoins par jour";
        sheet.Cell(row + 1, 1).Value = "Salaire par jour dans une ferme (feuille Bâtiments)";
        sheet.Cell(row + 2, 1).Value = "Part du salaire";
        for (var r = 0; r < Ranks.Length; r++)
        {
            var column = XLHelper.GetColumnLetterFromNumber(4 + r);
            sheet.Cell(row, 4 + r).FormulaA1 = $"SUMPRODUCT($C$2:$C${lastNeedRow},{column}2:{column}{lastNeedRow})";
            sheet.Cell(row + 1, 4 + r).FormulaA1 = Lookup("\"ferme\"", WageColumns[r]);
            sheet.Cell(row + 2, 4 + r).FormulaA1 = $"{column}{row}/{column}{row + 1}";
        }

        sheet.Range(row, 1, row + 2, 1).Style.Font.Bold = true;
        sheet.Range(2, 3, row + 1, 6).Style.NumberFormat.Format = "0.000";
        sheet.Range(row + 2, 4, row + 2, 6).Style.NumberFormat.Format = "0%";
        Finish(sheet, 1);
    }

    public void WriteStartingGame()
    {
        var sheet = workbook.AddWorksheet("Partie de départ");

        // Population par rang.
        sheet.Cell(1, 1).Value = "Population de départ";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        var populationCells = new Dictionary<Rank, string>();
        for (var r = 0; r < Ranks.Length; r++)
        {
            sheet.Cell(2 + r, 1).Value = RankName(Ranks[r]);
            Input(sheet.Cell(2 + r, 2), world.Classes.Where(c => c.Rank == Ranks[r]).Sum(c => c.Population));
            populationCells[Ranks[r]] = $"$B${2 + r}";
        }

        // Bâtiments de départ.
        const int header = 7;
        sheet.Cell(header - 1, 1).Value = "Bâtiments de départ (à plein régime)";
        sheet.Cell(header - 1, 1).Style.Font.Bold = true;
        Header(sheet, header, "Nom", "Type", "Propriétaire", "Liste", "Rang productif", "Emplois productifs", "Salaires par emploi productif (feuille Bâtiments)",
            "Produit", "Production par travailleur", "Matière 1", "Quantité 1", "Matière 2", "Quantité 2", "Production par jour",
            "Matière 1 par jour", "Matière 2 par jour", "Combustible", "Unités d'énergie", "Chauffage par jour (moyen)", "Eau par jour",
            "Emplois ouvriers", "Emplois prof. intermédiaires", "Emplois cadres", "Valeur par jour", "Matières par jour",
            "Salaires par jour", "Énergie par jour", "Loyers et frais par jour", "Bénéfice par jour");

        var buildings = world.Structures.Values.Where(s => s.Kind == StructureKind.Building).ToList();
        var row = header + 1;
        foreach (var building in buildings)
        {
            var type = building.TypeId is { } typeId ? world.BuildingTypes.GetValueOrDefault(typeId) : null;
            var tier = world.TierOf(building);
            var owner = building.Ownership == Ownership.Public
                ? "État"
                : $"{(building.Ownership == Ownership.Cooperative ? "Coopérative" : "Privé")} : "
                  + (building.OwnerClassId is { } ownerId && world.ClassesById.TryGetValue(ownerId, out var o) ? o.Name : "?");

            sheet.Cell(row, 1).Value = building.Name;
            sheet.Cell(row, 2).Value = type?.Id ?? "—";
            sheet.Cell(row, 3).Value = owner;
            sheet.Cell(row, 4).Value = tier;
            sheet.Cell(row, 5).Value = RankName(building.MainRank);
            Input(sheet.Cell(row, 6), building.Jobs);
            sheet.Cell(row, 7).FormulaA1 = Lookup($"B{row}", WageBillColumn);
            if (building.Production is { } recipe)
            {
                sheet.Cell(row, 8).Value = recipe.ProductId;
                Input(sheet.Cell(row, 9), recipe.QuantityPerWorker);
                for (var m = 0; m < Math.Min(2, recipe.Materials.Count); m++)
                {
                    sheet.Cell(row, 10 + 2 * m).Value = recipe.Materials[m].ProductId;
                    Input(sheet.Cell(row, 11 + 2 * m), recipe.Materials[m].Quantity);
                }
            }

            sheet.Cell(row, 14).FormulaA1 = $"F{row}*I{row}";
            sheet.Cell(row, 15).FormulaA1 = $"N{row}*K{row}";
            sheet.Cell(row, 16).FormulaA1 = $"N{row}*M{row}";
            sheet.Cell(row, 17).Value = tier == 1 ? building.HeatingProductId ?? Energy.DefaultHeating : "";
            sheet.Cell(row, 18).FormulaA1 = $"F{row}+{building.HousingPlaces}+{building.SchoolPlaces}";
            var factor = world.HeatsAllYear(building) ? "1" : C("FacteurMoyen");
            sheet.Cell(row, 19).FormulaA1 = $"IF(D{row}=1,R{row}*{C("Chauffage")}*{factor},0)";
            sheet.Cell(row, 20).FormulaA1 = $"IF(AND(D{row}=1,H{row}<>\"eau\"),R{row}*{C("Eau")},0)";
            for (var r = 0; r < Ranks.Length; r++)
                sheet.Cell(row, 21 + r).FormulaA1 = $"F{row}*{Lookup($"B{row}", RatioColumns[r])}";
            sheet.Cell(row, 24).FormulaA1 = $"N{row}*{Price($"H{row}")}";
            sheet.Cell(row, 25).FormulaA1 = $"O{row}*{Price($"J{row}")}+P{row}*{Price($"L{row}")}";
            sheet.Cell(row, 26).FormulaA1 = $"F{row}*G{row}";
            sheet.Cell(row, 27).FormulaA1 = $"S{row}*{Price($"Q{row}")}+T{row}*{C("PrixEau")}";
            sheet.Cell(row, 28).Value = building.HousingPlaces * building.DailyRent + building.SchoolPlaces * building.DailyTuition;
            sheet.Cell(row, 29).FormulaA1 = $"X{row}-Y{row}-Z{row}-AA{row}+AB{row}";
            row++;
        }

        var first = header + 1;
        var last = row - 1;
        sheet.Range(first, 14, last, 29).Style.NumberFormat.Format = "0.00";
        string Col(string column) => $"${column}${first}:${column}${last}";

        // Emplois comparés à la population.
        row += 2;
        sheet.Cell(row, 1).Value = "Emplois et population";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        Header(sheet, row + 1, "Rang", "Emplois", "Population", "Sans emploi", "Taux de chômage");
        string[] jobColumns = ["U", "V", "W"];
        for (var r = 0; r < Ranks.Length; r++)
        {
            var line = row + 2 + r;
            sheet.Cell(line, 1).Value = RankName(Ranks[r]);
            sheet.Cell(line, 2).FormulaA1 = $"SUM({Col(jobColumns[r])})";
            sheet.Cell(line, 3).FormulaA1 = populationCells[Ranks[r]];
            sheet.Cell(line, 4).FormulaA1 = $"MAX(0,C{line}-B{line})";
            sheet.Cell(line, 5).FormulaA1 = $"IFERROR(D{line}/C{line},0)";
            sheet.Cell(line, 5).Style.NumberFormat.Format = "0%";
        }

        // Production comparée à la demande, produit par produit.
        row += 7;
        sheet.Cell(row, 1).Value = "Production et demande par jour (à plein régime)";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        Header(sheet, row + 1, "Produit", "Production", "Utilisé comme matière", "Brûlé pour se chauffer", "Eau des bâtiments",
            "Demande des habitants", "Solde", "Solde en % de la production");
        var line2 = row + 2;
        foreach (var product in world.Products)
        {
            sheet.Cell(line2, 1).Value = product.Id;
            sheet.Cell(line2, 2).FormulaA1 = $"SUMIF({Col("H")},A{line2},{Col("N")})";
            sheet.Cell(line2, 3).FormulaA1 = $"SUMIF({Col("J")},A{line2},{Col("O")})+SUMIF({Col("L")},A{line2},{Col("P")})";
            sheet.Cell(line2, 4).FormulaA1 = $"SUMIF({Col("Q")},A{line2},{Col("S")})";
            sheet.Cell(line2, 5).FormulaA1 = product.Id == Energy.Water ? $"SUM({Col("T")})" : "0";

            var demand = product.Need is { } need && _mainProductOfNeed.GetValueOrDefault(need) == product.Id && _needRows.TryGetValue(need, out var needRow)
                ? string.Join("+", Ranks.Select((rank, r) =>
                    $"{populationCells[rank]}*Besoins!${XLHelper.GetColumnLetterFromNumber(4 + r)}${needRow}"))
                : "0";
            sheet.Cell(line2, 6).FormulaA1 = demand;
            sheet.Cell(line2, 7).FormulaA1 = $"B{line2}-C{line2}-D{line2}-E{line2}-F{line2}";
            sheet.Cell(line2, 8).FormulaA1 = $"IFERROR(G{line2}/B{line2},0)";
            sheet.Cell(line2, 8).Style.NumberFormat.Format = "0%";
            line2++;
        }

        sheet.Range(row + 2, 2, line2 - 1, 7).Style.NumberFormat.Format = "0.0";
        sheet.SheetView.FreezeColumns(1);
        sheet.Columns().AdjustToContents(header, line2, 8, 40);
        sheet.Row(header).Height = 32;
    }
}
