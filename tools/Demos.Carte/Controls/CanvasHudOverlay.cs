using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace Demos.Carte.Controls;

/// <summary>
/// Gestionnaire d'affichage du badge HUD flottant au sommet du canvas
/// indiquant l'outil sélectionné et ses raccourcis souris essentiels de manière élégante et compacte.
/// </summary>
public static class CanvasHudOverlay
{
    public static void Draw(
        Graphics g,
        EditorTool currentTool,
        string biomeBrushType,
        string resourceBrushType,
        int riverCount,
        int communeCount)
    {
        if (currentTool == EditorTool.Navigate) return;

        var (toolName, shortcuts, accentColor) = currentTool switch
        {
            EditorTool.ElevateBrush => ("Relief", "Clic G : Surélever (+)  ·  Clic D : Creuser (−)", Color.FromArgb(46, 125, 50)),
            EditorTool.SmoothBrush => ("Adoucir", "Clic G : Lisser pente  ·  Clic D : Aplanir plateau", Color.FromArgb(25, 118, 210)),
            EditorTool.DrawRiver => ("Fleuve", $"Clic G : Poser source  ·  Clic D : Supprimer" + (riverCount > 0 ? $" ({riverCount} tracé{(riverCount > 1 ? "s" : "")})" : ""), Color.FromArgb(2, 136, 209)),
            EditorTool.DrawBiome => ($"Biome {GetBiomeDisplayName(biomeBrushType)}", "Clic G : Peindre  ·  Clic D : Gommer", GetBiomeColor(biomeBrushType)),
            EditorTool.PlaceResource => ($"Gisement {GetResourceDisplayName(resourceBrushType)}", "Clic G : Peindre  ·  Clic D : Gommer", Color.FromArgb(180, 95, 35)),
            EditorTool.MoveCommune => ("Communes", $"Glisser : Déplacer  ·  Clic G : Poser  ·  Clic D : Supprimer" + (communeCount > 0 ? $" ({communeCount})" : ""), Color.FromArgb(180, 50, 40)),
            EditorTool.NamePlace => ("Toponymie", "Clic G : Placer nom  ·  Clic D : Supprimer", Color.FromArgb(145, 70, 30)),
            _ => ("", "", Color.FromArgb(70, 70, 70))
        };

        if (string.IsNullOrEmpty(toolName)) return;

        var prevSmooth = g.SmoothingMode;
        var prevTextHint = g.TextRenderingHint;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var fontBold = new Font("Segoe UI", 9.0f, FontStyle.Bold);
        using var fontRegular = new Font("Segoe UI", 8.8f, FontStyle.Regular);

        var titleSz = g.MeasureString(toolName, fontBold);
        var shortSz = g.MeasureString(shortcuts, fontRegular);

        const float padX = 12f;
        const float badgeH = 26f;
        const float badgeY = 10f;
        const float badgeX = 14f;
        var totalW = padX + titleSz.Width + 14f + shortSz.Width + padX;

        var rect = new RectangleF(badgeX, badgeY, totalW, badgeH);

        // Contour arrondi (pill moderne)
        using var gp = new GraphicsPath();
        const float r = 6f;
        gp.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        gp.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        gp.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        gp.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        gp.CloseFigure();

        // Fond ardoise sombre translucide moderne (88% opaque)
        using var bgBrush = new SolidBrush(Color.FromArgb(225, 24, 30, 36));
        using var borderPen = new Pen(Color.FromArgb(140, accentColor), 1.2f);
        using var textTitleBrush = new SolidBrush(Color.White);
        using var textShortBrush = new SolidBrush(Color.FromArgb(205, 215, 225));
        using var sepBrush = new SolidBrush(Color.FromArgb(110, 130, 150));

        g.FillPath(bgBrush, gp);
        g.DrawPath(borderPen, gp);

        // Pastille d'accent couleur vive à gauche
        using var accentBrush = new SolidBrush(accentColor);
        g.FillEllipse(accentBrush, rect.X + 8, rect.Y + 8, 10, 10);

        // Titre de l'outil
        var curX = rect.X + 24;
        var textY = rect.Y + 3.5f;
        g.DrawString(toolName, fontBold, textTitleBrush, curX, textY);
        curX += titleSz.Width + 2;

        // Séparateur vertical discret
        g.DrawString("|", fontRegular, sepBrush, curX, textY);
        curX += 10;

        // Raccourcis d'action
        g.DrawString(shortcuts, fontRegular, textShortBrush, curX, textY + 0.5f);

        g.SmoothingMode = prevSmooth;
        g.TextRenderingHint = prevTextHint;
    }

    private static string GetBiomeDisplayName(string type) =>
        ZoneMaskGrid.BiomeTypes.FirstOrDefault(b => b.Key.Equals(type, StringComparison.OrdinalIgnoreCase)).Label ?? type;

    private static Color GetBiomeColor(string type) => type.ToLowerInvariant() switch
    {
        "foret" or "forest" => Color.FromArgb(46, 125, 50),
        "fertile" => Color.FromArgb(205, 155, 30),
        "marais" => Color.FromArgb(30, 140, 110),
        "plage" => Color.FromArgb(215, 175, 50),
        "falaise" => Color.FromArgb(120, 125, 135),
        "desert" => Color.FromArgb(215, 125, 30),
        "glacier" => Color.FromArgb(50, 150, 210),
        _ => Color.FromArgb(46, 125, 50)
    };

    private static string GetResourceDisplayName(string type) => type.ToLowerInvariant() switch
    {
        "charbon" => "Charbon",
        "fer" => "Fer",
        _ => "Pierre"
    };
}
