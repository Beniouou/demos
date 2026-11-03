namespace Demos.Editeur;

/// <summary>Un choix d'un menu déroulant : le texte affiché et la valeur qu'il représente.</summary>
internal sealed record Item(string Text, object? Value)
{
    public override string ToString() => Text;
}

/// <summary>
/// Construit un formulaire, une ligne par champ : un libellé à gauche, le champ à droite, une aide en gris dessous.
/// </summary>
internal sealed class Fields
{
    public TableLayoutPanel Panel { get; } = new()
    {
        ColumnCount = 2,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Top,
        Padding = new Padding(8),
    };

    public Fields()
    {
        Panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        Panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
    }

    private T Add<T>(string label, T control, string? help = null) where T : Control
    {
        control.Width = 400;
        control.Margin = new Padding(3, 4, 3, 2);
        Panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 8, 3, 2) });
        Panel.Controls.Add(control);
        if (help is not null)
        {
            Panel.Controls.Add(new Label());
            Panel.Controls.Add(new Label { Text = help, AutoSize = true, MaximumSize = new Size(400, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 4) });
        }

        return control;
    }

    public void Section(string title)
    {
        var label = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(Panel.Font.FontFamily, Panel.Font.Size + 1, FontStyle.Bold),
            Margin = new Padding(3, 16, 3, 4),
        };
        Panel.Controls.Add(label);
        Panel.SetColumnSpan(label, 2);
    }

    public TextBox Text(string label, string? help = null, string value = "") => Add(label, new TextBox { Text = value }, help);

    public NumericUpDown Number(string label, double value, int decimals = 0, string? help = null, double max = 1_000_000) =>
        Add(label, new NumericUpDown
        {
            DecimalPlaces = decimals,
            Minimum = 0,
            Maximum = (decimal)max,
            Increment = decimals == 0 ? 1 : (decimal)Math.Pow(10, -Math.Min(decimals, 2)),
            Value = (decimal)value,
            ThousandsSeparator = true,
        }, help);

    /// <summary>Un menu déroulant fermé : on ne peut que choisir dans la liste.</summary>
    public ComboBox Choice(string label, IEnumerable<Item> items, string? help = null, object? selected = null)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        box.Items.AddRange(items.Cast<object>().ToArray());
        box.SelectedItem = box.Items.Cast<Item>().FirstOrDefault(i => Equals(i.Value, selected)) ?? (box.Items.Count > 0 ? box.Items[0] : null);
        return Add(label, box, help);
    }

    /// <summary>Un menu déroulant où l'on peut aussi écrire une valeur nouvelle (une nouvelle catégorie).</summary>
    public ComboBox Editable(string label, IEnumerable<string> items, string? help = null)
    {
        var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
        box.Items.AddRange(items.Cast<object>().ToArray());
        return Add(label, box, help);
    }

    public CheckBox Check(string label, bool value = false, string? help = null) => Add(label, new CheckBox { Checked = value, Text = "" }, help);

    public static object? ValueOf(ComboBox box) => (box.SelectedItem as Item)?.Value;

    public static double ValueOf(NumericUpDown box) => (double)box.Value;
}
