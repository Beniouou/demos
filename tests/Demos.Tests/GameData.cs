using Demos.Infrastructure;

namespace Demos.Tests;

/// <summary>Lit les vrais fichiers de données du jeu (game/data), pour tester le contenu tel qu'il est livré.</summary>
public static class GameData
{
    private static readonly string DataDirectory = FindDataDirectory();

    /// <summary>Le dossier game/data.</summary>
    public static string Folder => DataDirectory;

    public static JsonContentSource Content() => new(fileName => File.ReadAllText(Path.Combine(DataDirectory, fileName)), fileName => File.ReadAllBytes(Path.Combine(DataDirectory, fileName)));

    private static string FindDataDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "game", "data");
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException("Dossier game/data introuvable au-dessus du dossier des tests.");
    }
}
