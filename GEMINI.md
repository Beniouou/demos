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
- Une carte vectorielle continue en coordonnées réelles (lieues), sans cases ni hexagones, enregistrée dans `game/data/carte.json` (`VectorMapDefinition` dans `src/Demos.Domain/Map/MapVectorData.cs`).
- Rendu vectoriel sous deux styles : **IGN Pastel Moderne** et **Parchemin Ancien 1874**.
- Relief ombré physique continu (*Hillshading 3D* Azimut 315° NW, Zénith 45°, méthode de Horn) avec adret crème chaud et ubac bistre/sépia.
- Réseau hydrographique thalweg (creusement de vallées naturelles) et trait de côte avec hauts-fonds/estran et écume marine blanche.
- Les 7 biomes géomorphologiques naturels : Glacier ($\ge 2\,500\text{ m}$), Falaise (pente $\ge 150\%$), Marais ($< 400\text{ m}$, pente $< 30\%$, bord d'eau), Terre fertile ($< 600\text{ m}$, pente $< 60\%$), Plage ($\le 30\text{ m}$, pente $< 40\%$), Désert ($< 250\text{ m}$, pente $< 20\%$), Forêt ($< 2\,500\text{ m}$, pente $< 150\%$).
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
- Les distances : la formule exacte du terme distance dans `Extension.cs` basée sur les coordonnées de la carte.
- La logistique : quels bâtiments (route, voie ferrée, gare, port…) réduisent la perte de 20 % entre communes, et de combien ? Lignes d'approvisionnement avec délais.

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
- **Le terme distance vaut 0 en attendant la carte. Le remplir est à ton chantier.** Une piste, notée dans le
  document : la distance des bâtiments et des communes au centre de l'entité. La formule exacte est à proposer à
  l'utilisateur.
- Chaque préfecture traversée entre l'entité qui décide et la commune fait perdre 10 % d'efficacité.

## Où te brancher dans le code

| Besoin | Endroit |
| --- | --- |
| Terme distance de l'extension | `src/Demos.Domain/Politics/Extension.cs`, dans `ComputeLoad` : `const double distance = 0;`. Remplace-le par une valeur fournie de l'extérieur (par exemple un `Func<string, double>` passé à `Extension.Compute`), plutôt que de mettre la carte dans le domaine politique. |
| Perte de transport entre communes (20 %) | `src/Demos.Domain/Economy/Trade.cs` (`TransportLoss`, réglage `transportLoss`) |
| Pollution venue d'une autre commune (10 %) | `src/Demos.Domain/Economy/Pollution.cs` (`DistantShare`, réglage `distantPollutionShare`) |
| Arbre des entités, commune d'un bâtiment | `src/Demos.Domain/Politics/Structure.cs` (`Level`, `SeatId`), `Communes.cs` (`CommuneOf`, `Within`) |
| Ce que l'interface lit | `src/Demos.Application/GameView.cs` (`StructureTree`, `Communes`, `EntityLoads`, `Prefectures`) |
| Fonder une commune ou une préfecture | `src/Demos.Application/Simulation.cs` (`TryFoundCommune`, `TryFoundPrefecture`, `TryAttach`) |
| Place de la carte à l'écran | `game/scripts/Hud.cs` (« Carte en régions — à venir ») et `game/scripts/MainMenu.cs` (menu Carte) : ce sont des fichiers de l'interface, à voir dans le fichier de coordination avant de les changer. |

## Règles de travail à ne pas oublier

- **Aucun réglage en dur.** Un chiffre d'équilibrage va à trois endroits : `BalanceSettings`
  (`src/Demos.Domain/Economy/Balance.cs`), `game/data/equilibrage.json`, et la feuille Constantes du tableur
  (`AppliedConstants` et `WriteConstants` dans `tools/Demos.Equilibrage/Program.cs`, `Constants` dans `Apply.cs`).
- Les fichiers JSON gardent leur format : une ligne par élément.
- Après chaque changement : `dotnet test tests/Demos.Tests`, puis `dotnet build game`, puis la simulation (`simuler`,
  3 ans). La référence actuelle est un **trésor de 258 846 la 3e année**. Si ce chiffre change, dis-le et explique
  pourquoi. Vérifie aussi que le jeu démarre (`--headless --path . --quit-after 300`, aucune ligne « error »).
- Tout en français : réponses, commentaires, textes, messages de commit. Les noms de classes et de méthodes restent en
  anglais.
