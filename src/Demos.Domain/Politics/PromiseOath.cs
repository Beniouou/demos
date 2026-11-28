namespace Demos.Domain.Politics;

/// <summary>
/// Le texte du serment (décidé le 27 novembre 2026, document des règles, « Le serment ») : la première phrase de La
/// Promesse, la phrase de chacun de ses pôles, la dernière phrase et la ligne de signature. On recopie la première et la
/// dernière phrase ; les engagements (« Article 1 — … ») et les lois de l'An I s'écrivent dessous.
/// </summary>
/// <param name="First">« Nous, peuple de X, avons … » : à recopier.</param>
/// <param name="Middle">« Sur cette terre qui ne nous doit rien, nous faisons serment … » : les pôles de La Promesse.</param>
/// <param name="Last">« Que ceci soit notre Promesse à ceux qui viendront après nous. » : à recopier.</param>
/// <param name="Signed">« Signé, en l'An I de l'exil. »</param>
public sealed record PromiseOath(string First, string Middle, string Last, string Signed)
{
    public const string LastSentence = "Que ceci soit notre Promesse à ceux qui viendront après nous.";
    public const string SignedLine = "Signé, en l'An I de l'exil.";

    /// <summary>Le texte à recopier, phrase par phrase : la première et la dernière.</summary>
    public IReadOnlyList<string> ToCopy => [First, Last];

    /// <summary>Le nombre de mots à recopier.</summary>
    public int WordsToCopy => ToCopy.Sum(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

    /// <summary>La phrase de serment d'un pôle (décidé le 27 novembre 2026, gardée du texte d'exemple).</summary>
    public static string PhraseOf(Orientation pole) => pole.Code switch
    {
        "EG" => "qu'aucun ne sera placé au-dessus d'un autre",
        "SE" => "que chacun tiendra la place qui lui revient",
        "PO" => "de faire fructifier chaque main et chaque champ",
        "EC" => "de prendre à la terre sans la ruiner",
        "CO" => "que ce qui fait vivre appartiendra à tous",
        _ => "que chacun restera maître de son ouvrage",
    };

    /// <summary>Le serment d'une Promesse pour un peuple : les phrases des pôles dans l'ordre des axes, jointes par « , » et « et ».</summary>
    public static PromiseOath Of(PromiseDefinition promise, string peopleName)
    {
        var phrases = promise.Orientations.Select(PhraseOf).ToList();
        var joined = phrases.Count switch
        {
            0 => "",
            1 => phrases[0],
            _ => string.Join(", ", phrases.Take(phrases.Count - 1)) + " et " + phrases[^1],
        };
        var opening = promise.Opening ?? "avons choisi l'exil.";
        return new PromiseOath(
            $"Nous, peuple de {peopleName}, {opening}",
            $"Sur cette terre qui ne nous doit rien, nous faisons serment {joined}.",
            LastSentence,
            SignedLine);
    }
}
