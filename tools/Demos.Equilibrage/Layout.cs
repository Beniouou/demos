using ClosedXML.Excel;

/// <summary>
/// Les colonnes du tableur qui dépendent du nombre de matières par recette : le générateur les écrit, « appliquer »
/// les relit.
/// </summary>
public static class Layout
{
    /// <summary>Matières au plus par recette : autant de colonnes dans le tableur, de lignes dans l'éditeur.</summary>
    public const int MaxMaterials = 5;

    public static string Letter(int column) => XLHelper.GetColumnLetterFromNumber(column);

    // Feuille Recettes : la matière m (à partir de 0) et sa quantité, à partir de la colonne I ; puis les calculs et la clé.
    public static int RecipeMaterial(int m) => 9 + 2 * m;
    public static int RecipeQuantity(int m) => 10 + 2 * m;
    public const int RecipeProduction = 9 + 2 * MaxMaterials;
    public const int RecipeKey = RecipeProduction + 7;

    // Feuille Produits : la matière m et sa quantité, à partir de la colonne K ; puis les calculs du prix.
    public static int ProductMaterial(int m) => 11 + 2 * m;
    public static int ProductQuantity(int m) => 12 + 2 * m;
    public const int ProductWages = 11 + 2 * MaxMaterials;
    public const int ProductPrice = ProductWages + 3;

    // Feuille Partie de départ : la matière m et sa quantité, à partir de la colonne J ; la production et les matières
    // par jour ; puis l'énergie, les emplois, les comptes, la clé et la classe propriétaire.
    public static int StartMaterial(int m) => 10 + 2 * m;
    public static int StartQuantity(int m) => 11 + 2 * m;
    public const int StartProduction = 10 + 2 * MaxMaterials;
    public static int StartMaterialPerDay(int m) => StartProduction + 1 + m;
    public const int StartFuel = StartProduction + MaxMaterials + 1;
    public const int StartKey = StartFuel + 13;
    public const int StartOwnerClass = StartFuel + 14;
}
