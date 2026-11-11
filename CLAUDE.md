# Projet Démos — consignes pour Claude

Jeu solo de construction de nation (Godot .NET 4.7, C#, .NET 10), en français. Le dépôt est `demos/`
(https://github.com/Beniouou/demos, branche `main`). Le README décrit l'organisation ; ce fichier dit comment travailler.

## Façon de travailler avec l'utilisateur

- Tout en **français** : réponses, code (noms de méthodes et classes en anglais, commentaires et textes en français),
  messages de commit.
- **L'utilisateur décide des règles du jeu.** Proposer, expliquer les conséquences, attendre sa validation avant
  de coder une règle nouvelle. Tout chiffre qu'on invente est marqué **« proposition »**.
- La référence des règles est le **document « Projet Démos — Règles du jeu et glossaire »** (Claude Docs, projet
  `47b18403-d084-4409-895c-42a8ad76b73a`, onglet `f081bc6e-8367`). Le tenir à jour après chaque décision : sections
  « (décidé) », questions « À trancher », et « Réglé ». Lire seulement la partie utile (recherche), pas tout.
- **Commit et push uniquement quand l'utilisateur écrit « Commit et push ».** Branche `main`.
- **Plusieurs agents travaillent en même temps** dans ce dossier (par exemple un agent pour l'interface dans
  `game/scripts`, `game/scenes`). Ne jamais faire `git add -A` : ajouter seulement ses propres fichiers, et ne pas
  modifier les fichiers d'un autre chantier sans accord.
- Après un changement d'économie, **vérifier par la simulation** (voir plus bas) et montrer les chiffres avant de
  conclure. Dire honnêtement ce qui empire.

## Environnement (Windows)

- `dotnet` n'est pas dans le PATH des shells Bash : `export PATH="/c/Program Files/dotnet:$PATH"`. Ajouter
  `-nodeReuse:false` aux builds et rediriger l'entrée (`< /dev/null`), sinon la commande peut rester bloquée.
- Godot console : `C:\Users\garyv\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`.
  Vérifier que le jeu démarre : depuis `game/`, `--headless --path . --quit-after 300` (aucune ligne « error »).
- Pas de Python sur la machine. Pour éditer, préférer l'outil Edit ; avec `perl -0pi`, éviter `|` comme délimiteur.
- Les fichiers `.bat` doivent rester en **ASCII** (pas d'accents) : avec `chcp 65001`, les lignes accentuées cassent
  la lecture du fichier. Fins de ligne CRLF.

## Commandes

| But | Commande (depuis `demos/`) |
| --- | --- |
| Tests | `dotnet test tests/Demos.Tests` |
| Compiler le jeu | `dotnet build game` |
| Lancer le jeu | `lancer-jeu.bat` |
| Tableur depuis le jeu | `generer-equilibrage.bat` (ou `dotnet run --project tools/Demos.Equilibrage -- generer`) |
| Tableur vers le jeu | `appliquer-equilibrage.bat` (`-- appliquer`) |
| Simulation et rapport | `simuler.bat [années] [commune]` (`-- simuler`) |
| Éditeur de contenu | `editeur.bat` (fenêtre Windows, `tools/Demos.Editeur`) |
| Éditeur de carte | `carte.bat` (fenêtre Windows, `tools/Demos.Carte`) |
| Test auto de la carte | `dotnet run --project tools/Demos.Carte -- --test-sliders` |
| Relief de la carte (images) | `dotnet run --project tools/Demos.Carte -- --relief` (après chaque changement de `carte.json`) |
| Petites images de la carte | `dotnet run --project tools/Demos.Carte -- --images` (dessine celles qui manquent dans `game/assets/carte/` ; `--forcer` les redessine toutes) |

Le tableur (`equilibrage demos.xlsx`) et le rapport (`rapport simulation.html`) sont **à côté** du dossier `demos/`,
hors du dépôt. Référence actuelle de la simulation : trésor **252 207** la 3e année (`simuler`, 3 ans, départ préréglé, depuis l'atelier
de matériaux d'État du 10 novembre 2026 ; avant : 258 846 ; le chiffre sans loi, 410 815, date d'avant l'atelier).
Avec une deuxième commune (`simuler 3 commune`) : **116 018** depuis les routes (10 novembre 2026 ; 141 622 avant).

## Architecture et données

- `src/Demos.Domain` : règles, sans dépendance (`Economy/`, `Politics/`, `Population/`, `Map/` avec `MapVectorData.cs`).
  `src/Demos.Application` : `Simulation` (journée, mois, saison, année), `World`, `PrototypeScenario`, ports.
  `src/Demos.Infrastructure` : `JsonContentSource`. `game/` : Godot.
  `tools/Demos.Equilibrage` : tableur, appliquer, simuler, création de contenu (`ContentCreator`).
  `tools/Demos.Editeur` : fenêtre de l'éditeur de contenu.
  `tools/Demos.Carte` : éditeur cartographique vectoriel (classes partielles `MapEditorForm`, `Tools`, `IO`, `Testing`).
  `tests/Demos.Tests` : xUnit.
- Contenu dans `game/data/` : `products.json`, `buildings.json`, `besoins.json`, `equilibrage.json` (réglages de
  l'économie, `BalanceSettings`, lus via `Balance.Current`), `partie-depart.json` (partie de départ et lois du départ
  préréglé), `lois.json` (catalogue des lois : domaines, briques, modèles ; voir `Politics/LawBook.cs`),
  `carte.json` (définition vectorielle du monde : `VectorMapDefinition`).
- **Ne pas coder de réglage en dur** : un chiffre d'équilibrage va dans `BalanceSettings` et `equilibrage.json`, et
  dans la feuille Constantes du tableur (clé dans `AppliedConstants`, ligne dans `WriteConstants`, chemin JSON dans
  `BalanceApplier.Constants`). Les formules du tableur doivent suivre le jeu : la colonne « Écart » de la feuille
  Produits doit rester à 0 après `generer`.
- Les fichiers JSON sont écrits par `JsonFormat` (une ligne par élément) : garder ce format si on les modifie à la main.
- Les tests qui changent `Balance.Current` (réglages globaux) vont dans la collection `GlobalBalanceCollection`,
  qui tourne seule.

## Économie : état actuel (résumé)

Prix de départ = coût de revient du bâtiment de référence + 30 % ; entreprises privées et SCOP avec caisse propre
(réserve de 30 jours, le reste aux propriétaires) ; licenciements et embauches au changement de mois (public 2 %,
SCOP 5 % après une baisse de salaire jusqu'à −20 %, privé 15 %), faillite sans argent ; énergie (chauffage, eau) payée
par chaque bâtiment ; impôt sur la richesse des habitants (État 0,3 %, commune 0,2 % par jour). Écrits mais
désactivés : TVA sur la valeur ajoutée, épargne progressive des entreprises. Questions ouvertes principales :
à quoi sert le trésor (l'État ne redépense pas ce qu'il encaisse), prix dynamiques.

## Carte & Géographie : état actuel (résumé)

- Données produites dans `game/data/carte.json` (`VectorMapDefinition` dans `src/Demos.Domain/Map/MapVectorData.cs`) :
  carte vectorielle continue (3 600 × 2 250 lieues), 1 567 contours d'isohypses Marching Squares, 17 cours d'eau thalweg (`AllRivers`), 150 biomes naturels avec trous polygonaux intérieurs (`Biomes` et `Forests`), 66 gisements de sol (`ResourceZones`, `Resources`), communes et toponymie.
- Le moteur Godot affiche la carte vectorielle IGN Pastel au centre de l'écran (`game/scripts/Map/MapCanvas.cs`, `game/scripts/Hud.cs` et menu Carte `game/scripts/MainMenu.cs`) :
  - Enveloppes départementales continues des préfectures fusionnées dynamiquement sans grille (`Geometry2D.MergePolygons`).
  - Liaisons administratives pointillées (`DrawDashedLine`) reliant chaque commune membre au chef-lieu de préfecture.
  - Distinction visuelle entre Capitale nationale (blason or et carmin), Chef-lieu de préfecture (sceau pourpre civique) et Communes ordinaires.
  - Une vue de base, Monde, et des filtres qu'on superpose (`MapFilters`, cases à cocher du menu Carte, légende qui
    suit) : Géographie physique, Biomes, Ressources minières et sols, Politique, Production et transport. Dessin en
    calques, un par fichier, dans `game/scripts/Map/Layers/` (plan : `plan-refonte-carte.md`, à côté du dépôt).
- Calculs géographiques branchés dans la simulation :
  - Terme de distance administrative dans `src/Demos.Domain/Politics/Extension.cs` via `AdministrativeDistance.cs` (2 000 lieues par point de charge, réglage `extensionDistancePerLoad`).
  - Pertes de transport dans `Trade.cs` (5 % à 60 %, réglages `transport…`) et dispersion de pollution dans `Pollution.cs` (loi en $1/(1+(d/250)^2)$) via `GeographicEconomics.cs`.
  - Coordonnées géographiques exposées dans `GameView.cs` (`CommunePositions()`, `CommunePosition(id)`).
- Bâtiments d'extraction posés sur la carte (décidé le 10 novembre 2026) : sur une zone qui convient, à 240 lieues de la
  commune au plus (`extractionMaxDistance`) ; règle dans `Map/ExtractionSites.cs`, mode placement dans
  `game/scripts/Map/MapCanvas.Placement.cs` (Production → Construire ouvre la carte).
- Routes (décidé et codé le 10 novembre 2026, `src/Demos.Domain/Map/Roads.cs`) : le trafic entre communes crée sentier, chemin,
  route (un niveau par mois) ; tracé le moins cher sur la carte d'altitude (`game/data/relief-*.png`, lue par
  `JsonContentSource.LoadRelief`) ; un chemin retire 25 % de la perte de transport, une route 50 % ; construction et
  entretien payés par la plus petite structure qui contient les deux communes (préfecture : l'État paie pour l'instant).
  Chiffres : réglages `road…` de `BalanceSettings` (validés le 11 novembre 2026).
- Chantiers suivants ouverts :
  - Pose interactive d'une commune par clic sur la carte avec validation du terrain constructible.
  - Bonus logistique de transport fluvial le long des 17 cours d'eau navigables.
