using System.Collections.Generic;
using System.Linq;
using System.Text;
using Demos.Application;
using Demos.Domain.Population;
using Godot;
using Orientation = Demos.Domain.Politics.Orientation;

namespace Demos.Game;

/// <summary>
/// Population → Groupes et attentes (décidé le 21 novembre 2026), en lecture : à gauche la carte des groupes (une bulle
/// par classe, rang et idéologie, de la taille de ses membres, sur deux axes idéologiques au choix, le troisième en
/// couleur), pour toute la nation ou une commune ; à droite la fiche du groupe choisi : ce qu'il veut (ses pôles), son
/// adhésion aux lois, les lois qu'il rejette et accepte, les pressions qui peuvent changer son idéologie comparées à leur
/// seuil, puis ses besoins, sa richesse, son éducation et ses lieux de travail, commune par commune.
/// </summary>
public partial class PopulationGroupsPage : HBoxContainer, IMenuPage
{
    /// <summary>Une classe (rang et idéologie) dans les communes choisies : ses groupes locaux, commune par commune.</summary>
    private sealed record Group(string Name, Rank Rank, string IdeologyName, IReadOnlyList<Orientation> Poles,
        IReadOnlyList<(CommuneView Commune, SocialClassView Local)> Locals)
    {
        public double Members => Locals.Sum(l => (double)l.Local.Population);

        public double Mean(System.Func<SocialClassView, double> value) =>
            Members > 0 ? Locals.Sum(l => value(l.Local) * l.Local.Population) / Members : 0;
    }

    private static readonly IdeologicalAxis[] Axes = System.Enum.GetValues<IdeologicalAxis>();

    private readonly GameView _view = null!;
    private readonly CommuneChoice _commune = new();
    private readonly OptionButton _xAxis = null!;
    private readonly OptionButton _yAxis = null!;
    private readonly Label _colorLegend = null!;
    private readonly GroupMap _map = new();
    private readonly RichTextLabel _sheet = null!;
    private string? _selected;
    private string _mapKey = "";

    public PopulationGroupsPage(GameView view)
    {
        _view = view;
        AddThemeConstantOverride("separation", 12);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.1f };
        left.AddThemeConstantOverride("separation", 6);
        left.AddChild(_commune.Row());

        var axes = new HBoxContainer();
        axes.AddThemeConstantOverride("separation", 8);
        axes.AddChild(new Label { Text = "Axes", CustomMinimumSize = new Vector2(90, 0) });
        _xAxis = AxisMenu(IdeologicalAxis.Hierarchy);
        _yAxis = AxisMenu(IdeologicalAxis.Property);
        axes.AddChild(_xAxis);
        axes.AddChild(new Label { Text = "×" });
        axes.AddChild(_yAxis);
        left.AddChild(axes);

        _colorLegend = UiTheme.Caption("");
        left.AddChild(_colorLegend);
        _map.SizeFlagsVertical = SizeFlags.ExpandFill;
        _map.Chosen += key =>
        {
            _selected = key;
            _sheet.Text = "";
        };
        left.AddChild(_map);
        AddChild(left);
        AddChild(new VSeparator());

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _sheet = new HintText { BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SelectionEnabled = false };
        _sheet.AddThemeColorOverride("default_color", UiTheme.Text);
        scroll.AddChild(_sheet);
        AddChild(scroll);
    }

    // Godot recrée les nœuds C# par ce constructeur sans paramètre (rechargement des scripts).
    public PopulationGroupsPage() { }

    private OptionButton AxisMenu(IdeologicalAxis selected)
    {
        var menu = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var axis in Axes)
            menu.AddItem(AxisName(axis));
        menu.Select((int)selected);
        menu.ItemSelected += _ =>
        {
            // Deux axes différents : choisir l'axe de l'autre menu les échange.
            var other = menu == _xAxis ? _yAxis : _xAxis;
            if (other.Selected == menu.Selected)
                other.Select((int)Axes.First(a => (int)a != menu.Selected));
            _mapKey = "";
        };
        return menu;
    }

    private static string AxisName(IdeologicalAxis axis) => axis switch
    {
        IdeologicalAxis.Hierarchy => "Hiérarchie (EG / SE)",
        IdeologicalAxis.Production => "Production (PO / EC)",
        _ => "Propriété (CO / LI)",
    };

    public void Refresh()
    {
        var communes = _view.Communes();
        _commune.Fill(communes);
        if (_commune.Button.ItemCount > 0)
            _commune.Button.SetItemText(0, "Toute la nation");
        var chosen = _commune.SelectedId is { } id ? communes.Where(c => c.Id == id).ToList() : communes;
        var groups = GroupsOf(chosen);
        if (_selected is null || groups.All(g => g.Name != _selected))
            _selected = groups.OrderByDescending(g => g.Members).FirstOrDefault()?.Name;

        var key = $"{_commune.SelectedId}|{_xAxis.Selected}|{_yAxis.Selected}|{_selected}|"
            + string.Join(";", groups.Select(g => $"{g.Name}:{g.Members:N0}"));
        if (key != _mapKey)
        {
            _mapKey = key;
            _map.SetAxes(Axes[_xAxis.Selected], Axes[_yAxis.Selected]);
            _map.SetGroups(groups.Select(g => new GroupMap.Bubble(g.Name, g.Rank, g.Poles, g.Members)).ToList(), _selected);
            var third = _map.ThirdAxis;
            _colorLegend.Text = $"Couleur : {AxisName(third).ToLowerInvariant()} — "
                + $"{WorldText.PoleName(new Orientation(third, Pole.A))} en haut de chaque case, {WorldText.PoleName(new Orientation(third, Pole.B))} en bas ; "
                + "colonnes : ouvriers, intermédiaires, cadres.";
        }

        var text = groups.FirstOrDefault(g => g.Name == _selected) is { } group
            ? Describe(group, _commune.SelectedId is null)
            : SheetText.Muted("Aucun groupe : personne n'habite ici.");
        // Le texte n'est remis que s'il a changé : sinon la fiche remonterait en haut à chaque image.
        if (text != _sheet.Text)
            _sheet.Text = text;
    }

    private static List<Group> GroupsOf(IReadOnlyList<CommuneView> communes) =>
        communes
            .SelectMany(c => c.Classes.Where(g => g.Population > 0).Select(g => (Commune: c, Local: g)))
            .GroupBy(x => x.Local.Name)
            .Select(g => new Group(g.Key, g.First().Local.Rank, g.First().Local.IdeologyName, g.First().Local.Poles ?? [], g.ToList()))
            .ToList();

    private string Describe(Group group, bool nation)
    {
        var text = new StringBuilder(SheetText.Heading(group.Name.ToUpperInvariant()));
        text.AppendLine($"{SheetText.Hint("Membres", PopulationText.Members)} {group.Members:N0}"
            + (nation ? $" · {group.Locals.Count} commune(s)" : "")
            + $" · {SheetText.Hint("rang", PopulationText.Rank(group.Rank))} {PopulationText.RankName(group.Rank)}");

        // Ce qu'il veut : ses pôles, donc des lois qui vont vers eux.
        text.Append(SheetText.Section("Ce qu'il veut", PopulationText.Poles));
        text.AppendLine($"{SheetText.Chips(group.Poles)}  {SheetText.Escape(group.IdeologyName)} : des lois qui vont vers "
            + string.Join(", ", group.Poles.Select(p => WorldText.PoleName(p).ToLowerInvariant())) + ".");

        // Son adhésion et les lois qu'il juge.
        var adhesion = group.Mean(g => g.Adhesion);
        text.Append(SheetText.Section("Adhésion aux lois", PopulationText.Adhesion));
        text.AppendLine($"{Gauge(adhesion, adhesion >= 0.5 ? new Color("#46a758") : UiTheme.Danger)}  {adhesion:P0} des lois qui le touchent acceptées");
        var stances = group.Locals
            .SelectMany(l => (l.Local.LawStances ?? []).Select(s => (Stance: s, Weight: s.Weight * l.Local.Population)))
            .GroupBy(s => s.Stance.LawId)
            .Select(g => (Name: g.First().Stance.LawName, Share: g.Sum(s => s.Stance.Share * s.Weight) / System.Math.Max(1e-9, g.Sum(s => s.Weight))))
            .OrderBy(s => s.Share)
            .ToList();
        if (stances.Count == 0)
        {
            text.AppendLine(SheetText.Muted("Aucune loi en vigueur ne le touche."));
        }
        else
        {
            var rejected = stances.Where(s => s.Share < 0.5).ToList();
            var accepted = stances.Where(s => s.Share >= 0.5).ToList();
            if (rejected.Count > 0)
                text.AppendLine($"[color=#{UiTheme.Danger.ToHtml(false)}]Rejette[/color] : "
                    + string.Join(" · ", rejected.Select(s => $"{SheetText.Escape(s.Name)} ({s.Share:P0} accepté)")));
            if (accepted.Count > 0)
                text.AppendLine($"Accepte : " + string.Join(" · ", accepted.Select(s => $"{SheetText.Escape(s.Name)} ({s.Share:P0})")));
        }

        // Les pressions qui peuvent changer son idéologie, chacune comparée à son seuil.
        text.Append(SheetText.Section("Pressions sur son idéologie", PopulationText.Pressures));
        foreach (var line in Pressures(group))
            text.AppendLine(line);

        // Ses besoins, en bref (le détail est dans l'onglet Besoins).
        text.Append(SheetText.Section("Besoins", PopulationText.Needs));
        text.AppendLine($"Besoins couverts {group.Mean(g => g.NeedsSatisfaction):P0} → {PopulationText.Growth(group.Mean(g => g.NeedsSatisfaction))} par saison");
        var needs = group.Locals.SelectMany(l => (l.Local.NeedDetails ?? new Dictionary<string, NeedRecord>()).Select(n => (Need: n.Key, Record: n.Value, l.Local.Population)))
            .GroupBy(n => n.Need)
            .Select(g => (Need: g.Key, Coverage: g.Sum(n => n.Record.Coverage * n.Population) / System.Math.Max(1, g.Sum(n => (double)n.Population))))
            .OrderBy(n => n.Coverage)
            .ToList();
        if (needs.Count > 0)
            text.AppendLine(string.Join(" · ", needs.Select(n => $"{FlowText.NeedName(n.Need)} {n.Coverage:P0}")));

        // Commune par commune : membres, chômeurs, richesse, revenus et impôts, éducation, lieux de travail.
        text.Append(SheetText.Section("Dans chaque commune"));
        foreach (var (commune, local) in group.Locals.OrderByDescending(l => l.Local.Population))
        {
            text.AppendLine($"[b]{SheetText.Escape(commune.Name)}[/b] : {local.Population:N0} membres · "
                + $"{SheetText.Hint("chômeurs", PopulationText.Unemployed)} {local.Unemployed:N0} "
                + SheetText.Muted($"({(local.Population > 0 ? local.Unemployed / local.Population : 0):P0})"));
            text.AppendLine($"    {SheetText.Hint("Richesse", PopulationText.Wealth)} {local.Wealth:N0} ({local.WealthPerMember:N1} par membre) · "
                + $"{SheetText.Hint("revenus de la saison", PopulationText.Taxes)} {local.SeasonIncome:N0}, impôts {local.SeasonTaxes:N0}");
            text.AppendLine($"    {SheetText.Hint("Éducation", PopulationText.Education)} {local.EducationPoints:N0} (avancée {local.AdvancedEducationPoints:N0})");
            if (local.Possessions is { Count: > 0 } possessions && local.Population > 0)
                text.AppendLine("    " + SheetText.Hint("Possessions", PopulationText.Possessions) + " : " + string.Join(" · ", possessions.Select(p =>
                    $"{FlowText.NeedName(p.NeedId)} {p.Owned / local.Population:N1} / {p.Target / local.Population:N1} par personne")));
            var workplaces = local.Workplaces ?? [];
            text.AppendLine("    " + SheetText.Hint("Travaille à", PopulationText.Workplaces) + " : " + (workplaces.Count == 0
                ? SheetText.Muted("nulle part pour l'instant")
                : string.Join(" · ", workplaces.Select(w => $"{SheetText.Structure(_view, w.StructureId)} {w.Members:N0}"))));
        }
        return text.ToString();
    }

    /// <summary>
    /// Les pressions qui s'appliquent à ce groupe, selon ses pôles (décidé le 21 novembre 2026 pour l'affichage ; règles de
    /// <see cref="ClassTransitions"/>). Chacune : sa mesure, son seuil, une jauge, et le pôle vers lequel elle pousse.
    /// Elles se mesurent commune par commune : la jauge montre la moyenne, la fin de ligne les communes où le seuil est passé.
    /// </summary>
    private static IEnumerable<string> Pressures(Group group)
    {
        Pole PoleOn(IdeologicalAxis axis) => group.Poles.FirstOrDefault(p => p.Axis == axis).Pole;
        static double Unemployment(SocialClassView g) => g.Population > 0 ? g.Unemployed / g.Population : 0;
        string Where(System.Func<CommuneView, SocialClassView, bool> crossed)
        {
            var places = group.Locals.Where(l => crossed(l.Commune, l.Local)).Select(l => l.Commune.Name).ToList();
            return places.Count == 0 ? "" : $" [color=#{UiTheme.Danger.ToHtml(false)}]⚠ {SheetText.Escape(string.Join(", ", places))}[/color]";
        }
        string Line(string label, double value, double threshold, bool above, Orientation to, string effect, string where) =>
            $"{label} {value:P0} (seuil {(above ? "au-delà de" : "sous")} {threshold:P0}) {Gauge(above ? value / threshold / 2 : threshold / System.Math.Max(value, 1e-9) / 2, where.Length > 0 ? UiTheme.Danger : UiTheme.Muted)}"
            + $" → {SheetText.Chip(to)} {effect}{where}";

        // Hiérarchie : le chômage fait descendre et rend ségrégationniste ; le plein emploi ramène à l'égalitarisme.
        var unemployment = group.Mean(Unemployment);
        yield return Line("Chômage", unemployment, ClassTransitions.UnemploymentThreshold, true,
            new Orientation(IdeologicalAxis.Hierarchy, Pole.B),
            $"{ClassTransitions.UnemployedShareAffected:P0} des chômeurs descendent d'un rang, si la richesse par membre ne monte plus ; "
            + $"en défiance (adhésion sous {ClassTransitions.TrustThreshold:P0}) et déclassés, ils deviennent ségrégationnistes ; "
            + "confiants et pas déclassés, égalitaristes ; sinon la crise les divise",
            Where((_, g) => Unemployment(g) > ClassTransitions.UnemploymentThreshold && g.WealthPerMemberLastSeason is { } last && g.WealthPerMember <= last));
        if (PoleOn(IdeologicalAxis.Hierarchy) == Pole.B)
            yield return Line("Plein emploi : chômage", unemployment, ClassTransitions.FullEmploymentThreshold, false,
                new Orientation(IdeologicalAxis.Hierarchy, Pole.A),
                $"{ClassTransitions.FullEmploymentShareAffected:P0} des membres redeviennent égalitaristes chaque saison",
                Where((_, g) => Unemployment(g) < ClassTransitions.FullEmploymentThreshold));

        // Hiérarchie encore (décidé le 23 novembre 2026) : manquer d'argent en travaillant rend égalitariste ; être
        // comblé et très prélevé rend ségrégationniste.
        if (PoleOn(IdeologicalAxis.Hierarchy) == Pole.B)
            yield return Line("Manque d'argent", group.Mean(g => g.MoneyShortfall), ClassTransitions.MoneyShortfallThreshold, true,
                new Orientation(IdeologicalAxis.Hierarchy, Pole.A),
                $"{ClassTransitions.PropertyShareAffected:P0} des membres deviennent égalitaristes chaque saison, s'ils travaillent",
                Where((_, g) => g.MoneyShortfall >= ClassTransitions.MoneyShortfallThreshold && Unemployment(g) <= ClassTransitions.UnemploymentThreshold));
        else
            yield return Line("Impôts sur les revenus", group.Mean(g => g.SeasonTaxRate), ClassTransitions.HeavyTaxRateThreshold, true,
                new Orientation(IdeologicalAxis.Hierarchy, Pole.B),
                $"{ClassTransitions.PropertyShareAffected:P0} des membres deviennent ségrégationnistes chaque saison, si leurs besoins sont comblés (au-delà de {ClassTransitions.FullySatisfiedThreshold:P0})",
                Where((_, g) => g.SeasonTaxRate >= ClassTransitions.HeavyTaxRateThreshold && g.NeedsSatisfaction >= ClassTransitions.FullySatisfiedThreshold));

        // Propriété : l'expérience du travail et ce que le groupe possède.
        var property = PoleOn(IdeologicalAxis.Property) == Pole.B
            ? new Orientation(IdeologicalAxis.Property, Pole.A)
            : new Orientation(IdeologicalAxis.Property, Pole.B);
        yield return $"Lieu de travail et propriété → {SheetText.Chip(property)} "
            + (property.Pole == Pole.A
                ? "travailler dans un bâtiment public ou une SCOP qui va bien, ou dans une entreprise privée en difficulté, rend collectiviste ; posséder une SCOP aussi"
                : "travailler dans une entreprise privée qui va bien, ou dans un bâtiment public ou une SCOP en difficulté, rend libéral ; posséder une entreprise privée aussi")
            + $" ({ClassTransitions.PropertyShareAffected:P0} des membres concernés par saison ; en difficulté : moins de 80 % des salaires payés sur 30 jours)";

        // Production : la pollution rend écologiste ; manquer de produits ou de places rend positiviste.
        if (PoleOn(IdeologicalAxis.Production) == Pole.A)
        {
            var threshold = Demos.Domain.Economy.Pollution.PollutedThreshold;
            var pollution = group.Locals.Count > 0 ? group.Locals.Max(l => l.Commune.PollutionPerResident) : 0;
            yield return $"Pollution de la commune {pollution:N2} (seuil {threshold:N2}) "
                + Gauge(threshold > 0 ? pollution / threshold / 2 : 0, pollution >= threshold ? UiTheme.Danger : UiTheme.Muted)
                + $" → {SheetText.Chip(new Orientation(IdeologicalAxis.Production, Pole.B))} {ClassTransitions.ProductionShareAffected:P0} des membres deviennent écologistes chaque saison"
                + Where((c, _) => c.PollutionPerResident >= threshold);
        }
        else
        {
            yield return Line("Manque de produits ou de places", group.Mean(g => g.SupplyShortage), ClassTransitions.SupplyShortageThreshold, true,
                new Orientation(IdeologicalAxis.Production, Pole.A),
                $"{ClassTransitions.ProductionShareAffected:P0} des membres deviennent positivistes chaque saison",
                Where((_, g) => g.SupplyShortage >= ClassTransitions.SupplyShortageThreshold));
        }
    }

    /// <summary>Une jauge en texte : dix pavés, remplis selon la part (0 à 1) ; le seuil d'une pression tombe au milieu.</summary>
    private static string Gauge(double part, Color color)
    {
        var filled = (int)System.Math.Round(System.Math.Clamp(part, 0, 1) * 10);
        return $"[color=#{color.ToHtml(false)}]{new string('▰', filled)}[/color][color=#{UiTheme.Border.ToHtml(false)}]{new string('▱', 10 - filled)}[/color]";
    }
}
