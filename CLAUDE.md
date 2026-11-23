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
| Gestionnaire d'icônes | `icones.bat` (fenêtre application 74 logos, `galerie-icones.html` ; `remplacer.bat` pour remplacement direct) |
| Test auto de la carte | `dotnet run --project tools/Demos.Carte -- --test-sliders` |
| Relief de la carte (images) | `dotnet run --project tools/Demos.Carte -- --relief` (après chaque changement de `carte.json`) |
| Petites images de la carte | `dotnet run --project tools/Demos.Carte -- --images` (dessine celles qui manquent dans `game/assets/carte/` ; `--forcer` les redessine toutes) |

Le tableur (`equilibrage demos.xlsx`) et le rapport (`rapport simulation.html`) sont **à côté** du dossier `demos/`,
hors du dépôt. Référence actuelle de la simulation : trésor **254 865** la 3e année (`simuler`, 3 ans, départ préréglé, depuis la crise, l'école et le seuil de pollution à 0,3 du 23 novembre 2026 ; 255 091 depuis les moteurs de l'axe Propriété ; 252 286 depuis les
objets durables et la chaîne du fer réduite du 20 novembre 2026 ; 252 563 avec la chaîne à 10, 3 et 3 emplois ; 255 632 avec les seuls biens durables ; 251 118 depuis l'ordre
de la journée par étapes de la chaîne ; avant : 251 177 depuis les routes des bâtiments, 252 207 depuis
l'atelier de matériaux d'État, 258 846 avant lui ; le chiffre sans loi, 410 815, date d'avant l'atelier). Avec une deuxième
commune (`simuler 3 commune`) : **118 701** depuis la crise et le seuil de pollution (114 571 depuis les moteurs de l'axe Propriété, 114 738 depuis les objets durables, 114 268 depuis la durée des trajets du 20 novembre 2026, 114 271 depuis les échanges de
matières, 114 954 avant eux,
114 989 avant l'ordre par étapes, 116 018 avant les routes des bâtiments, 141 622 avant les routes).

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
  `carte.json` (définition vectorielle du monde : `VectorMapDefinition`), `promesses.json` (les 26 Promesses des exilés,
  une par combinaison de 1 à 3 pôles, et leurs noms : `Politics/PromiseDefinition.cs`, `LoadPromises`, feuille Promesses du
  tableur où le nom se change ; sans effet sur la partie pour l'instant). `engagements.json` : les 42 engagements de La Promesse
  (`Politics/EngagementDefinition.cs`, de principe ou concrets, par pôle ; `LoadEngagements`, feuille Engagements du tableur ;
  sans effet sur la partie pour l'instant).
- Codes des pôles (`Orientation.Code`) : EG, SE, PO, EC, **CO** (collectiviste, ancien PR « protectionniste », renommé
  le 23 novembre 2026 partout : code, `lois.json`, `promesses.json`, icône `pole-co-collectiviste.svg`), LI. Axes
  indépendants : la redistribution relève de la Hiérarchie, la Propriété de qui possède et gère ; chaque brique de
  `lois.json` ne touche qu'un axe (test `Chaque_brique_de_loi_ne_touche_qu_un_seul_axe`). Les moteurs de l'axe Propriété
  dans `ClassTransitions` (23 novembre 2026) : expérience du travail (`isStruggling` = `World.Wages.IsStruggling`, lieu
  public ou SCOP qui va bien → CO, en difficulté → LI, l'inverse pour le privé) et propriété (`OwnerClassId`) ; le manque
  d'argent et le chômage (la crise) poussent vers EG ou SE selon la confiance (adhésion) et le déclassement ; les
  prélèvements lourds vers SE ; l'école enseigne La Promesse (`World.Promise`, `SocialClass.SeasonPupils`). Tous les réglages
  des passages sont dans `BalanceSettings.Transition…` (feuille Passages du tableur, `Book.TransitionRows`, `ApplyTransitions`) ;
  `pollutedThreshold` 0,3 (écologie en retrait). Pas codés : licenciés, expropriation.
- **Ne pas coder de réglage en dur** : un chiffre d'équilibrage va dans `BalanceSettings` et `equilibrage.json`, et
  dans la feuille Constantes du tableur (clé dans `AppliedConstants`, ligne dans `WriteConstants`, chemin JSON dans
  `BalanceApplier.Constants`). Les formules du tableur doivent suivre le jeu : la colonne « Écart » de la feuille
  Produits doit rester à 0 après `generer`.
- Les fichiers JSON sont écrits par `JsonFormat` (une ligne par élément) : garder ce format si on les modifie à la main.
- Les tests qui changent `Balance.Current` (réglages globaux) vont dans la collection `GlobalBalanceCollection`,
  qui tourne seule.

## Économie : état actuel (résumé)

Nouvelles communes (20 novembre 2026) : `Simulation.Build(structure, mode, position, size)` → `ConstructionOutcome` ; matériaux
selon `Simulation.PlanConstruction` (`ConstructionPlan` : réserve, stock de l'État, achat ici, puis la commune la plus proche qui
échange, avec perte, trajet ajouté au chantier, une journée de capacité au plus ; texte `GameView.DescribePlan`) ; taille 10 % à
100 % (`BuildingType.Sized`, `ConstructionQuantity`, réglages `buildingMinUnits/Materials`) ; zones limitées (`Map/ZoneCapacity`,
`World.ZoneUsage` / `ZoneProblem`, rayon `zoneRadius`, densités `zoneDensityFertile/Forest/Deposit`) ; les outils comptent dans
`Trade.BuildingDemand`. Interface : `ConstructionPage` (taille − +, origine des matériaux, zone vérifiée à la pose).
Familles de produits (21 novembre 2026, sans effet sur la partie) : `ProductDefinition.Family` (`ProductFamily`, champ
`family` de `products.json`, test `Chaque_produit_a_une_famille`) ; noms et rangement dans `game/scripts/ProductFamilies.cs` ;
Ressources → Stocks (`StocksPage` : famille → produit avec son total → répartition par commune ; stockage des communes en
bas) et Communes → Statistiques (`ProductionStatsPage`) rangés par famille, repliables.
Objets durables (20 novembre 2026) : `ProductDefinition.UsageDays` (vêtements, meubles, poterie, outils, charrette) ;
habitants : `SocialClass.Possessions`, possession visée `possessionByRank`, rachat de l'usure + `possessionCatchUp` du manque
(`Consumption`, seulement si le produit a une durée d'usage), équipés au départ (`Consumption.EquipFully`) ; bâtiments :
`Tools` (stock `World.ToolStockOf`, `toolsPerJob`, pénalité `toolShortagePenalty` via `World.ToolEffectOf` dans `WorkDay`,
stock offert au départ et en fin de chantier) ; chaîne du fer d'État au départ (mine de fer 3 emplois, forge 1, atelier d'outils 1) ;
charrettes : `Carts`, `World.CartStockOf` / `SetWantedCarts` (`Simulation.SetWantedCarts`, `PlayerDesk.SetWantedCarts`),
`cartCapacity` ajouté à `World.ShippingCapacity`. Tableur : colonne « Durée d'usage (jours) » de Produits
(`Layout.ProductUsage`), constantes `Possession…`, `Outils…`, `CapaciteCharrette`.
Ordre de la journée (20 novembre 2026, `WorkDay.Run`) : les bâtiments travaillent par étapes de la chaîne
(`WorkDay.ChainStages`), la production d'une étape sert la suivante le jour même ; dans une étape, une matière qui manque
est partagée au prorata du besoin (`ShortageRatios`, plafond `marketCap` de `Supply.Acquire`), puis ce qui reste est
offert aux autres. Échanges entre communes (`Trade.RunDaily`, le soir après les habitants) : tous les produits qui se
stockent, au-delà de la demande de demain (habitants + `Trade.BuildingDemand`) moins la production du jour ; seulement si
l'une des deux communes a un bâtiment de catégorie Logistique (`Trade.CanTrade`, `World.HasLogisticsBuilding`, aussi pour
les lignes) ; les chargements (`Shipment`, `World.ShipmentsInTransit`, aussi ceux des lignes entre communes, vers la réserve
du client) prennent `Roads.TravelDays` jours (distance ÷ vitesse de la liaison : réglages `transportSpeedTrail/Path/Road`,
`Simulation.TravelDaysBetween`) et avancent chaque matin (`Trade.Advance`, péremption de chaque nuit comptée en perdu en
route) ; une commune commande sa demande nette × la durée du trajet, moins son stock et ce qui est déjà en route.
Capacité d'envoi (`World.ShippingCapacity` : réglages `shippingCapacityLevel1/2` selon le niveau des bâtiments logistiques ;
`Trade.CapacityHolder` : celle de la commune qui envoie, sinon de celle qui reçoit), prise d'abord par les lignes entre
communes puis partagée au prorata entre les échanges du soir ; utilisée le dernier jour : `World.ShippingCapacityUsed`.
Interface (20 novembre 2026) : Communes → Transport (`TransportPage`) — colonne « Durée » des liaisons, sections « Capacité
d'envoi » et « Chargements en route » (`GameView.TravelDays`, `ShippingCapacityOf`, `ShipmentsFrom` / `ShipmentsTo`) ;
Statistiques — colonne « En route » (`ProductFlowView.InTransit`, relevé comme le stock). Prix de départ = coût de revient du bâtiment de référence + 30 % ; entreprises privées et SCOP avec caisse propre
(réserve de 30 jours, le reste aux propriétaires) ; licenciements et embauches au changement de mois (public 2 %,
SCOP 5 % après une baisse de salaire jusqu'à −20 %, privé 15 %), faillite sans argent ; énergie (chauffage, eau) payée
par chaque bâtiment ; impôt sur la richesse des habitants (État 0,3 %, commune 0,2 % par jour). Écrits mais
désactivés : TVA sur la valeur ajoutée, épargne progressive des entreprises. Questions ouvertes principales :
à quoi sert le trésor (l'État ne redépense pas ce qu'il encaisse), prix dynamiques.

## Carte & Géographie : état actuel (résumé)

- Données produites dans `game/data/carte.json` (`VectorMapDefinition` dans `src/Demos.Domain/Map/MapVectorData.cs`) :
  carte vectorielle continue (3 600 × 2 250 unités de 100 m, soit 360 × 225 km), 1 567 contours d'isohypses Marching Squares, 17 cours d'eau thalweg (`AllRivers`), 150 biomes naturels avec trous polygonaux intérieurs (`Biomes` et `Forests`), 66 gisements de sol (`ResourceZones`, `Resources`), communes et toponymie.
- Le moteur Godot affiche la carte vectorielle IGN Pastel au centre de l'écran (`game/scripts/Map/MapCanvas.cs`, `game/scripts/Hud.cs` et menu Carte `game/scripts/MainMenu.cs`) :
  - Enveloppes départementales continues des préfectures fusionnées dynamiquement sans grille (`Geometry2D.MergePolygons`).
  - Liaisons administratives pointillées (`DrawDashedLine`) reliant chaque commune membre au chef-lieu de préfecture.
  - Blason rond de chaque commune, dans toutes les vues (19 novembre 2026, `Map/Layers/CommuneBlason.cs`, dessiné avec
    les écritures, au-dessus des bâtiments) : champ en deux émaux (un métal, une couleur, `MapPalette.HeraldicMetals` /
    `HeraldicColours`) et partage (coupé, parti, tranché, écartelé) tirés du nom ; figure selon le produit le plus
    fabriqué en valeur depuis le début (épi, arbre, tour, marteau, goutte, flamme ; étoile sans production), relue une
    fois par jour ; bordure or pour la capitale, pourpre pour un chef-lieu, argent sinon. Remplace les anciens sceaux.
  - Une vue de base, Monde, et des filtres qu'on superpose (`MapFilters`, cases à cocher du menu Carte, légende qui
    suit) : Géographie physique, Biomes, Ressources minières et sols, Politique, Production et transport. Dessin en
    calques, un par fichier, dans `game/scripts/Map/Layers/` (plan : `plan-refonte-carte.md`, à côté du dépôt).
- Calculs géographiques branchés dans la simulation :
  - Terme de distance administrative dans `src/Demos.Domain/Politics/Extension.cs` via `AdministrativeDistance.cs` (200 km par point de charge, réglage `extensionDistancePerLoad`).
  - Pertes de transport dans `Trade.cs` (5 % à 60 %, réglages `transport…`) et dispersion de pollution dans `Pollution.cs` (part ÷ (1 + (d ÷ 25 km)²)) via `GeographicEconomics.cs`.
  - Coordonnées géographiques exposées dans `GameView.cs` (`CommunePositions()`, `CommunePosition(id)`).
- Bâtiments d'extraction posés sur la carte (décidé le 10 novembre 2026) : sur une zone qui convient, à 20 km de la
  commune au plus (`extractionMaxDistance`) ; règle dans `Map/ExtractionSites.cs`, mode placement dans
  `game/scripts/Map/MapCanvas.Placement.cs` (Communes → Construire ouvre la carte).
- Échelle (décidé le 12 novembre 2026, remplace la « lieue ») : une unité de carte vaut 100 m (`Map/MapScale.cs`), la
  carte fait 360 × 225 km. Les règles et les réglages de distance sont **en km** (`equilibrage.json`, tableur) ; les
  coordonnées restent en unités et sont converties par `MapScale` (`KmBetween`, `ToKm`, `ToUnits`, `Format`). Ne plus
  écrire « lieue ». Ressorts : commune 8 km, préfecture 40 km autour du chef-lieu (`communeJurisdictionRadius`,
  `prefectureJurisdictionRadius`). Maisons et arbres presque à l'échelle (tailles dans `MapPalette`). Barre
  d'échelle : `game/scripts/Map/MapCanvas.Scale.cs`.
- Éditeur de carte : au chargement, il prend le relief de la carte (`relief-altitude.png` s'il est plus récent que
  `carte.json`, sinon `ReliefBuilder`), échantillonné en `Heightmap.FromRelief`. Pentes des biomes en vrais % (seuils
  `ZoneMaskGrid.CliffMinSlope`… : falaise ≥ 8 %, fertile < 10 %, marais < 1 %, désert et plage < 2 % ; 12 novembre 2026).
  Le canevas garde la carte dessinée en image (`MapCanvasControl.DrawMapFromCache`) : toute modification de la carte
  ou du relief doit passer par `SetMap` (ou `NotifyReliefChanged`), sinon l'affichage ne suit pas. Les pinceaux de
  biomes et de gisements recalculent les contours au relâcher seulement.
- Routes (décidé et codé le 10 novembre 2026, `src/Demos.Domain/Map/Roads.cs`) : le trafic entre communes crée sentier, chemin,
  route (un niveau par mois) ; tracé le moins cher sur la carte d'altitude (`game/data/relief-*.png`, lue par
  `JsonContentSource.LoadRelief`) ; un chemin retire 25 % de la perte de transport, une route 50 % ; construction et
  entretien payés par la plus petite structure qui contient les deux communes (préfecture : l'État paie pour l'instant).
  Chiffres : réglages `road…` de `BalanceSettings` (validés le 11 novembre 2026). Routes des bâtiments (décidé le
  12 novembre 2026) : ce que produit un bâtiment posé sur la carte fait du trafic avec sa commune
  (`Simulation.AddBuildingTraffic`), mêmes seuils, payé par la commune, **sans perte de transport** ; `GameView.TradeFlows`
  ne garde que les paires de communes.
- Fonder une commune (décidé le 12 novembre 2026, `Map/CommuneSites.cs`, `World.CommuneSiteProblem`, contrôlé par
  `TryFoundCommune`) : sur la carte, pas sur l'eau (altitude sous 0), une falaise ou un glacier, et à 16 km au moins de
  toute autre commune (`communeMinDistance`) ; pas de distance maximale. Légitimité du camp : `baseCampLegitimacyCost` (20).
- Chantiers suivants ouverts :
  - Bonus logistique de transport fluvial le long des 17 cours d'eau navigables.

## Interface Godot : état actuel (résumé)

- Menus dans `game/scripts/MainMenu.cs` (Carte, Lois, Structure, Population, Communes, Recherche, Ressources, Aide) ; une page
  par fichier (`*Page.cs`) ; `Coming(...)` = entrée grisée « à venir ». Lecture de la partie par `GameView`
  (`src/Demos.Application/GameView.cs`), **actions du joueur par `PlayerDesk`** (`game/scripts/PlayerDesk.cs`, qui
  appelle `Simulation`) ; textes de partie dans `WorldText.cs`, couleurs et styles dans `UiTheme.cs`.
- Commission des lois branchée (12 novembre 2026) : actions dans `PlayerDesk.Laws.cs` (`PlayerDesk` est une classe
  partielle) ; `LawCommissionPage` crée un brouillon depuis un modèle (niveau au choix) ou une loi vierge (éditeur
  libre, décidé le 12 novembre 2026 : `Simulation.DraftBlank`, nom, domaine, niveau), renomme, ajoute ou retire des
  briques (3 au plus, du domaine), choisit leur sujet, règle curseurs et cases « verrouillée », vote, supprime, annule
  un vote ; `CivilCodePage` a « Amender » et « Abroger ». Bulles d'aide des fiches de lois (hérité, bornes, écart,
  verrouillée, pôles, coût…) : textes dans `LawHints.cs`, balise `[hint]` via `SheetText.Hint`.
- Impôts (décidé le 12 novembre 2026) : ils ne se changent que par une loi (domaine Impôts). `SetStateTaxRate` et
  `SetCommuneTaxRate` ne sont volontairement pas branchés ; `RevenuePage` reste en lecture, avec un raccourci
  « Préparer une loi d'impôt » (`PlayerDesk.DraftTaxLaw` : brouillon au niveau de l'entité, brique impôt à 0 %).
- Routes : après chaque bilan (`GameView.RoadChanges`, liste remplacée à chaque bilan), `Main` met les changements dans
  la barre du bas (textes dans `RoadText.cs`) ; Communes → Transport (`TransportPage`) liste les liaisons, le dernier
  bilan et la règle.
- Lois entrées en vigueur : au 1er du mois, `Main` écoute `LawsEnacted` : pause et fenêtre `LawsEnactedDialog` (sans
  « qui accepte, qui rejette » : pas de règle).
- Menu Structure (13 novembre 2026) : « Gestion des entités politiques » (`StructurePage` : arbre, fiche, et
  « Rattacher » sous la fiche d'une commune ou d'une préfecture, `StructurePage.Attach.cs`), « Création d'entité
  politique » (`EntityCreationPage` : Commune ou Préfecture ; remplace `PrefecturesPage` et l'ancienne fenêtre de
  fondation), « Organisme » (à venir).
- Fonder une commune : Structure → Création d'entité politique, formulaire Commune (nom, commune qui paie, charte, coût) ; la légitimité se paie toujours en PO
  (`Simulation.FoundingPole`, `FoundingLegitimacyCostOf` : coût de base × `LawBook.FoundingCostFactor`, brique « Défrichement »), la
  charte se choisit à part ; il faut une loi d'extension (brique « Extension », `LawBook.AllowsExtension`, aussi pour poser un
  bâtiment d'extraction au-delà du ressort de la commune, `World.ExtractionSiteProblem`) — 23 novembre 2026 ; voter une loi peut se
  faire à crédit (`LegitimacyPool.TrySpendOnCredit`, plafond `legitimacyMaxDebt` 100, constante DetteLegitimite) : un pôle en
  dette ne paie rien d'autre (`TrySpend` refuse) ; la légitimité produite par les lois acceptées est un réglage (`legitimacyPerMemberPerDay`
  0,0001, constante LegitimiteParMembre), séparé du taux du coût des lois (`lawCostPerMemberPerDay` 0,001, LoiCoutParMembre) ; la légitimité
  s'use (`LegitimacyPool.Erode`, chaque mois, `legitimacyYearlyErosion` 10 %) ; les coûts fixes suivent la nation
  (`Simulation.NationCostScale` : fonder, rattacher), les habitants regroupés (`PrefectureLegitimacyCostOf`) ou les emplois
  (`BuildingLegitimacyCost` : vendre, nationaliser) ;
  la touche C et le bouton « Camp de base » y mènent (`MainMenu.ShowCommuneCreation`). Puis la carte passe en mode pose
  (`MapCanvas.PlacementRequest`, sans zones, avec `ExclusionRadius` : cercles rouges autour des communes) ;
  `PlayerDesk.FoundCommune` (`PlayerDesk.Communes.cs`) fonde et explique un refus.
- Événements (13 novembre 2026) : `Main` écoute `BuildingCompleted`, `CompaniesNationalizedByLaw`, `SeasonPassed`,
  `YearPassed` (bilan : trésor et habitants depuis le 1er janvier) et le bilan des routes ; les messages d'une même image
  vont ensemble dans la barre du bas (textes dans `EventText.cs` ; le message entier au survol). La charte de fondation
  (`GameView.CharterOf`) s'affiche dans la fiche d'une commune, avec son effet sur les migrations en bulle d'aide.
- Menu Population (refait le 21 novembre 2026), en lecture, pour toute la nation ou une commune : « Besoins »
  (`PopulationNeedsPage` : tableau en couleurs besoin × rang, clic → causes du manque en barre, groupes, croissance et
  pôles poussés), « Groupes et attentes » (`PopulationGroupsPage` : carte `GroupMap`, une bulle par classe sur deux axes
  au choix, le troisième en couleur ; fiche : pôles voulus, adhésion, lois rejetées et acceptées, pressions comparées
  aux seuils de `ClassTransitions`, besoins, puis commune par commune), « Évolution » (`PopulationHistoryPage` : courbes
  de la satisfaction de chaque besoin, des membres par rang, de l'adhésion par pôle, du chômage par rang, de la richesse).
  Relevés sans effet sur la partie : `SocialClass.NeedDetails` (`NeedRecord`, par `Consumption`) et `LawStances`
  (`LawStance`, par `LawEffects`), lus par `SocialClassView.NeedDetails` / `LawStances` (`LawStanceView`).
  L'ancienne vue d'ensemble est devenue Communes → Habitants (`PopulationOverviewPage(view, Focus)`).
- Légitimité (23 novembre 2026) : Population → « Légitimité » (`LegitimacyPage` : par pôle stock, gain/jour, gagné,
  dépensé et solde sur 30 jours ; gain par loi et par pôle ; dépenses récentes ; courbe du stock, `LegitimacyHistory`
  relevé par `Main` chaque jour, un point par jour sur 30 jours puis un par mois) et Communes → « Légitimité »
  (`CommuneLegitimacyPage` : produit de la commune et sa part du pays, par pôle, par groupe, par loi avec
  `GameView.LawEfficiencyIn`). Relevé sans effet sur la partie : `LegitimacyLedger` (dans `LegitimacyPool.Ledger`,
  30 jours) note les gains (`LegitimacyPool.Earn`, appelé par `LawEffects`, jour ouvert par `Simulation` avec
  `BeginDay`) et les dépenses avec leur motif (`LegitimacySpendingKind`, paramètre des `TrySpend`/`TrySpendOnCredit`,
  et `Add` pour un vote annulé) ; lu par `GameView.LegitimacyLedger()` (`GameView.Legitimacy.cs`). Textes et bulles
  dans `LegitimacyText.cs`. `LineChart` descend sous 0 quand une valeur est négative (pôle en dette).
  Chiffres agrégés dans `PopulationStats.cs` (`NeedCells`, `UnemployedByRank`, `AdhesionByPole`, `NeedsByRank`), bulles d'aide (règles et constantes du
  moteur) dans `PopulationText.cs`. Graphiques dessinés par Godot, réutilisables : `LineChart` (courbes, axe serré
  autour des relevés sauf pour une part, survol) et `StackedBar` (barre en parts) ; couleurs `UiTheme.SeriesColor`.
  La simulation ne garde pas d'historique : le relevé (`PopulationHistory`, `MainMenu.History`) est pris par `Main`
  chaque jour joué, tassé à un relevé par mois au-delà d'un an (décidé le 21 novembre 2026 ; de même pour `ProductionHistory`, dont `MonthStarts` sert au tableau de Statistiques et à `FlowText`), et ne dure que la partie. `SocialClassView` porte aussi les pôles, le nom de l'idéologie, les chômeurs, les revenus
  et manques de la saison, la richesse par membre et les lieux de travail (`WorkplaceView`).
- Communes → Statistiques (15 novembre 2026), en lecture (`ProductionStatsPage`) : pour la commune du menu (avant le
  20 novembre : toutes les communes ou une
  seule, courbe mois par mois du produit choisi (production, utilisé, envoyé, perdu en route, perdu au stockage, stock
  en fin de mois) et tableau de tous les produits pour le dernier mois fini. Compteurs sans effet sur la partie :
  `Market.TallyOf` (utilisé = `TryBuy` + `TryWithdraw`, envoyé = `Take`, perdu au stockage = `Keep`, perdu en route =
  `CountLostInTransit`, appelé par `Trade` et `SupplyLines` dans la commune de départ, 20 novembre 2026 ; les stocks
  hors entrepôt — réserves des lignes, combustible, réserve de chantier, produits des habitants, `World.StocksOutsideStorage` —
  se périment en fin de journée par `Storage.SpoilOutsideStorage`, comptés par `Market.CountLost`) et
  `World.CountProduced` (appelé par
  `Simulation.CountProduction` après la journée de travail) ; lecture par `GameView.ProductFlows` (totaux depuis le
  début). Relevé quotidien par l'interface (`ProductionHistory`, `MainMenu.ProductionHistory`), pris avec celui de la
  population ; un mois = différence entre deux relevés.
- Communes → Chaîne de production (15 novembre 2026, `ProductionChainPage`) : schéma d'une commune (`ChainDiagram`,
  dessiné par Godot, en bandes depuis le 19 novembre 2026 : une bande par besoin nourri — Se nourrir, Se distraire,
  S'habiller, Se meubler, puis Chantiers, Énergie, Services, Autres —, un bâtiment dans la première qu'il nourrit, sans
  doublon ; dans chaque bande, colonnes selon la profondeur, ce qui va droit au besoin dans la dernière ; produit
  écrit sur les flèches ; bande « Sans recette » en bas, lue par `GameView.BuildingsWithoutRecipe` ; cadre orange
  quand les matières ont manqué, « Suivre » un produit estompe le reste) ;
  en lecture seule depuis le 19 novembre 2026 : un clic sur un bâtiment ouvre Unités de production sur lui
  (`MainMenu.ShowBuilding` → `BuildingsPage.Show`) ; cadre extérieur bleu ou violet pour un bâtiment relié par une ligne
  à une autre commune. Lecture : `GameView.ProductionChain(communeId)`. La part que les
  matières ont permise est retenue par `WorkDay.Run(…, supplyShares)` dans `World.SupplyShares` (sans effet sur la
  partie).
- Lignes d'approvisionnement (règle décidée le 15 novembre 2026, dans le document des règles, Économie →
  Approvisionnement) : `SupplyLines` (domaine) — `ProblemWith` (deux bâtiments publics, le client utilise le produit,
  pas de doublon), `RunDaily` (dans la même commune après chaque étape de la chaîne, servi le jour même ; entre
  communes après la journée de travail, servi le lendemain : `Simulation.SplitSupplyLines`, 20 novembre 2026 ; part de la production du jour, 50 % par défaut, plafonnée
  au besoin du client pour un jour moins sa réserve ; payée au prix du marché entre communes, gratuite dans la même ;
  perte de transport et trafic de routes entre communes ; lignes dans l'ordre de la liste, la plus ancienne d'abord sauf
  priorité changée par `World.MoveSupplyLine`). La réserve du client
  (`World.ReserveOf`) passe en premier dans `Supply.Acquire`. `Market.TryDeliver` cède les produits d'un vendeur.
  Actions : `Simulation.TryAddSupplyLine` / `RemoveSupplyLine` / `SetSupplyLineShare` (5 % à 100 %), par
  `PlayerDesk.Supply.cs`. Lecture : `GameView.SupplyLines`, `SupplierCandidates`. Interface : Unités de production
  (`SupplyLinesPanel`), flèches blanches épaisses sur le schéma. Ligne bloquée faute de bâtiment logistique (20 novembre
  2026, maquette 2) : `SupplyLineView.MissingLogistics` / `SupplierCandidateView.MissingLogistics` (`GameView.CanTrade`) ;
  avertissement orange au-dessus des lignes, mention « ne livre pas : aucun bâtiment logistique » et bulle sur la ligne,
  « sans bâtiment logistique » dans le menu des fournisseurs. Sans ligne, rien ne change
  (référence 3 ans : 251 177).
- Noms des bâtiments (décidé le 19 novembre 2026) : calculés, jamais écrits à la main. `World.DisplayNameOf` : type,
  puis le produit de la recette en cours entre parenthèses (« Atelier (Farine) ») ; sans produit, le type seul ;
  doublon dans la même commune : numéro à partir du deuxième (« Atelier (Farine) 2 »). Communes, préfectures et
  organismes gardent leur nom. `GameView.NameOf` et le schéma Structure l'utilisent, `PlayerDesk` aussi ; plus de
  `name` pour les bâtiments dans `partie-depart.json`. Le carré du propriétaire : `SheetText.OwnerSquare` /
  `SheetText.Structure` dans les fiches (BBCode), icônes dans les arbres et menus, rien dans la barre de messages.
- Flux d'un bâtiment (19 novembre 2026) : `GameView.BuildingFlows` — sources (ses lignes, puis les producteurs de ses
  matières dans la commune, ou « aucun producteur ») et destinations (ses lignes, les bâtiments de la commune qui
  utilisent son produit, les habitants pour un besoin, les autres communes par les échanges), chacune avec
  `PlaceRelation` (même commune, autre commune de la même préfecture, autre préfecture : `GameView.RelationBetween`).
  Textes partagés dans `FlowText.cs` (la quantité envoyée aux autres communes vient du relevé mensuel). Structure →
  fiche d'un bâtiment : sections « D'où viennent ses matières » et « Où part sa production ». Unités de production :
  petit schéma `FlowDiagram` (sources | bâtiment | destinations, cadre bleu ou violet hors de la commune, clic sur un
  autre bâtiment pour le choisir), puis recette, lignes d'approvisionnement (`SupplyLinesPanel`), postes, chauffage,
  vente, nationalisation. Couleurs : `UiTheme.RelationColor`, légende `UiTheme.RelationLegend`. Étape B
  (réglages d'achat et de vente) : voir le point suivant.
- Réglages d'achat et de vente des bâtiments publics (étape B, décidée le 19 novembre 2026, dans le document des
  règles, Économie) : `SupplySettings` (domaine) — `OnlyFromLines`, `RefusePrivate`, `RefuseCooperative`,
  `ReserveDays` (0 à 30), `NoExport` ; `PurchaseRulesFor` donne à `Supply.Acquire` (par `WorkDay`) les `PurchaseRules`
  (marché permis, vendeurs permis) ; `ApplySales` met chaque jour au marché la réserve de l'État (`Market.SetHeld` :
  ni achats ni échanges, mais l'État et ses lignes si) et le refus d'envoi (`Market.SetNotExported`), par commune et
  produit. Acheteurs : `Market.QuantityForSale` / `TryBuy(…, allowed)` ; sans réglage, chemin identique à avant
  (référence 3 ans : 251 177). Brique de loi `choix-fournisseurs` (« Choix des fournisseurs publics », Permission
  autorisée par défaut, sujet = la sorte de vendeur) → `AppliedLaws.MayRefusePrivate` / `MayRefuseCooperative`.
  Priorité des lignes : `World.MoveSupplyLine`. Actions : `Simulation.SetSupplySettings`, `MoveSupplyLine`, par
  `PlayerDesk.Supply.cs`. Interface : `PurchaseSalePanel` et ↑ ↓ dans `SupplyLinesPanel` (Unités de production) ;
  fournisseur écarté barré dans `FlowDiagram` (`FlowEndView.Blocked`). Lecture : `GameView.SupplySettingsOf`.
- Menu Communes (20 novembre 2026, ex-menu Production) : ses onglets (Unités de production, Construire, Transport,
  Chaîne de production, Statistiques, Habitants, Légitimité) montrent tous la commune choisie, affichée au-dessus d'eux avec ◀ ▶ pour passer
  à une autre (`CommuneFocus`, `MainMenu.Focus`, gardée d'un onglet à l'autre et quand le menu se ferme). Plus de menu
  déroulant de commune dans ces pages : Unités de production cache le menu « Entité » (`StructurePicker.FixEntity`),
  Transport ne montre que les liaisons de la commune, Statistiques n'a plus « Toutes les communes ». Un clic sur une
  commune de la carte ouvre ce menu sur elle (`MainMenu.ShowClicked` ; une préfecture ouvre toujours sa fiche dans
  Structure) ; la carte se centre sur la commune à chaque changement et à l'ouverture du menu
  (`MapCanvas.CenterOnCommune`, à l'image suivante pour tenir compte de l'élargissement du menu).
- Pas branché, volontairement : `SetPrice` (prix fixés par le joueur : question ouverte des prix dynamiques).
  Prompt de la session d'intégration : `prompt-integration-interface.md`, à côté du dépôt.
