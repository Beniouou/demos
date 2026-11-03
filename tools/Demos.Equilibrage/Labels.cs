using Demos.Domain.Economy;
using Demos.Domain.Politics;
using Demos.Domain.Population;

/// <summary>
/// Les mots des menus déroulants du tableur, et leur traduction en valeurs du jeu : le générateur les écrit,
/// « appliquer » les relit.
/// </summary>
public static class Labels
{
    public const string Yes = "oui";
    public const string No = "non";

    public static readonly Rank[] Ranks = [Rank.Workers, Rank.Intermediate, Rank.Executives];

    public static string RankName(Rank rank) => rank switch
    {
        Rank.Workers => "Ouvriers",
        Rank.Intermediate => "Prof. intermédiaires",
        _ => "Cadres",
    };

    public static Rank? RankOf(string label) => Ranks.Cast<Rank?>().FirstOrDefault(r => RankName(r!.Value) == label);

    public static readonly StorageKind[] StorageKinds = [StorageKind.General, StorageKind.Food, StorageKind.NonFood, StorageKind.Specific];

    public static string StorageKindName(StorageKind kind) => kind switch
    {
        StorageKind.General => "Général",
        StorageKind.Food => "Alimentaire",
        StorageKind.NonFood => "Non alimentaire",
        _ => "Spécifique",
    };

    public static StorageKind? StorageKindOf(string label) =>
        StorageKinds.Cast<StorageKind?>().FirstOrDefault(k => StorageKindName(k!.Value) == label);

    public static readonly Ownership[] Ownerships = [Ownership.Public, Ownership.Cooperative, Ownership.Private];

    public static string OwnershipName(Ownership ownership) => ownership switch
    {
        Ownership.Public => "État",
        Ownership.Cooperative => "Coopérative",
        _ => "Privé",
    };

    public static Ownership? OwnershipOf(string label) =>
        Ownerships.Cast<Ownership?>().FirstOrDefault(o => OwnershipName(o!.Value) == label);
}
