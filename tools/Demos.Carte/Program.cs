using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Demos.Domain.Map;

namespace Demos.Carte;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string dataDir;
        try
        {
            dataDir = FindDataDirectory();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Projet Démos — Éditeur de Carte", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        // Mode CLI si exécuté avec --generer (pour les scripts batch automatisés)
        if (args.Length > 0 && (args[0].Equals("--generer", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-g", StringComparison.OrdinalIgnoreCase)))
        {
            return RunCli(dataDir);
        }

        // Mode test automatique des curseurs et synchronisation
        if (args.Length > 0 && args[0].Equals("--test-sliders", StringComparison.OrdinalIgnoreCase))
        {
            ApplicationConfiguration.Initialize();
            var form = new MapEditorForm(dataDir);
            form.Show();
            Application.DoEvents();
            var success = form.TestSlidersSync(out var report);
            File.WriteAllText(Path.Combine(dataDir, "slider_test_result.txt"), report);
            Console.WriteLine(report);
            form.Close();
            return success ? 0 : 1;
        }

        // Mode export d'images
        if (args.Length > 1 && args[0].Equals("--export", StringComparison.OrdinalIgnoreCase))
        {
            return ExportImages(dataDir, args[1]);
        }

        // Mode capture d'écran de l'interface
        if (args.Length > 1 && args[0].Equals("--screenshot", StringComparison.OrdinalIgnoreCase))
        {
            ApplicationConfiguration.Initialize();
            var form = new MapEditorForm(dataDir);
            form.WindowState = FormWindowState.Normal;
            form.ClientSize = new System.Drawing.Size(1440, 900);

            if (args.Length > 3 && args[3].Equals("--island", StringComparison.OrdinalIgnoreCase))
            {
                form.CreateIslandMap();
            }
            else if (args.Length > 3 && args[3].Equals("--load", StringComparison.OrdinalIgnoreCase))
            {
                form.LoadExistingMap();
            }

            if (args.Length > 4 && args[4].Equals("--vintage", StringComparison.OrdinalIgnoreCase))
            {
                form.SetStyleForTesting(MapRenderStyle.VintageParchment1874);
            }

            if (args.Length > 2 && Enum.TryParse<EditorTool>(args[2], true, out var t))
            {
                form.SelectTool(t);
                if (t == EditorTool.DrawRiver)
                {
                    form.AddRiverPointForTesting(300, 300);
                }
                else if (t == EditorTool.DrawBiome || t == EditorTool.DrawForest)
                {
                    // 1. Élever un massif montagneux naturel culminant à > 2500m pour le glacier
                    for (int step = 0; step < 22; step++)
                    {
                        form.ElevateBrushForTesting(1000, 420, 200, 150);
                    }
                    form.ApplyBiomeBrushForTesting(1000, 420, 90, "glacier", true, out _);

                    // 2. Forêt dans les moyennes terres (< 2500m)
                    form.ApplyBiomeBrushForTesting(1150, 720, 130, "foret", true, out _);

                    // 3. Terres fertiles en plaine (< 600m)
                    form.ApplyBiomeBrushForTesting(1450, 800, 120, "fertile", true, out _);

                    // 4. Falaises sur le versant escarpé et en ceinture autour du massif
                    form.ApplyBiomeBrushForTesting(1000, 420, 160, "falaise", true, out _);
                    form.ApplyBiomeBrushForTesting(820, 480, 90, "falaise", true, out _);
                    // Rétablir le dôme sommital du glacier (alt >= 2500m)
                    form.ApplyBiomeBrushForTesting(1000, 420, 75, "glacier", true, out _);

                    // 5. Marais
                    form.ApplyBiomeBrushForTesting(720, 740, 90, "marais", true, out _);

                    // 6. Plage le long du littoral ouest
                    form.ApplyBiomeBrushForTesting(540, 680, 75, "plage", true, out _);

                    // 7. Désert en plaine orientale
                    form.ApplyBiomeBrushForTesting(1650, 620, 95, "desert", true, out _);
                }
                else if (t == EditorTool.PlaceResource)
                {
                    form.ApplyBiomeBrushForTesting(1100, 550, 110, "foret", true, out _);
                    form.ApplyResourceBrushForTesting(650, 420, 55, "pierre", true);
                    form.ApplyResourceBrushForTesting(950, 450, 50, "charbon", true);
                    form.ApplyResourceBrushForTesting(1350, 680, 55, "fer", true);
                }
                else if (t == EditorTool.MoveCommune)
                {
                    form.MoveCommuneForTesting(0, 1050, 620);
                    form.AddCommuneForTesting(1600, 800, "Beaufort-en-Vallée");
                    form.AddCommuneForTesting(1950, 1100, "Saint-Malo");
                    form.AddCommuneForTesting(450, 950, "Châteauneuf");
                }
            }

            form.Show();
            Application.DoEvents();

            using var bmp = new System.Drawing.Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
            bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            form.Close();
            Console.WriteLine("Screenshot saved to " + args[1]);
            return 0;
        }

        // Mode GUI fenêtré Windows Forms
        ApplicationConfiguration.Initialize();
        try
        {
            System.Windows.Forms.Application.Run(new MapEditorForm(dataDir));
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Une erreur inattendue est survenue : {ex.Message}", "Projet Démos — Éditeur de Carte", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int RunCli(string dataDir)
    {
        Console.WriteLine("=== Générateur de Carte Topographique IGN (Démos - Mode CLI) ===");
        try
        {
            var targetPath = Path.Combine(dataDir, "carte.json");
            var map = MapGenerator.Generate(TerrainParameters.Default, out var heightmap);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            var json = JsonSerializer.Serialize(map, options);
            File.WriteAllText(targetPath, json);

            Console.WriteLine($"✓ Carte générée avec succès : {targetPath}");
            Console.WriteLine($"  • Dimensions : {map.Width} × {map.Height} lieues (espace continu vectoriel)");
            Console.WriteLine($"  • Courbes de niveau (isohypses) : {map.Contours.Count} polylignes (Marching Squares)");
            Console.WriteLine($"  • Altitude maximale : {heightmap.MaxElevation:F0} m");
            Console.WriteLine($"  • Points cotés : {map.SpotElevations.Count} sommets répertoriés");
            Console.WriteLine($"  • Hydrographie : fleuve \"{map.River.Name}\" ({map.River.Points.Count} points)");
            Console.WriteLine($"  • Gisements : {map.Resources.Count} ressources géoréférencées");
            Console.WriteLine($"  • Chef-lieu initial : {map.DefaultCommune.Name} en ({map.DefaultCommune.Position.X}, {map.DefaultCommune.Position.Y})");

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Erreur : {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static int ExportImages(string dataDir, string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
            var map = MapGenerator.Generate(TerrainParameters.Default, out var heightmap);

            var pathIgn = Path.Combine(outputDir, "carte-ign-pastel.png");
            var pathVintage = Path.Combine(outputDir, "carte-parchemin-1874.png");

            using (var bmp = new System.Drawing.Bitmap(2000, 1280))
            {
                using var g = System.Drawing.Graphics.FromImage(bmp);
                MapRenderer.Render(g, new System.Drawing.Rectangle(0, 0, 2000, 1280), map, heightmap, MapRenderStyle.IgnPastel);
                bmp.Save(pathIgn, System.Drawing.Imaging.ImageFormat.Png);
            }

            using (var bmp = new System.Drawing.Bitmap(2000, 1280))
            {
                using var g = System.Drawing.Graphics.FromImage(bmp);
                MapRenderer.Render(g, new System.Drawing.Rectangle(0, 0, 2000, 1280), map, heightmap, MapRenderStyle.VintageParchment1874);
                bmp.Save(pathVintage, System.Drawing.Imaging.ImageFormat.Png);
            }

            Console.WriteLine($"✓ Images exportées dans {outputDir} :");
            Console.WriteLine($"  • {pathIgn}");
            Console.WriteLine($"  • {pathVintage}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur export : {ex.Message}");
            return 1;
        }
    }

    private static string FindDataDirectory()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var data = Path.Combine(directory.FullName, "game", "data");
                if (Directory.Exists(data))
                    return data;
            }
        }

        throw new DirectoryNotFoundException("Dossier game/data introuvable. Veuillez exécuter l'outil depuis le dépôt demos.");
    }
}
