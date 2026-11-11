using Demos.Domain.Economy;
using Demos.Domain.Map;
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

    /// <summary>La liste des besoins des habitants et leurs noms.</summary>
    IReadOnlyList<NeedDefinition> LoadNeeds();

    /// <summary>Réglages d'équilibrage de l'économie ; ceux par défaut si le fichier n'existe pas.</summary>
    BalanceSettings LoadBalance();

    /// <summary>Le catalogue des lois : domaines, briques et modèles ; vide si le fichier n'existe pas.</summary>
    LawCatalogue LoadLawCatalogue();

    /// <summary>La partie de départ : communes, bâtiments, classes, trésor et lois du départ préréglé.</summary>
    ScenarioDefinition LoadScenario();

    /// <summary>La carte vectorielle géographique du monde ; nulle si le fichier n'existe pas.</summary>
    VectorMapDefinition? LoadMap() => null;

    /// <summary>La carte d'altitude (pour le tracé des routes) ; nulle si elle manque.</summary>
    ReliefGrid? LoadRelief() => null;
}
