using System.Globalization;
using System.Text;

namespace Demos.Infrastructure;

/// <summary>
/// Où sont rangées les cartes (décidé le 24 novembre 2026) : une carte par dossier, <c>cartes/&lt;nom&gt;/</c> dans les
/// données du jeu, avec sa <c>carte.json</c> et ses images de relief (<c>relief.json</c>, <c>relief-altitude.png</c>,
/// <c>relief-ombres.png</c>). Le nom de la carte par défaut est dans <c>cartes/carte-par-defaut.txt</c> ; la carte
/// d'origine du jeu s'appelle <see cref="OriginalMap"/>. Les chemins sont relatifs au dossier des données, avec des « / ».
/// </summary>
public static class MapFiles
{
    /// <summary>Le dossier des cartes, dans les données du jeu.</summary>
    public const string Folder = "cartes";

    /// <summary>Le fichier qui nomme la carte par défaut.</summary>
    public const string DefaultFile = Folder + "/carte-par-defaut.txt";

    /// <summary>La carte d'origine du jeu, carte par défaut si rien d'autre n'est dit.</summary>
    public const string OriginalMap = "origine";

    /// <summary>Le fichier de la carte dans son dossier.</summary>
    public const string MapFile = "carte.json";

    /// <summary>Le fichier qui décrit le relief dans le dossier d'une carte.</summary>
    public const string ReliefFile = "relief.json";

    /// <summary>Le chemin d'un fichier de cette carte, relatif au dossier des données (« cartes/origine/carte.json »).</summary>
    public static string PathOf(string map, string file) => $"{Folder}/{map}/{file}";

    /// <summary>
    /// Le nom de la carte par défaut, lu par <paramref name="readText"/> dans <see cref="DefaultFile"/> ;
    /// <see cref="OriginalMap"/> si le fichier manque ou est vide.
    /// </summary>
    public static string DefaultName(Func<string, string> readText)
    {
        try
        {
            var name = readText(DefaultFile).Trim();
            return string.IsNullOrEmpty(name) ? OriginalMap : name;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return OriginalMap;
        }
    }

    /// <summary>
    /// Le nom écrit en tête d'une carte.json (<c>"name"</c>), lu dans ses premiers octets sans lire tout le fichier
    /// (plusieurs Mo) ; vide s'il n'y est pas.
    /// </summary>
    public static string NameFromStart(ReadOnlySpan<byte> start)
    {
        try
        {
            var reader = new System.Text.Json.Utf8JsonReader(start, isFinalBlock: false, state: default);
            while (reader.Read())
            {
                if (reader.CurrentDepth == 1 && reader.TokenType == System.Text.Json.JsonTokenType.PropertyName && reader.ValueTextEquals("name")
                    && reader.Read() && reader.TokenType == System.Text.Json.JsonTokenType.String)
                    return reader.GetString() ?? "";
                if (reader.CurrentDepth > 1)
                    break;
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }
        return "";
    }

    /// <summary>
    /// Un nom de dossier tiré du nom d'une carte : minuscules sans accents, mots reliés par des tirets
    /// (« Terre des Exilés » → « terre-des-exiles ») ; « carte » s'il ne reste rien.
    /// </summary>
    public static string FolderNameOf(string displayName)
    {
        var decomposed = displayName.Normalize(NormalizationForm.FormD);
        var text = new StringBuilder();
        var dash = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9' || c is >= 'A' and <= 'Z')
            {
                text.Append(char.ToLowerInvariant(c));
                dash = false;
            }
            else if (!dash && text.Length > 0)
            {
                text.Append('-');
                dash = true;
            }
        }
        var name = text.ToString().Trim('-');
        return name.Length == 0 ? "carte" : name;
    }
}
