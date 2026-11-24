# Projet Démos — consignes pour l'agent Gemini (chantier de la carte)

Tu travailles sur **la carte** du jeu Démos. D'autres agents (Claude) travaillent en même temps dans ce dossier.
Lis d'abord **`CLAUDE.md`** : ses consignes (langue, environnement Windows, commandes, architecture, réglages)
s'appliquent aussi à toi. Ce fichier ajoute ce qui est propre à ton chantier.

## Qui décide

- **L'utilisateur décide des règles du jeu.** Propose, explique les conséquences, attends sa validation avant de coder
  une règle nouvelle. Tout chiffre que tu inventes est marqué **« proposition »**.
- **Claude tranche les conflits** entre ton chantier et celui des autres agents : un fichier que vous modifiez tous
  les deux, une règle qui contredit une règle déjà codée, un choix d'architecture, un test qui casse chez l'autre.
  Dans ces cas, ne tranche pas toi-même : arrête-toi sur ce point et écris-le dans le fichier de coordination
  (voir plus bas), puis continue sur ce qui n'est pas bloqué.
- **Commit et push uniquement quand l'utilisateur écrit « Commit et push ».** Branche `main`. Jamais `git add -A` :
  ajoute seulement tes propres fichiers.

## Fichier de coordination

`C:\Users\garyv\Desktop\Projet Demos\coordination-carte.md`, hors du dépôt. Pour chaque conflit ou question qui touche
un autre chantier, ajoute une entrée datée :

- ce que tu veux faire, et pourquoi ;
- les fichiers concernés (chemin et, si possible, ligne) ;
- ce qui est en conflit (le code ou la règle existante) ;
- ta proposition.

Claude y répond sous l'entrée, en écrivant **« Décision : … »**. Applique la décision telle qu'elle est écrite.

## Ce que tu ne modifies pas sans accord

- `src/Demos.Domain/Politics/` (lois, structures, extension, préfectures) et `src/Demos.Domain/Economy/` : tu peux
  t'y brancher, pas changer le comportement existant. Si tu dois le changer, passe par le fichier de coordination.
- `game/scripts/` et `game/scenes/` hors de la carte : c'est le chantier de l'interface.
- Les formules du tableur (`tools/Demos.Equilibrage`) : la colonne « Écart » de la feuille Produits doit rester à 0.

Tu peux créer tes propres fichiers : par exemple un dossier `src/Demos.Domain/Map/`, des données
`game/data/cartes/<nom>/carte.json`, des scènes et scripts de carte dans `game/`, et leurs tests dans `tests/Demos.Tests/`.

## Les règles déjà écrites qui touchent la carte

Le document de règles de référence est dans Claude Docs, et tu n'y as pas accès. Voici ce qu'il dit d'utile pour toi.

**Carte géographique vectorielle continue (implémentée et opérationnelle)**
- Une carte vectorielle continue en coordonnées réelles (unités de 100 m ; la carte d'origine fait 360 × 225 km, les cartes neuves ont quatre tailles, de 100 × 60 à 1 600 × 1 000 km), sans cases ni hexagones, enregistrée dans `game/data/cartes/<nom>/carte.json` (une carte par dossier, avec ses images de relief ; carte par défaut nommée dans `game/data/cartes/carte-par-defaut.txt`, aujourd'hui `origine` ; chemins dans `src/Demos.Infrastructure/MapFiles.cs` ; `VectorMapDefinition` dans `src/Demos.Domain/Map/MapVectorData.cs`).
- Rendu vectoriel sous deux styles : **IGN Pastel Moderne** et **Parchemin Ancien 1874**.
- Relief ombré physique continu (*Hillshading 3D* Azimut 315° NW, Zénith 45°, méthode de Horn) avec adret crème chaud et ubac bistre/sépia.
- Réseau hydrographique thalweg (creusement de vallées naturelles) et trait de côte avec hauts-fonds/estran et écume marine blanche.
- Les 7 biomes géomorphologiques naturels (pentes en vrais %, mesurées sur 1,2 km ; corrigé le 12 novembre 2026, les anciens seuils valaient 100 fois trop) : Glacier (≥ 2 500 m), Falaise (versant très raide, pente ≥ 8 %), Marais (< 400 m, pente < 1 %, bord d'eau), Terre fertile (< 600 m, pente < 10 %), Plage (≤ 30 m, pente < 2 %), Désert (< 250 m, pente < 2 %), Forêt (< 2 500 m, pente < 8 %). Seuils dans `ZoneMaskGrid` (`CliffMinSlope`…). L'éditeur charge le relief de la carte (images du jeu, ou recalculé depuis les courbes), pas un relief procédural.
- Gestion topologique stricte des trous polygonaux intérieurs (`PolygonArea.Holes`) : les sommets intérieurs ceinturés de falaises sont préservés.

**Éditeur de carte (`tools/Demos.Carte`)**
- Lancé par `carte.bat` ou `dotnet run --project tools/Demos.Carte`.
- Découpé en classes partielles :
  - `MapEditorForm.cs` (base, layout Ligne 1/2, journal),
  - `MapEditorForm.Tools.cs` (gestionnaires des 8 outils),
  - `MapEditorForm.IO.cs` (Mode Île, carte vierge, chargement `carte.json`, export PNG, Undo/Redo),
  - `MapEditorForm.Testing.cs` (harnais de validation automatique).
- Palette des 8 outils : Navigation (`🔍`), Terrain (`⛰️`), Adoucir & Aplanir (`〰️`), Fleuves (`💧`), Biomes (`🗺️`), Gisements (`⛏️`), Communes (`🏛️`), Toponymie (`📍`).
- Paramètres procéduraux : Graine (`seed`), 4 morphologies d'îles (`Natural`, `AlpineRidge`, `Archipelago`, `Fjords`), plafond d'altitude, 3 profils de pinceau (`DomeCos`, `ConeLinear`, `PlateauFlat`).
- Test automatisé complet : `dotnet run --project tools/Demos.Carte -- --test-sliders`.

**Questions encore ouvertes (à faire trancher par l'utilisateur)**
- Le découpage en régions politiques et administratives au-dessus du fond vectoriel.
- Où se bâtit un camp de base : n'importe où, ou sur une région libre ? Faut-il une distance minimale entre communes ?
- La logistique : quels bâtiments (route, voie ferrée, gare, port…) réduisent la perte entre communes, et de combien ? Lignes d'approvisionnement avec délais.

**Distances (décidé par l'utilisateur le 10 novembre 2026)**
- **Perte de transport entre communes** : 20 % × (0,5 + 0,5 × distance ÷ 50 km), bornée entre 5 % et 60 %
  (environ 11 % à 5 km, 20 % à 50 km, 40 % à 150 km). Sans position sur la carte, la perte reste de 20 %.
- **Pollution venue d'une autre commune** : 10 % ÷ (1 + (distance ÷ 25 km)²), en gardant le plafond (jamais plus,
  par habitant, que dans la commune où elle est produite).
- **Terme distance de l'extension** : distance au chef-lieu (la capitale pour l'État) ÷ **200 km** par point de
  charge, et non 50 : à 50 km, une seule commune à 80 km faisait tomber le trésor de 153 838 à 51 067 en 3 ans.
- Ces chiffres sont des réglages (`transportLoss`, `transportReferenceDistance`, `transportLossMin`, `transportLossMax`,
  `distantPollutionShare`, `pollutionDispersionDistance`, `extensionDistancePerLoad`) dans `BalanceSettings`,
  `equilibrage.json` et la feuille Constantes du tableur. Ne pas les remettre en constantes dans le code.
- La capitale est le chef-lieu de l'État (`Structure.SeatId` de l'État, `World.CapitalId`, `GameView.CapitalId`) :
  ne pas la reconnaître par l'identifiant « commune-1 ».

**Bâtiments d'extraction sur la carte (décidé par l'utilisateur le 10 novembre 2026, codé par Claude)**
- Un camp de bûcheron se pose sur une forêt, une ferme (ou entreprise agricole) sur une terre fertile, une carrière sur
  un gisement de pierre, une mine sur un gisement de charbon ou de fer selon sa recette. C'est **obligatoire**.
- Il se pose à **20 km** de sa commune au plus (réglage `extractionMaxDistance`). La distance n'a **aucun effet** sur
  la production pour l'instant. Une zone peut être partagée par plusieurs bâtiments et plusieurs communes.
- Les bâtiments d'extraction du départ sont posés automatiquement sur la zone qui convient la plus proche de leur
  commune. Tour de forage et puits : sans zone pour l'instant.
- Code : `src/Demos.Domain/Map/ExtractionSites.cs` (la règle), `World.BuildingPositions`, `Simulation.TryBuild(…, position)`,
  `game/scripts/Map/MapCanvas.Placement.cs` (icônes et mode placement). Ne pas retirer `TryPlaceAt` du clic de `MapCanvas`.

**Terre d'exil (décidé par l'utilisateur le 23 novembre 2026, pas encore codé)**
- Le jeu a deux modes : avec La Promesse (mode principal) et bac à sable. En mode avec Promesse, la partie commence
  pauvre (départ d'exil) sur une **terre d'exil** : une zone ingrate, avec peu de terre fertile et loin des gisements
  (proposition : gisements à plus de 20 km du camp, atteignables en fondant une commune) ; les terres riches existent
  plus loin.
- Chaque carte porte **1 à 3 terres d'exil, marquées à la main dans l'éditeur de carte**, chacune avec sa zone
  d'arrivée, son nom et l'emplacement du camp de base.
- L'éditeur **calcule et affiche leur pauvreté** pour vérifier qu'elles sont jouables (terre fertile et forêt à portée,
  distance aux gisements de fer, de charbon et de pierre, emplois de ferme possibles avec les zones limitées) et
  **prévient quand un départ est injouable**.
- **Cartes au hasard (décidé le 23 novembre 2026) : le hasard se fait dans l'éditeur, pas dans le jeu.** L'éditeur
  génère une carte (relief, biomes, fleuves, gisements, terres d'exil proposées d'après leur pauvreté) ; l'utilisateur
  la retouche, la vérifie, puis l'enregistre sous un nom. Le jeu ne propose que des cartes enregistrées et ne calcule
  rien au lancement. Le même générateur pourra plus tard être appelé par le jeu : le garder indépendant de la fenêtre.
- **Départ en mode avec Promesse (décidé le 23 novembre 2026)** : le joueur choisit la carte parmi les cartes
  enregistrées, puis la terre d'exil est **tirée au hasard** parmi celles de cette carte. Toutes les terres d'exil d'une
  carte doivent donc être jouables : la fiche de pauvreté de l'éditeur le vérifie.
- **Départ du bac à sable (décidé le 23 novembre 2026)** : chaque carte a aussi **un départ riche**, marqué dans
  l'éditeur, où le départ préréglé (`partie-depart.json`) doit tenir : chaque bâtiment d'extraction y trouve sa zone et
  ses emplois (zones limitées). En bac à sable, le joueur choisit la carte, puis le départ riche (départ préréglé) ou
  une terre d'exil de la carte (départ d'exil, sans Promesse). La carte actuelle devient la première carte enregistrée,
  son point actuel (`defaultCommune`) son départ riche (proposition).
- **Codé par Claude le 23 novembre 2026** : les points 1 à 3 ci-dessous (outil « Départs », fiche, verdict).
  Données : `ExileSites` dans `VectorMapDefinition` (`ExileSite`, champ `exileSites` facultatif) ; règle :
  `src/Demos.Domain/Map/StartSites.cs` (`Report`, `Judge`, `PresetProblems`, seuils `ExileThresholds`) ; éditeur :
  `EditorTool.PlaceStart`, `MapCanvasControl.Starts.cs`, `MapEditorForm.Starts.cs`. Le départ riche est la commune de
  base (`DefaultCommune`, outil Communes). Restent les points 4 et 5.
- **Ce que l'éditeur doit permettre (décidé le 23 novembre 2026)** :
  1. Un 9e outil « Départs » : poser le départ riche (un par carte) et les terres d'exil (1 à 3), chacun avec un nom.
     Une terre d'exil est l'emplacement du camp ; sa zone est dessinée autour (ressort de 8 km, portée d'extraction de
     20 km), sans contour à la main (proposition).
  2. Une fiche par départ, recalculée à chaque retouche : terre fertile et forêt à moins de 8 km et de 20 km (km² ; 8 km
     car le départ d'exil n'a pas de loi d'extension), emplois de ferme et de bûcheron possibles d'après les zones
     limitées (`Map/ZoneCapacity`, densités `zoneDensity…`, rayon `zoneRadius`), distance au gisement de fer, de charbon
     et de pierre le plus proche, côte, fleuve, altitude. Lire les réglages dans `equilibrage.json`, pas en dur.
  3. Un verdict : départ riche, le départ préréglé tient-il (chaque bâtiment d'extraction trouve sa zone et ses
     emplois) ; terre d'exil, jouable, injouable ou trop facile, selon des seuils (proposition, à régler par la
     simulation : au moins 100 emplois de ferme à moins de 8 km ; fer au-delà de 20 km mais à moins de 40 km ; une
     forêt et de la pierre à moins de 20 km).
  4. « Nouvelle carte au hasard » : relief et biomes (existent), fleuves qui coulent des sommets vers la mer, gisements
     de fer, de charbon et de pierre placés selon le relief en nombre réglable, départ riche et 3 terres d'exil proposés
     d'après leur fiche. Retouche ensuite avec les outils existants.
  5. Plusieurs cartes (codé par Claude le 24 novembre 2026, `MapEditorForm.Maps.cs` : Ouvrir, Enregistrer dans la carte ouverte, Enregistrer sous, ★ Par défaut, relief refait à chaque enregistrement ; `--relief [carte]`) : « Enregistrer sous » un nom, un dossier par carte (`game/data/cartes/<nom>/` : carte et images
     de relief, proposition) ; ouvrir une carte enregistrée.
  6. Lacs (décidé et codé le 24 novembre 2026) : un outil « Lac » à part (l'outil du niveau de la mer ne convient
     pas : un lac a son propre niveau, au-dessus de la mer). Un clic dans une cuvette la remplit jusqu'à son seuil (le
     point le plus bas de son bord, par où elle déborderait) ; un curseur baisse le niveau pour un lac plus petit. Les
     lacs se posent avant les fleuves. Sortie : le curseur décide (décidé le 24 novembre 2026) — un lac au niveau de
     son seuil a toujours un fleuve de sortie, créé tout seul au seuil ; un lac baissé sous son seuil est fermé, sans
     sortie. Un fleuve qui arrive à un lac s'y arrête. Tracé des fleuves (décidé le 24 novembre 2026) : vers la mer, le
     bord ou un lac, par le chemin qui creuse le moins (le point le plus haut à franchir est le plus bas ; il suit la
     pente là où le terrain descend, une cuvette se vide par son col le plus bas) ; à pente identique, il vise la mer ;
     même règle pour le fleuve de sortie d'un lac. Remplace le tracé par pente et élan. Dans le jeu (décidé le 24 novembre 2026) : un lac compte comme
     de l'eau, pas de commune dessus, les routes le contournent, il est navigable avec les fleuves (quand le transport
     fluvial sera codé), ses rives ne sont pas une côte pour une terre d'exil. Codé par Claude le 24 novembre 2026
     (éditeur : outil « Lacs », `MapEditorForm.Lakes.cs`, `Heightmap.Water.cs` ; jeu : `LakesLayer`, `ReliefGrid.SetLakes`,
     `CommuneSites`, `RoadPlanner`), sauf la navigation.
  Le choix de la carte et le tirage de la terre d'exil se font dans le jeu (écran de départ, chantier de l'interface) ; le
  choix de la carte est codé (24 novembre 2026, écran « Nouvelle partie », `game/scripts/NewGameScreen.cs`), le tirage pas encore.
- **Côte et désert (décidé le 23 novembre 2026)** : chaque carte a une côte et un désert (biome Désert), que le
  générateur garantit et que l'éditeur vérifie, pour les Grands Œuvres « Vaisseau du Second Exil » et « Terre Rendue ».
  Une autre rive (terre séparée par l'eau, pour le « Pont vers l'Autre Rive ») est facultative ; un Grand Œuvre que la
  carte ne permet pas n'est pas proposé. Tailles des cartes neuves (décidé le 24 novembre 2026, remplace
  « 360 × 225 km ») : département 100 × 60 km, petit pays 240 × 150 km, grand pays 800 × 500 km, petit continent
  1 600 × 1 000 km. Les règles (en km) valent partout ; l'équilibrage reste à vérifier par la simulation. La carte
  d'origine garde 360 × 225 km.
- Points à reprendre en codant :
  - la fiche vérifie qu'un passage par la terre (pente de 12 % au plus, pas de mer, comme les routes de `Roads.cs`) relie
    la terre d'exil à ses gisements, sinon aucune route ne s'y fera (proposition) ;
  - l'eau ne bloque ni la fondation sur une autre rive (`CommuneSites` ne contrôle que l'emplacement) ni les échanges à
    vol d'oiseau : à régler avec le chantier du port et du transport fluvial, sans le changer seul ;
  - le transport fluvial rendra moins ingrate une terre d'exil au bord d'un fleuve : seuils de pauvreté à revoir ;
  - le jeu ne lit qu'un jeu d'images de relief à un chemin fixe (`JsonContentSource.LoadRelief`,
    `game/scripts/Map/Layers/MapRelief.cs`) : à changer pour plusieurs cartes, en passant par le fichier de coordination.
- À trancher : les terres d'exil de la carte actuelle, les seuils de pauvreté.

**Structures politiques (décidé et codé)**
- L'État contient des communes et des **préfectures**. Une préfecture contient des communes ou d'autres préfectures,
  empilées librement. Tout habitant vit dans une commune.
- Une préfecture a un **chef-lieu** (`Structure.SeatId`) : la commune où se trouve son bâtiment.
- Fonder une commune, c'est bâtir un **camp de base**. Une commune sans habitants devient une ville fantôme.

**Extension et bureaucratie (décidé et codé)**
- Chaque entité politique a une **charge** et une **efficacité**. L'efficacité vaut 1 jusqu'à une charge de 1, puis
  1 / charge.
- La charge vaut : population comptée / 20 000 + bâtiments publics directs / 40 + 0,25 × bâtiments autonomes / 40 +
  0,1 × la charge hors population des entités contenues + **distance**.
- **Le terme distance est maintenant branché** : calculé par `AdministrativeDistance.cs` et injecté via
  `Func<string, double>? distanceOf` dans `Extension.Compute`. Réglage décidé : **200 km par point de charge** ; c'est le réglage `extensionDistancePerLoad`.
- Chaque préfecture traversée entre l'entité qui décide et la commune fait perdre 10 % d'efficacité.

**Échelle de la carte (décidé le 12 novembre 2026 ; la « lieue » est abandonnée)**
- Une unité de carte vaut 100 m (`src/Demos.Domain/Map/MapScale.cs`) : la carte d'origine fait 360 × 225 km. Les règles et les
  réglages de distance sont en km ; les coordonnées restent en unités, converties par `MapScale`. Ne plus écrire « lieue ».
- Ressort d'une commune : 8 km ; d'une préfecture : 40 km autour du chef-lieu, plus les ressorts de ses communes.
- Maisons et arbres sont presque à l'échelle (un toit figure un îlot de 100 m, un arbre un bosquet). Tailles dans
  `MapPalette` ; une barre d'échelle en km est en bas à gauche de la carte.

**Routes (décidé et codé le 10 novembre 2026)** — règle dans `src/Demos.Domain/Map/Roads.cs`
- Le trafic les crée : quand deux communes échangent assez en un mois, un sentier apparaît, puis un chemin, puis une
  route (un niveau par mois au plus). Seuils validés (11 novembre 2026) : 1 000, 4 000 et 9 000 produits par mois.
- Le tracé suit le chemin le moins cher sur la carte d'altitude (`game/data/cartes/<nom>/relief-altitude.png`) : un pas coûte sa
  longueur × (1 + 0,1 × pente), une pente de plus de 12 % et la mer sont interdites, une voie existante coûte
  moitié prix (le réseau se ramifie).
- Effet : un chemin retire 25 % de la perte de transport, une route 50 %.
- Coût : construction et entretien selon la longueur et la montée, payés par la plus petite structure qui contient les
  deux communes (commune, préfecture, sinon l'État). Une préfecture n'a pas encore de caisse : l'État paie pour elle.
- Tous les chiffres, validés le 11 novembre 2026, sont des réglages `road…` (`BalanceSettings`, `equilibrage.json`,
  feuille Constantes). La carte lit
  les routes dans `GameView.Roads()` et les échanges dans `GameView.TradeFlows()` (calque `TransportLayer`).

## Où te brancher dans le code

| Besoin | Endroit | État |
| --- | --- | --- |
| Terme distance de l'extension | `src/Demos.Domain/Politics/Extension.cs` | **Branché** (`AdministrativeDistance.cs`, paramètre `distanceOf`) |
| Perte de transport entre communes | `src/Demos.Domain/Economy/Trade.cs` | **Branché** (`GeographicEconomics.TransportLoss(dist)`) |
| Pollution venue d'une autre commune | `src/Demos.Domain/Economy/Pollution.cs` | **Branché** (`GeographicEconomics.DistantPollutionShare(dist)`) |
| Coordonnées géoréférencées | `src/Demos.Application/GameView.cs` | **Branché** (`CommunePositions()`, `CommunePosition(id)`) |
| Rendu de la carte vectorielle | `game/scripts/Map/MapCanvas.cs` | **Branché** (Préfectures sans grille `MergePolygons` ; vue Monde et filtres superposables, calques dans `Map/Layers/`) |
| Place de la carte à l'écran | `game/scripts/Hud.cs` et `game/scripts/MainMenu.cs` | **Branché** (`MapCanvas` initialisé et rafraîchi en direct) |
| Fonder une commune ou une préfecture | `src/Demos.Application/Simulation.cs` | `TryFoundCommune` (avec `Point2D? position`), `TryFoundPrefecture` |

## Règles de travail à ne pas oublier

- **Aucun réglage en dur.** Un chiffre d'équilibrage va à trois endroits : `BalanceSettings`
  (`src/Demos.Domain/Economy/Balance.cs`), `game/data/equilibrage.json`, et la feuille Constantes du tableur
  (`AppliedConstants` et `WriteConstants` dans `tools/Demos.Equilibrage/Program.cs`, `Constants` dans `Apply.cs`).
- Les fichiers JSON gardent leur format : une ligne par élément.
- Après chaque changement : `dotnet test tests/Demos.Tests`, puis `dotnet build game`, puis la simulation (`simuler`,
  3 ans). La référence actuelle est un **trésor de 252 207 la 3e année**. Si ce chiffre change, dis-le et explique
  pourquoi. Vérifie aussi que le jeu démarre (`--headless --path . --quit-after 300`, aucune ligne « error »).
- Tout en français : réponses, commentaires, textes, messages de commit. Les noms de classes et de méthodes restent en
  anglais.
