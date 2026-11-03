using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

namespace Demos.Editeur;

/// <summary>
/// La fenêtre de l'éditeur : on choisit ce qu'on crée, le formulaire change, ses menus sont remplis avec le contenu du
/// jeu. « Créer » applique d'abord le tableur, ajoute l'élément aux fichiers du jeu, puis régénère le tableur.
/// </summary>
internal sealed class EditorForm : Form
{
    private readonly ToolPaths _paths;
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420, AccessibleName = "Que voulez-vous créer ?" };
    private readonly Panel _formArea = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "Journal" };
    private readonly Button _create = new() { Text = "Créer", Width = 160, Height = 34 };

    // L'action du formulaire affiché : elle crée l'élément dans le contenu donné et renvoie les erreurs.
    private Func<ContentCreator, List<string>>? _action;

    private static readonly (string Text, Action<EditorForm, Fields, ContentCreator> Build)[] Kinds =
    [
        ("Un produit (ressource, bien ou service)", (f, fields, content) => f.BuildProduct(fields, content)),
        ("Une recette (ce qu'un bâtiment fabrique)", (f, fields, content) => f.BuildRecipe(fields, content)),
        ("Un type de bâtiment", (f, fields, content) => f.BuildBuilding(fields, content)),
        ("Un besoin des habitants", (f, fields, content) => f.BuildNeed(fields, content)),
    ];

    public EditorForm(ToolPaths paths)
    {
        _paths = paths;
        Text = "Projet Démos — éditeur de contenu";
        Font = new Font("Segoe UI", 10);
        Size = new Size(820, 900);
        StartPosition = FormStartPosition.CenterScreen;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(12, 12, 12, 0) };
        top.Controls.Add(new Label { Text = "Que voulez-vous créer ?", AutoSize = true, Margin = new Padding(3, 6, 12, 3), Font = new Font(Font, FontStyle.Bold) });
        _kind.Items.AddRange(Kinds.Select(k => (object)k.Text).ToArray());
        top.Controls.Add(_kind);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 220, ColumnCount = 1, RowCount = 2, Padding = new Padding(12, 4, 12, 12) };
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        buttons.Controls.Add(_create);
        buttons.Controls.Add(new Label
        {
            Text = "Le tableur est d'abord appliqué au jeu, puis régénéré avec le nouvel élément.\nFermez-le dans Excel avant de créer.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(12, 2, 3, 3),
        });
        bottom.Controls.Add(buttons);
        bottom.Controls.Add(_log);

        Controls.Add(_formArea);
        Controls.Add(bottom);
        Controls.Add(top);

        _kind.SelectedIndexChanged += (_, _) => ShowForm();
        _create.Click += (_, _) => Create();
        _kind.SelectedIndex = 0;
    }

    /// <summary>Refait le formulaire du choix courant, avec le contenu du jeu tel qu'il est maintenant.</summary>
    private void ShowForm()
    {
        ContentCreator content;
        try
        {
            content = new ContentCreator(_paths.Data);
        }
        catch (Exception e)
        {
            _log.Text = $"Impossible de lire les données du jeu : {e.Message}";
            return;
        }

        var fields = new Fields();
        Kinds[_kind.SelectedIndex].Build(this, fields, content);
        _formArea.SuspendLayout();
        _formArea.Controls.Clear();
        _formArea.Controls.Add(fields.Panel);
        _formArea.ResumeLayout();
    }

    // Menus remplis avec le contenu du jeu ---------------------------------------------------------------------------

    private static IEnumerable<Item> ProductItems(ContentCreator content, string? none = null) =>
        (none is null ? [] : new[] { new Item(none, null) })
        .Concat(content.Products.OrderBy(p => p.Name, StringComparer.CurrentCulture).Select(p => new Item($"{p.Name} ({p.Id})", p.Id)));

    private static IEnumerable<Item> RankItems() => Labels.Ranks.Select(r => new Item(Labels.RankName(r), r));

    /// <summary>Remplit l'identifiant à partir du nom, tant qu'on ne l'a pas écrit soi-même.</summary>
    private static void LinkId(TextBox name, TextBox id)
    {
        var manual = false;
        name.TextChanged += (_, _) =>
        {
            if (!manual)
                id.Text = ContentCreator.IdFrom(name.Text);
        };
        id.KeyPress += (_, _) => manual = true;
    }

    /// <summary>Deux matières au plus : un menu (ou « aucune ») et une quantité par unité produite.</summary>
    private static Func<List<(string ProductId, double Quantity)>> Materials(Fields fields, ContentCreator content)
    {
        var boxes = new List<(ComboBox Material, NumericUpDown Quantity)>();
        for (var m = 1; m <= 2; m++)
        {
            var material = fields.Choice($"Matière {m}", ProductItems(content, "(aucune)"));
            var quantity = fields.Number($"Quantité de matière {m} par unité produite", 1, 3);
            boxes.Add((material, quantity));
        }

        return () => boxes.Where(b => Fields.ValueOf(b.Material) is string)
            .Select(b => ((string)Fields.ValueOf(b.Material)!, Fields.ValueOf(b.Quantity))).ToList();
    }

    // Formulaires ----------------------------------------------------------------------------------------------------

    private void BuildProduct(Fields fields, ContentCreator content)
    {
        fields.Section("Nouveau produit");
        var name = fields.Text("Nom", "Le nom affiché dans le jeu, par exemple « Livre ».");
        var id = fields.Text("Identifiant", "Rempli tout seul à partir du nom ; sert dans les fichiers du jeu.");
        LinkId(name, id);
        var need = fields.Choice("Besoin couvert",
            new[] { new Item("(aucun : une matière)", null) }.Concat(content.PurchasableNeeds.Select(n => new Item(n.Name, n.Id))),
            "Une matière (pierre, bois…) ne couvre aucun besoin : elle sert aux recettes.");
        var automatic = fields.Check("Prix automatique (recommandé)", true,
            "Le jeu calcule le prix dès qu'un bâtiment fabrique ce produit : salaires, énergie et matières, plus 30 % de bénéfice.");
        var price = fields.Number("Prix de secours", 1, 2,
            "Utilisé tant qu'aucun bâtiment ne fabrique ce produit (une matière qu'on ne produit pas encore, par exemple).");
        price.Enabled = false;
        automatic.CheckedChanged += (_, _) =>
        {
            price.Enabled = !automatic.Checked;
            if (automatic.Checked)
                price.Value = 1;
        };
        var stockable = fields.Check("Stockable", true, "Décochez pour un produit qui se consomme le jour même (l'eau, les services).");
        var perishable = fields.Check("Périssable");
        var life = fields.Number("Durée de vie (jours)", 30, 0, "Dans un stockage général ; plus longue dans un grenier ou un stockage spécifique.");
        life.Enabled = false;
        perishable.CheckedChanged += (_, _) => life.Enabled = perishable.Checked;

        _action = creator => creator.AddProduct(new ProductDraft(id.Text.Trim(), name.Text, Fields.ValueOf(need) as string, Fields.ValueOf(price),
            stockable.Checked, perishable.Checked ? Fields.ValueOf(life) : null));
    }

    private void BuildRecipe(Fields fields, ContentCreator content)
    {
        fields.Section("Nouvelle recette");
        var type = fields.Choice("Bâtiment",
            content.BuildingTypes.Where(t => t.Jobs > 0).OrderBy(t => t.Name, StringComparer.CurrentCulture).Select(t => new Item($"{t.Name} ({t.Id})", t.Id)),
            "Seuls les bâtiments qui ont des emplois productifs fabriquent quelque chose.");
        var existing = new Label { AutoSize = true, MaximumSize = new Size(400, 0), ForeColor = SystemColors.GrayText };
        fields.Panel.Controls.Add(new Label { Text = "Ses recettes actuelles", AutoSize = true, Margin = new Padding(3, 8, 3, 2) });
        fields.Panel.Controls.Add(existing);
        void ShowExisting() => existing.Text = content.BuildingTypes.FirstOrDefault(t => t.Id == Fields.ValueOf(type) as string) is { } t
            ? string.Join("\n", t.AvailableRecipes.Select(r => $"{r.Label} ({r.QuantityPerWorker} par travailleur)")) is { Length: > 0 } list ? list : "aucune"
            : "";
        type.SelectedIndexChanged += (_, _) => ShowExisting();
        ShowExisting();

        var product = fields.Choice("Produit fabriqué", ProductItems(content));
        var perWorker = fields.Number("Production par travailleur et par jour", 1, 3);
        var materials = Materials(fields, content);

        _action = creator => creator.AddRecipe(new RecipeDraft((string)Fields.ValueOf(type)!, (string)Fields.ValueOf(product)!, Fields.ValueOf(perWorker), materials()));
    }

    private void BuildNeed(Fields fields, ContentCreator content)
    {
        fields.Section("Nouveau besoin");
        var name = fields.Text("Nom", "Ce que les habitants veulent, par exemple « Se cultiver ».");
        var id = fields.Text("Identifiant", "Rempli tout seul à partir du nom.");
        LinkId(name, id);
        var description = fields.Text("Description (facultatif)");
        fields.Section("Quantité consommée par personne et par jour");
        var quantities = Labels.Ranks.ToDictionary(r => r, r => fields.Number(Labels.RankName(r), 0, 3));
        fields.Section("Ensuite");
        fields.Panel.Controls.Add(new Label
        {
            Text = "Créez un produit qui couvre ce besoin, puis une recette pour le fabriquer.",
            AutoSize = true,
            MaximumSize = new Size(660, 0),
            ForeColor = SystemColors.GrayText,
        });

        _action = creator => creator.AddNeed(new NeedDraft(id.Text.Trim(), name.Text, description.Text,
            quantities.ToDictionary(q => q.Key, q => Fields.ValueOf(q.Value))));
    }

    private void BuildBuilding(Fields fields, ContentCreator content)
    {
        fields.Section("Nouveau type de bâtiment");
        var name = fields.Text("Nom");
        var id = fields.Text("Identifiant", "Rempli tout seul à partir du nom.");
        LinkId(name, id);
        var category = fields.Editable("Catégorie", content.Categories, "Choisissez une catégorie, ou écrivez-en une nouvelle.");
        var level = fields.Number("Niveau", 1, 0, "La version améliorée d'un même usage a un niveau de plus.", max: 10);
        var description = fields.Text("Description");
        var tier = fields.Choice("Liste", [new Item("1 : chauffage et eau", 1), new Item("2 : électricité", 2), new Item("3 : internet", 3)]);
        var heating = fields.Check("Chauffage constant", false, "Chauffe autant toute l'année (comme la forge).");

        fields.Section("Emplois et salaires");
        var mainRank = fields.Choice("Rang productif", RankItems(), "Le rang qui produit ; les autres encadrent et dirigent.");
        var jobs = fields.Number("Emplois productifs", 0);
        var defaults = new Dictionary<Rank, double> { [Rank.Workers] = 0, [Rank.Intermediate] = 0.1, [Rank.Executives] = 0.01 };
        var ratios = Labels.Ranks.ToDictionary(r => r, r => fields.Number($"{Labels.RankName(r)} par emploi productif", defaults[r], 3));
        void UpdateRatios()
        {
            var main = (Rank)Fields.ValueOf(mainRank)!;
            foreach (var (rank, box) in ratios)
            {
                box.Enabled = rank != main;
                if (rank == main)
                    box.Value = 1;
                else if (box.Value == 1)
                    box.Value = (decimal)Staffing.ShareOf(main, rank);
            }
        }

        mainRank.SelectedIndexChanged += (_, _) => UpdateRatios();
        UpdateRatios();
        var wageDefaults = new Dictionary<Rank, double> { [Rank.Workers] = 2, [Rank.Intermediate] = 4, [Rank.Executives] = 8 };
        var wages = Labels.Ranks.ToDictionary(r => r, r => fields.Number($"Salaire par jour : {Labels.RankName(r).ToLowerInvariant()}", wageDefaults[r], 2));

        fields.Section("Première recette (facultatif)");
        var product = fields.Choice("Produit fabriqué", ProductItems(content, "(aucun)"), "D'autres recettes s'ajoutent ensuite avec « Une recette ».");
        var perWorker = fields.Number("Production par travailleur et par jour", 1, 3);
        var materials = Materials(fields, content);

        fields.Section("Logement, école, stockage, pollution");
        var housing = fields.Number("Places de logement", 0);
        var rent = fields.Number("Loyer par place et par jour", 0, 2);
        var school = fields.Number("Places d'élèves", 0);
        var tuition = fields.Number("Frais par élève et par jour", 0, 2);
        var storage = fields.Number("Stockage", 0);
        var storageKind = fields.Choice("Sorte de stockage", Labels.StorageKinds.Select(k => new Item(Labels.StorageKindName(k), k)));
        var stored = fields.Choice("Produit stocké (stockage spécifique)", ProductItems(content, "(aucun)"));
        var pollution = fields.Number("Pollution par travailleur", 0, 3);

        fields.Section("Partie de départ");
        var start = fields.Check("L'ajouter à la partie de départ", false,
            "Le jeu n'a pas encore de menu de construction : sans cela, le bâtiment existe dans le catalogue mais pas dans la partie.");
        var ownership = fields.Choice("Propriété", Labels.Ownerships.Select(o => new Item(Labels.OwnershipName(o), o)));
        var owner = fields.Choice("Classe propriétaire", new[] { new Item("(aucune)", null) }.Concat(content.Scenario.Classes.Select(c => new Item(c.Name, c.Id))),
            "Pour une coopérative ou une entreprise privée.");
        var startJobs = fields.Number("Emplois productifs dans la partie de départ", 0, help: "0 : ceux du type de bâtiment.");
        void UpdateStart() => ownership.Enabled = owner.Enabled = startJobs.Enabled = start.Checked;
        start.CheckedChanged += (_, _) => UpdateStart();
        UpdateStart();

        _action = creator =>
        {
            var main = (Rank)Fields.ValueOf(mainRank)!;
            RecipeDraft? recipe = Fields.ValueOf(product) is string productId
                ? new RecipeDraft(id.Text.Trim(), productId, Fields.ValueOf(perWorker), materials())
                : null;
            StartingDraft? starting = start.Checked
                ? new StartingDraft((Ownership)Fields.ValueOf(ownership)!, Fields.ValueOf(owner) as string,
                    Fields.ValueOf(startJobs) > 0 ? (int)Fields.ValueOf(startJobs) : null, null)
                : null;
            return creator.AddBuilding(new BuildingDraft(
                id.Text.Trim(), name.Text, category.Text, (int)Fields.ValueOf(level), description.Text, (int)Fields.ValueOf(tier)!, main,
                (int)Fields.ValueOf(jobs),
                ratios.Where(r => r.Key != main).ToDictionary(r => r.Key, r => Fields.ValueOf(r.Value)),
                wages.ToDictionary(w => w.Key, w => Fields.ValueOf(w.Value)),
                (int)Fields.ValueOf(housing), Fields.ValueOf(rent), (int)Fields.ValueOf(school), Fields.ValueOf(tuition),
                (int)Fields.ValueOf(storage), (StorageKind)Fields.ValueOf(storageKind)!, Fields.ValueOf(stored) as string,
                Fields.ValueOf(pollution), heating.Checked, recipe, starting));
        };
    }

    // Créer ----------------------------------------------------------------------------------------------------------

    private void Create()
    {
        if (_action is null)
            return;

        _log.Clear();
        UseWaitCursor = true;
        _create.Enabled = false;
        try
        {
            Run();
        }
        finally
        {
            UseWaitCursor = false;
            _create.Enabled = true;
        }
    }

    private void Log(string text) => _log.AppendText(text.Replace("\r", "").Replace("\n", Environment.NewLine) + Environment.NewLine);

    private void Run()
    {
        // 1. Les réglages du tableur d'abord, pour ne pas perdre ce qui n'a pas encore été appliqué.
        if (File.Exists(_paths.Workbook))
        {
            Log("1. Le tableur est appliqué au jeu…");
            var output = new StringWriter();
            var console = Console.Out;
            Console.SetOut(output);
            int code;
            try
            {
                code = BalanceApplier.Apply(_paths.Workbook, _paths.Data, _paths.Content());
            }
            finally
            {
                Console.SetOut(console);
            }

            Log(output.ToString().TrimEnd());
            if (code != 0)
            {
                Log("Rien n'a été créé : corrigez d'abord le tableur.");
                MessageBox.Show(this, "Le tableur a des erreurs (voir en bas de la fenêtre) : rien n'a été créé.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        // 2. L'élément, vérifié, dans les fichiers du jeu.
        Log("2. Création…");
        var creator = new ContentCreator(_paths.Data);
        var errors = _action!(creator);
        if (errors.Count == 0)
            errors = creator.Save();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Log($"  - {error}");
            MessageBox.Show(this, string.Join("\n", errors), "Rien n'a été créé", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        foreach (var change in creator.Changes)
            Log($"  - {change}");
        foreach (var price in creator.PriceChanges())
            Log($"  - {price}");

        // 3. Le tableur refait, avec le nouvel élément.
        Log("3. Le tableur est régénéré…");
        try
        {
            WorkbookGenerator.Generate(_paths.Content(), _paths.Workbook);
            Log($"Tableur écrit : {_paths.Workbook}");
        }
        catch (IOException)
        {
            Log("Le tableur est ouvert dans Excel : fermez-le puis lancez generer-equilibrage.bat. Le jeu, lui, a bien le nouvel élément.");
        }

        Log("C'est fait.");
        ShowForm();
    }
}
