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
`game/data/carte.json`, des scènes et scripts de carte dans `game/`, et leurs tests dans `tests/Demos.Tests/`.

## Les règles déjà écrites qui touchent la carte

Le document de règles de référence est dans Claude Docs, et tu n'y as pas accès. Voici ce qu'il dit d'utile pour toi.

**Carte géographique vectorielle continue (implémentée et opérationnelle)**
- Une carte vectorielle continue en coordonnées réelles (unités de 100 m : 360 × 225 km), sans cases ni hexagones, enregistrée dans `game/data/carte.json` (`VectorMapDefinition` dans `src/Demos.Domain/Map/MapVectorData.cs`).
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
- Une unité de carte vaut 100 m (`src/Demos.Domain/Map/MapScale.cs`) : la carte fait 360 × 225 km. Les règles et les
  réglages de distance sont en km ; les coordonnées restent en unités, converties par `MapScale`. Ne plus écrire « lieue ».
- Ressort d'une commune : 8 km ; d'une préfecture : 40 km autour du chef-lieu, plus les ressorts de ses communes.
- Maisons et arbres sont presque à l'échelle (un toit figure un îlot de 100 m, un arbre un bosquet). Tailles dans
  `MapPalette` ; une barre d'échelle en km est en bas à gauche de la carte.

**Routes (décidé et codé le 10 novembre 2026)** — règle dans `src/Demos.Domain/Map/Roads.cs`
- Le trafic les crée : quand deux communes échangent assez en un mois, un sentier apparaît, puis un chemin, puis une
  route (un niveau par mois au plus). Seuils validés (11 novembre 2026) : 1 000, 4 000 et 9 000 produits par mois.
- Le tracé suit le chemin le moins cher sur la carte d'altitude (`game/data/relief-altitude.png`) : un pas coûte sa
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
