using Demos.Domain.Economy;

namespace Demos.Application.Ports;

/// <summary>
/// Port de sortie : fournit le contenu du jeu (produits, lois, bâtiments…).
/// L'adaptateur JSON de Demos.Infrastructure l'implémente.
/// </summary>
public interface IContentSource
{
    IReadOnlyList<ProductDefinition> LoadProducts();
}
