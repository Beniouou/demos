using Demos.Domain.Economy;
using Demos.Domain.Politics;

namespace Demos.Application.Ports;

/// <summary>
/// Port de sortie : fournit le contenu du jeu (produits, types de bâtiments, lois…).
/// L'adaptateur JSON de Demos.Infrastructure l'implémente.
/// </summary>
public interface IContentSource
{
    IReadOnlyList<ProductDefinition> LoadProducts();

    IReadOnlyList<BuildingType> LoadBuildingTypes();

    /// <summary>Réglages d'équilibrage de l'économie ; ceux par défaut si le fichier n'existe pas.</summary>
    BalanceSettings LoadBalance();
}
