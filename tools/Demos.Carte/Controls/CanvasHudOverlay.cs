using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Demos.Carte.Controls;

/// <summary>
/// Gestionnaire d'affichage du bandeau HUD flottant au sommet du canvas
/// indiquant l'outil sélectionné et ses raccourcis souris.
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
        string? message = null;
        Color badgeColor = Color.FromArgb(240, 40, 60, 80);

        if (currentTool == EditorTool.DrawRiver)
        {
            message = $"Fleuve spontané — Clic gauche : Poser la source | Clic droit : Supprimer ({riverCount} tracé{(riverCount > 1 ? "s" : "")})";
            badgeColor = Color.FromArgb(245, 25, 90, 140);
        }
        else if (currentTool == EditorTool.DrawBiome)
        {
            var biomeDisplay = ZoneMaskGrid.BiomeTypes.FirstOrDefault(b => b.Key.Equals(biomeBrushType, StringComparison.OrdinalIgnoreCase)).Label ?? biomeBrushType;
            message = $"Pinceau Biome ({biomeDisplay}) — Clic gauche : Peindre | Clic droit : Gommer";
            badgeColor = biomeBrushType.ToLowerInvariant() switch
            {
                "foret" or "forest" => Color.FromArgb(245, 34, 110, 50),
                "fertile" => Color.FromArgb(245, 175, 130, 25),
                "marais" => Color.FromArgb(245, 30, 115, 90),
                "plage" => Color.FromArgb(245, 185, 145, 45),
                "falaise" => Color.FromArgb(245, 90, 95, 105),
                "desert" => Color.FromArgb(245, 185, 100, 25),
                "glacier" => Color.FromArgb(245, 55, 130, 180),
                _ => Color.FromArgb(245, 34, 110, 50)
            };
        }
        else if (currentTool == EditorTool.PlaceResource)
        {
            var resLabel = resourceBrushType.ToLowerInvariant() switch { "charbon" => "Charbon", "fer" => "Fer", _ => "Pierre" };
            message = $"Pinceau Gisement ({resLabel}) — Clic gauche : Peindre la veine minérale | Clic droit : Gommer";
            badgeColor = Color.FromArgb(245, 140, 80, 40);
        }
        else if (currentTool == EditorTool.ElevateBrush)
        {
            message = "Pinceau Terrain — Clic gauche : Surélever (+ relief) | Clic droit : Creuser (− relief, mer sous 0 m)";
            badgeColor = Color.FromArgb(245, 45, 95, 65);
        }
        else if (currentTool == EditorTool.SmoothBrush)
        {
            message = "Pinceau Adoucisseur — Clic gauche : Adoucir la pente | Clic droit : Aplanir en plateau";
            badgeColor = Color.FromArgb(245, 20, 110, 160);
        }
        else if (currentTool == EditorTool.NamePlace)
        {
            message = "Toponymie — Clic gauche : Placer le nom | Clic droit : Supprimer un lieu";
            badgeColor = Color.FromArgb(245, 120, 60, 30);
        }
        else if (currentTool == EditorTool.MoveCommune)
        {
            message = $"Communes — Glisser-déposer : Déplacer | Clic gauche : Poser | Clic droit : Supprimer ({communeCount} commune{(communeCount > 1 ? "s" : "")})";
            badgeColor = Color.FromArgb(245, 150, 45, 35);
        }

        if (message is null) return;

        using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        var sz = g.MeasureString(message, font);
        var rect = new RectangleF(14, 12, sz.Width + 24, sz.Height + 10);

        using var gp = new GraphicsPath();
        const float r = 8f;
        gp.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        gp.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        gp.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        gp.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        gp.CloseFigure();

        using var bgBrush = new SolidBrush(badgeColor);
        using var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        using var textBrush = new SolidBrush(Color.White);

        var prevSmooth = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPath(bgBrush, gp);
        g.DrawPath(pen, gp);
        g.DrawString(message, font, textBrush, rect.X + 12, rect.Y + 5);
        g.SmoothingMode = prevSmooth;
    }
}
