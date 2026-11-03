using System.Globalization;
using System.Net;
using System.Text;
using Demos.Application;
using Demos.Application.Ports;
using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

/// <summary>
/// Commande « simuler » : joue la partie de départ pendant quelques années, sans joueur, et écrit un rapport HTML :
/// finances, classes, chômage, production et stocks, bâtiments en difficulté, année par année.
/// </summary>
internal static class SimulationReport
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly Rank[] Ranks = [Rank.Workers, Rank.Intermediate, Rank.Executives];

    /// <summary>Jour de fondation de la deuxième commune, quand elle est demandée.</summary>
    private const int FoundingDay = 30;

    public static void Write(IContentSource content, int years, bool secondCommune, string reportPath)
    {
        var simulation = PrototypeScenario.Create(content);
        var world = simulation.World;
        // Les emplois du départ, pour la photo du départ (la simulation les refait chaque jour).
        world.Workers = Employment.Assign(world.Structures, world.Classes);
        var start = Snapshot.Take(simulation, "Départ");
        var snapshots = new List<Snapshot>();
        var curve = new List<(int Day, double Treasury, double Population)> { (0, world.Treasury.Balance, world.Classes.Sum(c => c.Population)) };
        string? foundingNote = null;

        Console.WriteLine($"Simulation de {years} an(s){(secondCommune ? ", avec une deuxième commune" : "")}…");
        var day = 0;
        for (var year = 1; year <= years; year++)
        {
            var output = new Dictionary<string, double>();
            var stockBefore = StockOf(world);
            var energy = new Dictionary<string, double>();
            for (var d = 0; d < 365; d++)
            {
                simulation.AdvanceDay();
                day++;
                foreach (var (id, produced) in world.Produced)
                    output[id] = output.GetValueOrDefault(id) + produced;
                foreach (var building in world.Structures.Values.Where(s => s.Jobs > 0 || s.HousingPlaces > 0))
                    energy[building.Id] = energy.GetValueOrDefault(building.Id) + world.EnergyShareOf(building);

                if (secondCommune && day == FoundingDay)
                    foundingNote = FoundSecondCommune(simulation);
                if (day % 7 == 0)
                    curve.Add((day, world.Treasury.Balance, world.Classes.Sum(c => c.Population)));
            }

            snapshots.Add(Snapshot.Take(simulation, $"Année {year}", output, energy, stockBefore));
            Console.WriteLine($"  année {year} : trésor {Money(world.Treasury.Balance)}, habitants {Count(world.Classes.Sum(c => c.Population))}");
        }

        File.WriteAllText(reportPath, Html(start, snapshots, curve, years, foundingNote, world), new UTF8Encoding(false));
        Console.WriteLine($"Rapport écrit : {reportPath}");
    }

    private static string FoundSecondCommune(Simulation simulation)
    {
        var world = simulation.World;
        var pole = Orientation.All.MaxBy(p => world.Legitimacy[p]);
        var commune = simulation.TryFoundCommune("Deuxième commune", PrototypeScenario.FirstCommuneId, PaymentMode.StockFirst, pole, 5);
        var cost = world.BuildingTypes[Simulation.BaseCampTypeId].Construction;
        var materials = cost is null ? "aucun coût de construction" : $"{Count(cost.Quantity)} {cost.ProductId}, {cost.Days} jours de chantier";
        return commune is not null
            ? $"Deuxième commune fondée le jour {FoundingDay} (charte {pole.Code}, {materials}, payés par la réserve de chantier, le stock ou le trésor)."
            : $"La deuxième commune n'a pas pu être fondée le jour {FoundingDay} ({materials} : matériaux, argent ou légitimité insuffisants).";
    }

    private static Dictionary<string, double> StockOf(World world)
    {
        var stock = new Dictionary<string, double>();
        foreach (var market in world.Markets.Values)
            foreach (var (productId, quantity) in market.Contents)
                stock[productId] = stock.GetValueOrDefault(productId) + quantity;
        return stock;
    }

    // Une photo du pays à une date ----------------------------------------------------------------------------------

    private sealed record CommuneLine(string Name, double Residents, double Fund, double Pollution, int HousingPlaces, double Stored, int Capacity);

    private sealed record ClassLine(string Name, string Commune, Rank Rank, long Population, double Wealth, double Needs, double Housing,
        double SupplyShortage, double MoneyShortfall, double Unemployed);

    private sealed record RankLine(Rank Rank, double Population, double Jobs, double Employed);

    private sealed record ProductLine(string Id, string Name, double Price, double ProducedPerDay, double MaterialPerDay, double OtherPerDay,
        double Stock, double StockChange);

    private sealed record BuildingLine(string Name, string Type, string Commune, string Owner, int Jobs, double Workers, double? ProducedPerDay,
        double? Potential, string? Product, double Energy, double? PaidShare, bool Struggling, double? Fund, int Capacity, double WageCut);

    private sealed record Snapshot(
        string Title,
        DateOnly Date,
        double Treasury,
        double CompanyFunds,
        IReadOnlyList<CommuneLine> Communes,
        IReadOnlyList<ClassLine> Classes,
        IReadOnlyList<RankLine> Ranks,
        IReadOnlyList<ProductLine> Products,
        IReadOnlyList<BuildingLine> Buildings)
    {
        public static Snapshot Take(Simulation simulation, string title, Dictionary<string, double>? output = null,
            Dictionary<string, double>? energy = null, Dictionary<string, double>? stockBefore = null)
        {
            var world = simulation.World;
            var communes = world.Communes.ToList();
            string CommuneName(string? id) => communes.FirstOrDefault(c => c.Id == id)?.Name ?? "—";

            var communeLines = communes.Select(c => new CommuneLine(c.Name, world.Residents(c.Id), world.CommuneFundOf(c.Id).Balance,
                world.PollutionByCommune.GetValueOrDefault(c.Id), world.HousingPlaces(c.Id), world.StorageOf(c.Id).Stored.Values.Sum(),
                world.StorageCapacity(c.Id))).ToList();

            var classLines = world.Classes.Where(c => c.Population > 0)
                .Select(c => new ClassLine(c.Name, CommuneName(c.CommuneId), c.Rank, c.Population, c.Wealth, c.NeedsSatisfaction, c.HousingCoverage,
                    c.SeasonSupplyShortage, c.SeasonMoneyShortfall, c.UnemployedMembers))
                .ToList();

            var buildings = world.Structures.Values.Where(s => s.Kind == StructureKind.Building).ToList();
            var rankLines = SimulationReport.Ranks.Select(rank => new RankLine(rank,
                world.Classes.Where(c => c.Rank == rank).Sum(c => (double)c.Population),
                buildings.Sum(b => Staffing.JobsOf(b, rank)),
                world.Classes.Where(c => c.Rank == rank).Sum(c => c.Population - c.UnemployedMembers))).ToList();

            // Production et usage comme matière, par produit, en moyenne par jour sur l'année écoulée.
            var produced = new Dictionary<string, double>();
            var asMaterial = new Dictionary<string, double>();
            foreach (var (id, quantity) in output ?? [])
            {
                if (!world.Structures.TryGetValue(id, out var building) || building.Production is not { } recipe)
                    continue;
                produced[recipe.ProductId] = produced.GetValueOrDefault(recipe.ProductId) + quantity / 365;
                foreach (var material in recipe.Materials)
                    asMaterial[material.ProductId] = asMaterial.GetValueOrDefault(material.ProductId) + quantity * material.Quantity / 365;
            }

            var stock = StockOf(world);
            var productLines = world.Products.Select(p =>
            {
                var change = stock.GetValueOrDefault(p.Id) - (stockBefore?.GetValueOrDefault(p.Id) ?? stock.GetValueOrDefault(p.Id));
                var made = produced.GetValueOrDefault(p.Id);
                var used = asMaterial.GetValueOrDefault(p.Id);
                return new ProductLine(p.Id, p.Name, world.PriceOf(p.Id), made, used, made - used - change / 365, stock.GetValueOrDefault(p.Id), change);
            }).ToList();

            var buildingLines = buildings.Where(b => b.JobCapacity > 0 || b.HousingPlaces > 0).Select(b =>
            {
                var owner = b.Ownership == Ownership.Public ? "État"
                    : (b.Ownership == Ownership.Cooperative ? "Coop. " : "Privé ")
                      + (b.OwnerClassId is { } o && world.ClassesById.TryGetValue(o, out var c) ? c.Name : "?");
                var workers = world.Workers.GetValueOrDefault(b.Id);
                double? perDay = output is not null && b.Production is not null ? output.GetValueOrDefault(b.Id) / 365 : null;
                double? potential = b.Production is { } r ? r.QuantityPerWorker * b.Jobs : null;
                return new BuildingLine(b.Name, b.TypeId ?? "", CommuneName(world.CommuneOf(b)), owner, b.Jobs, workers, perDay, potential,
                    b.Production?.ProductId, energy?.GetValueOrDefault(b.Id) / 365 ?? world.EnergyShareOf(b), world.Wages.PaidShare(b.Id),
                    simulation.IsStruggling(b), Companies.HasCompanyFund(b) ? world.CompanyFundOf(b).Balance : null, b.JobCapacity, b.WageCut);
            }).ToList();

            return new Snapshot(title, simulation.Clock.Today, world.Treasury.Balance,
                buildings.Where(Companies.HasCompanyFund).Sum(b => world.CompanyFundOf(b).Balance),
                communeLines, classLines, rankLines, productLines, buildingLines);
        }
    }

    // Page HTML -----------------------------------------------------------------------------------------------------

    private static string Money(double value) => (Math.Abs(value) < 0.5 ? 0 : value).ToString("N0", French);

    private static string Count(double value) => value.ToString("N0", French);

    private static string Number(double value, int decimals = 2) => (Math.Abs(value) < 0.5 * Math.Pow(10, -decimals) ? 0 : value).ToString("N" + decimals, French);

    private static string Percent(double value) => value.ToString("P0", French);

    private static string E(string text) => WebUtility.HtmlEncode(text);

    private static string RankName(Rank rank) => rank switch
    {
        Rank.Workers => "Ouvriers",
        Rank.Intermediate => "Prof. intermédiaires",
        _ => "Cadres",
    };

    private static string Html(Snapshot start, List<Snapshot> years, List<(int Day, double Treasury, double Population)> curve, int count,
        string? foundingNote, World world)
    {
        var html = new StringBuilder();
        html.Append("""
            <!doctype html>
            <html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Rapport de simulation</title>
            <style>
            :root { --bg: #fafaf8; --ink: #1d1d1b; --muted: #6b6b66; --line: #e2e0da; --head: #eef2f7; --bad: #b3261e; --badbg: #fbe9e7; --good: #1e6b3a; --accent: #2f5d8a; }
            @media (prefers-color-scheme: dark) { :root { --bg: #1b1b1a; --ink: #ecebe6; --muted: #a09f99; --line: #3a3936; --head: #263040; --bad: #ff8a80; --badbg: #3a2120; --good: #7fd19b; --accent: #8db8e8; } }
            body { margin: 0; background: var(--bg); color: var(--ink); font: 14px/1.45 system-ui, "Segoe UI", sans-serif; }
            main { max-width: 1200px; margin: 0 auto; padding: 24px 16px 64px; }
            h1 { font-size: 24px; margin: 0 0 4px; } h2 { font-size: 19px; margin: 36px 0 8px; border-bottom: 2px solid var(--line); padding-bottom: 4px; }
            h3 { font-size: 15px; margin: 20px 0 6px; } .muted { color: var(--muted); }
            .scroll { overflow-x: auto; } table { border-collapse: collapse; margin: 4px 0 8px; font-variant-numeric: tabular-nums; }
            th, td { border-bottom: 1px solid var(--line); padding: 4px 8px; text-align: right; white-space: nowrap; }
            th { background: var(--head); font-weight: 600; } th:first-child, td:first-child { text-align: left; }
            td.text { text-align: left; } tr.bad td { background: var(--badbg); } .bad { color: var(--bad); font-weight: 600; } .good { color: var(--good); }
            nav a { color: var(--accent); margin-right: 12px; } svg text { fill: var(--muted); font-size: 11px; }
            </style></head><body><main>
            """);

        html.Append($"<h1>Rapport de simulation</h1><p class=\"muted\">Partie de départ jouée {count} an(s) sans joueur, du {start.Date:dd/MM/yyyy} au {years[^1].Date:dd/MM/yyyy}. ")
            .Append($"Écrit le {DateTime.Now:dd/MM/yyyy à HH:mm}.</p>");
        if (foundingNote is not null)
            html.Append($"<p>{E(foundingNote)}</p>");
        html.Append("<nav>").Append(string.Join("", years.Select((y, i) => $"<a href=\"#annee{i + 1}\">{E(y.Title)}</a>"))).Append("</nav>");

        // Résumé : l'évolution d'une année à l'autre.
        var all = new List<Snapshot> { start };
        all.AddRange(years);
        html.Append("<h2>Résumé</h2><div class=\"scroll\"><table><tr><th></th>");
        foreach (var s in all)
            html.Append($"<th>{E(s.Title)}</th>");
        html.Append("</tr>");
        Row(html, "Trésor de l'État", all.Select(s => Money(s.Treasury)));
        Row(html, "Caisses des communes", all.Select(s => Money(s.Communes.Sum(c => c.Fund))));
        Row(html, "Caisses des entreprises", all.Select(s => Money(s.CompanyFunds)));
        Row(html, "Habitants", all.Select(s => Count(s.Classes.Sum(c => c.Population))));
        Row(html, "Richesse des habitants", all.Select(s => Money(s.Classes.Sum(c => c.Wealth))));
        foreach (var rank in Ranks)
            Row(html, $"Chômage : {RankName(rank)}", all.Select(s => s.Ranks.First(r => r.Rank == rank) is var r && r.Population > 0
                ? Percent(1 - r.Employed / r.Population) : "—"));
        Row(html, "Bâtiments en difficulté", all.Select(s => Count(s.Buildings.Count(b => b.Struggling))));
        Row(html, "Postes fermés (licenciements)", all.Select(s => Count(s.Buildings.Sum(b => b.Capacity - b.Jobs))));
        html.Append("</table></div>");
        html.Append(Chart(curve));

        foreach (var (snapshot, index) in years.Select((s, i) => (s, i)))
            Year(html, snapshot, index + 1);

        html.Append("<h2>Réglages utilisés</h2><p class=\"muted\">Ceux de game/data/equilibrage.json au moment de la simulation.</p><div class=\"scroll\"><table>");
        var balance = Balance.Current;
        (string, string)[] settings =
        [
            ("Marge", Percent(balance.Margin)),
            ("Impôt d'État / communal (par jour)", $"{Number(balance.StateTaxRate * 100, 2)} % / {Number(balance.CommuneTaxRate * 100, 2)} %"),
            ("Chauffage par unité, eau par unité", $"{Number(balance.HeatingPerUnit, 3)} / {Number(balance.WaterPerUnit, 3)}"),
            ("Activité sans énergie", Percent(balance.OutputWithoutEnergy)),
            ("Réserve de combustible, des entreprises (jours)", $"{balance.FuelReserveDays} / {balance.CompanyReserveDays}"),
            ("Seuil de pollution", Number(balance.PollutedThreshold, 3)),
        ];
        foreach (var (label, value) in settings)
            html.Append($"<tr><td>{E(label)}</td><td>{E(value)}</td></tr>");
        html.Append("</table></div></main></body></html>\n");
        return html.ToString();
    }

    private static void Row(StringBuilder html, string label, IEnumerable<string> cells) =>
        html.Append($"<tr><td>{E(label)}</td>").Append(string.Join("", cells.Select(c => $"<td>{E(c)}</td>"))).Append("</tr>");

    private static void Year(StringBuilder html, Snapshot s, int number)
    {
        html.Append($"<h2 id=\"annee{number}\">{E(s.Title)} <span class=\"muted\">({s.Date:dd/MM/yyyy})</span></h2>");
        html.Append($"<p>Trésor de l'État : <b>{Money(s.Treasury)}</b> · caisses des entreprises : {Money(s.CompanyFunds)}</p>");

        html.Append("<h3>Communes</h3><div class=\"scroll\"><table><tr><th>Commune</th><th>Habitants</th><th>Places de logement</th><th>Caisse</th>")
            .Append("<th>Pollution par habitant</th><th>Stockage occupé</th></tr>");
        foreach (var c in s.Communes)
        {
            var polluted = c.Pollution > Balance.Current.PollutedThreshold;
            html.Append($"<tr><td>{E(c.Name)}</td><td>{Count(c.Residents)}</td><td>{Count(c.HousingPlaces)}</td><td>{Money(c.Fund)}</td>")
                .Append($"<td class=\"{(polluted ? "bad" : "")}\">{Number(c.Pollution, 3)}</td><td>{Count(c.Stored)} / {Count(c.Capacity)}</td></tr>");
        }

        html.Append("</table></div>");

        html.Append("<h3>Classes</h3><div class=\"scroll\"><table><tr><th>Classe</th><th>Commune</th><th>Rang</th><th>Population</th><th>Richesse</th>")
            .Append("<th>Par membre</th><th>Besoins couverts</th><th>Logés</th><th>Manque de produits</th><th>Manque d'argent</th><th>Sans emploi</th></tr>");
        foreach (var c in s.Classes.OrderBy(c => c.Commune).ThenBy(c => c.Rank).ThenByDescending(c => c.Population))
        {
            var bad = c.Needs < 0.75;
            html.Append($"<tr class=\"{(bad ? "bad" : "")}\"><td>{E(c.Name)}</td><td class=\"text\">{E(c.Commune)}</td><td class=\"text\">{RankName(c.Rank)}</td>")
                .Append($"<td>{Count(c.Population)}</td><td>{Money(c.Wealth)}</td><td>{Number(c.Population > 0 ? c.Wealth / c.Population : 0, 1)}</td>")
                .Append($"<td>{Percent(c.Needs)}</td><td>{Percent(c.Housing)}</td><td>{Percent(c.SupplyShortage)}</td><td>{Percent(c.MoneyShortfall)}</td>")
                .Append($"<td>{Count(c.Unemployed)}</td></tr>");
        }

        html.Append("</table></div><p class=\"muted\">En rouge : moins de 75 % des besoins couverts. Les manques sont la moyenne de la dernière saison.</p>");

        html.Append("<h3>Emplois par rang</h3><div class=\"scroll\"><table><tr><th>Rang</th><th>Population</th><th>Emplois</th><th>Employés</th><th>Chômage</th><th>Emplois vacants</th></tr>");
        foreach (var r in s.Ranks)
        {
            var unemployment = r.Population > 0 ? 1 - r.Employed / r.Population : 0;
            html.Append($"<tr><td>{RankName(r.Rank)}</td><td>{Count(r.Population)}</td><td>{Count(r.Jobs)}</td><td>{Count(r.Employed)}</td>")
                .Append($"<td class=\"{(unemployment > 0.2 ? "bad" : "")}\">{Percent(unemployment)}</td><td>{Count(Math.Max(0, r.Jobs - r.Employed))}</td></tr>");
        }

        html.Append("</table></div>");

        html.Append("<h3>Production et stocks (moyenne par jour sur l'année)</h3><div class=\"scroll\"><table><tr><th>Produit</th><th>Prix</th><th>Production</th>")
            .Append("<th>Utilisé comme matière</th><th>Autres usages</th><th>Stock en fin d'année</th><th>Variation du stock</th></tr>");
        foreach (var p in s.Products.Where(p => p.ProducedPerDay > 0 || p.MaterialPerDay > 0 || Math.Abs(p.Stock) > 0.5))
        {
            var falling = p.StockChange < -0.5;
            html.Append($"<tr><td>{E(p.Name)}</td><td>{Number(p.Price)}</td><td>{Number(p.ProducedPerDay, 1)}</td><td>{Number(p.MaterialPerDay, 1)}</td>")
                .Append($"<td>{Number(p.OtherPerDay, 1)}</td><td>{Count(p.Stock)}</td><td class=\"{(falling ? "bad" : p.StockChange > 0.5 ? "good" : "")}\">")
                .Append($"{(p.StockChange > 0 ? "+" : "")}{Count(p.StockChange)}</td></tr>");
        }

        html.Append("</table></div><p class=\"muted\">« Autres usages » : ce qui a été produit et n'est ni devenu matière ni resté en stock : achats des habitants, ")
            .Append("chauffage et eau des bâtiments, péremption, pertes entre communes (le stock de départ entre dans le calcul).</p>");

        html.Append("<h3>Bâtiments</h3><div class=\"scroll\"><table><tr><th>Bâtiment</th><th>Commune</th><th>Propriétaire</th><th>Postes ouverts / capacité</th><th>Pourvus</th>")
            .Append("<th>Produit</th><th>Production par jour</th><th>À plein régime</th><th>Énergie</th><th>Salaires payés (30 j)</th><th>Baisse des salaires</th><th>Caisse</th></tr>");
        foreach (var b in s.Buildings.OrderByDescending(b => b.Struggling).ThenBy(b => b.Commune))
        {
            var filled = b.Jobs > 0 ? b.Workers / b.Jobs : 1;
            var lowOutput = b.ProducedPerDay is { } made && b.Potential is > 0 && made / b.Potential < 0.5;
            var trouble = b.Struggling || lowOutput || b.Energy < 0.9;
            html.Append($"<tr class=\"{(trouble ? "bad" : "")}\"><td>{E(b.Name)}</td><td class=\"text\">{E(b.Commune)}</td><td class=\"text\">{E(b.Owner)}</td>")
                .Append($"<td class=\"{(b.Jobs < b.Capacity ? "bad" : "")}\">{Count(b.Jobs)} / {Count(b.Capacity)}</td><td>{(b.Jobs > 0 ? Percent(filled) : "—")}</td><td class=\"text\">{E(b.Product ?? "")}</td>")
                .Append($"<td>{(b.ProducedPerDay is { } p ? Number(p, 1) : "—")}</td><td>{(b.Potential is { } pot ? Number(pot, 1) : "—")}</td>")
                .Append($"<td>{Percent(b.Energy)}</td><td>{(b.PaidShare is { } paid ? Percent(paid) : "—")}</td><td>{(b.WageCut > 0 ? "−" + Percent(b.WageCut) : "—")}</td><td>{(b.Fund is { } f ? Money(f) : "—")}</td></tr>");
        }

        html.Append("</table></div><p class=\"muted\">En rouge, en tête : un bâtiment en difficulté (moins de 80 % des salaires payés sur 30 jours), ")
            .Append("qui produit moins de la moitié de son plein régime (emplois non pourvus, matières manquantes), ou qui manque d'énergie. ")
            .Append("Les postes ouverts en rouge sont sous la capacité : l'entreprise a licencié (au changement de mois, selon son statut).</p>");
    }

    /// <summary>Petit graphique du trésor et de la population, relevés chaque semaine.</summary>
    private static string Chart(List<(int Day, double Treasury, double Population)> curve)
    {
        const double width = 900, height = 180, left = 8, top = 8;
        var lastDay = Math.Max(1, curve[^1].Day);
        string Line(Func<(int Day, double Treasury, double Population), double> value, string color)
        {
            var min = Math.Min(0, curve.Min(value));
            var max = Math.Max(1, curve.Max(value));
            var points = curve.Select(p => $"{(left + p.Day / (double)lastDay * (width - 2 * left)).ToString("0.#", CultureInfo.InvariantCulture)},"
                + $"{(top + (1 - (value(p) - min) / (max - min)) * (height - 2 * top)).ToString("0.#", CultureInfo.InvariantCulture)}");
            return $"<polyline fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" points=\"{string.Join(" ", points)}\"/>";
        }

        return $"""
            <h3>Évolution, semaine par semaine</h3>
            <p class="muted"><span style="color:#2f5d8a">━ trésor de l'État</span> (de {Money(curve.Min(c => c.Treasury))} à {Money(curve.Max(c => c.Treasury))}) ·
            <span style="color:#c06a1b">━ habitants</span> (de {Count(curve.Min(c => c.Population))} à {Count(curve.Max(c => c.Population))}) ; chaque courbe a sa propre échelle.</p>
            <div class="scroll"><svg viewBox="0 0 {width} {height}" width="100%" style="max-width:{width}px;border:1px solid var(--line)" role="img" aria-label="Trésor et habitants">
            {Line(c => c.Treasury, "#2f5d8a")}{Line(c => c.Population, "#c06a1b")}
            </svg></div>
            """;
    }
}
