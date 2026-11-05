# Projet Démos

Jeu solo de construction de nation, organique et en temps réel avec pause.
Les règles du jeu sont décrites dans le document « Projet Démos — Règles du jeu et glossaire ».

## Organisation (DDD, architecture hexagonale)

| Dossier | Rôle |
| --- | --- |
| `src/Demos.Domain` | Règles du jeu, sans aucune dépendance au moteur : modificateurs, temps, classes, idéologies, produits. |
| `src/Demos.Application` | Simulation (cycle mensuel), état de la partie et ports (interfaces vers l'extérieur). |
| `src/Demos.Infrastructure` | Adaptateurs de sortie : lecture du contenu en JSON. |
| `game/` | Projet Godot .NET : adaptateur d'entrée (horloge, pause, affichage). |
| `game/data/` | Contenu du jeu en JSON : produits (`products.json`) et catalogue des bâtiments (`buildings.json`). |
| `tests/Demos.Tests` | Tests automatiques des règles. |

Règle d'or : `Demos.Domain` ne référence rien. Les autres projets dépendent de lui, jamais l'inverse.

Les écrans du joueur (`game/scripts`) ne voient la partie qu'à travers `GameView` (`Demos.Application`) et
`PlayerDesk` (`game/scripts`), qui ne donnent que des copies : ils ne peuvent rien modifier eux-mêmes. Chaque décision
du joueur (vente, nationalisation, postes ouverts, recette, chauffage) passe par `PlayerDesk`, qui appelle
`Simulation`.

## Prérequis

- [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Godot 4, version .NET](https://godotengine.org/download) (pas la version standard)

Le projet vise Godot 4.7. Avec une version plus récente, Godot propose de mettre le projet à jour à l'ouverture.

## Lancer les tests

```bash
dotnet test
```

## Lancer le jeu

Ouvrir `game/project.godot` dans Godot .NET, puis F5.
Espace met en pause ou relance le temps ; 1 à 4 changent la vitesse (un jour dure 5 s en ×1) ; B construit des logements d'État avec le stock, complété par le trésor ; C bâtit un camp de base qui fonde une nouvelle commune.
Les menus du joueur (carte, lois, structure, production, recherche, ressources, aide) s'ouvrent depuis la colonne de
gauche ; Échap ferme le menu ouvert. Un bâtiment se gère dans Production → Unités de production ; les impôts se
changent par une loi (menu Lois), et Ressources → Revenus montre ce qu'ils rapportent. Le contenu du jeu (emplois, salaires, production, stockage, prix…) se règle dans le tableur
d'équilibrage et l'éditeur de contenu, plus dans le jeu.

## Équilibrer l'économie

Les réglages d'équilibrage (besoins par rang, taille des logements, salaires, encadrement, marge, énergie, saisons,
impôts, réserves…) sont dans `game/data/equilibrage.json`, à côté du catalogue des produits et des bâtiments ; la
partie de départ (communes, bâtiments, classes, lois, trésor) est dans `game/data/partie-depart.json`, et la liste des
besoins des habitants dans `game/data/besoins.json`.

Les outils `tools/Demos.Equilibrage` (trois commandes) et `tools/Demos.Editeur` (l'éditeur de contenu) se lancent chacun par un
fichier `.bat` du dossier `demos` :

| Fichier | Commande | Ce qu'elle fait |
| --- | --- | --- |
| `generer-equilibrage.bat` | `generer` | Refait le tableur `equilibrage demos.xlsx` (à côté du dossier `demos`) depuis les données du jeu. Les modifications non appliquées sont perdues. |
| `appliquer-equilibrage.bat` | `appliquer` | Reporte les cellules jaunes du tableur dans `game/data`. Vérifie d'abord chaque valeur (nombre, pas de négatif, pas de produit inconnu, pas de case vide) : en cas d'erreur, rien n'est écrit et la feuille et la cellule sont indiquées. Affiche chaque changement. |
| `simuler.bat` | `simuler [années] [commune]` | Joue la partie de départ sans joueur (3 ans par défaut, `commune` pour fonder une deuxième commune) et ouvre le rapport `rapport simulation.html` : trésor, caisses, pollution, classes, chômage par rang, production et stocks, bâtiments en difficulté. |
| `editeur.bat` | (éditeur de contenu) | Ouvre une fenêtre pour **créer** un produit, une recette, un type de bâtiment ou un besoin, avec des menus déroulants remplis par le contenu du jeu. « Créer » applique d'abord le tableur, vérifie tout (doublons, cases vides, nombres négatifs, recettes identiques, besoins inconnus), écrit dans `game/data`, puis régénère le tableur. Un nouveau type de bâtiment peut être ajouté à la partie de départ (le jeu n'a pas encore de menu de construction). |

Le tableur recalcule par formules les prix, les coûts, les bilans, la demande et le chômage attendu. Les mots à
choisir (rang, produit, matière, propriétaire, sorte de stockage, oui/non) ont un menu déroulant, et les feuilles sont
protégées sans mot de passe : seules les cases jaunes se modifient. Les colonnes « Clé (ne pas modifier) » servent à
retrouver chaque réglage. Sans les `.bat` :

```bash
dotnet run --project tools/Demos.Equilibrage -- appliquer
```

