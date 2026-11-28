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
| Simulation du départ d'exil | `dotnet run --project tools/Demos.Equilibrage -- exil [années]` (console, un relevé par mois) |
| Éditeur de contenu | `editeur.bat` (fenêtre Windows, `tools/Demos.Editeur`) |
| Éditeur de carte | `carte.bat` (fenêtre Windows, `tools/Demos.Carte`) |
| Gestionnaire d'icônes | `icones.bat` (fenêtre application 74 logos, `galerie-icones.html` ; `remplacer.bat` pour remplacement direct) |
| Test auto de la carte | `dotnet run --project tools/Demos.Carte -- --test-sliders` |
| Mesure de l'éditeur par taille de carte | `dotnet run --project tools/Demos.Carte -- --mesurer-tailles [dossier]` (temps de chaque étape ; avec un dossier, y enregistre l'île du petit continent pour l'essayer dans le jeu) |
| Relief d'une carte (images) | `dotnet run --project tools/Demos.Carte -- --relief [carte]` (la carte par défaut sans nom ; l'éditeur le refait à chaque enregistrement) |
| Petites images de la carte | `dotnet run --project tools/Demos.Carte -- --images` (dessine celles qui manquent dans `game/assets/carte/` ; `--forcer` les redessine toutes) |

Le tableur (`equilibrage demos.xlsx`) et le rapport (`rapport simulation.html`) sont **à côté** du dossier `demos/`,
hors du dépôt. Relevé le 28 novembre 2026 : trésor **984** la 3e année (88 904 la 1re, 48 576 la 2e ; identique avant
et après le début de partie scénarisé, la baisse vient d'avant, sans doute du budget de l'État). Ancienne référence : trésor 254 865 la 3e année (`simuler`, 3 ans, départ préréglé, depuis la crise, l'école et le seuil de pollution à 0,3 du 23 novembre 2026 ; 255 091 depuis les moteurs de l'axe Propriété ; 252 286 depuis les
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
  `cartes/<nom>/` (une carte par dossier : `carte.json`, définition vectorielle du monde, `VectorMapDefinition`, et ses images de relief ; la carte par défaut est nommée dans `cartes/carte-par-defaut.txt`, aujourd'hui `origine` ; `essai-1` est une carte d'essai ; chemins dans `Demos.Infrastructure/MapFiles.cs`, `JsonContentSource(…, mapName)` lit une carte par son nom, la carte par défaut sinon, l'ancien `carte.json` à la racine en secours ; Godot : `MapData.LoadVectorMap(mapName)`, `MapRelief.Load(dossier)`), `promesses.json` (les 26 Promesses des exilés,
  une par combinaison de 1 à 3 pôles, et leurs noms : `Politics/PromiseDefinition.cs`, `LoadPromises`, feuille Promesses du
  tableur où le nom se change ; sans effet sur la partie pour l'instant). `engagements.json` : les 42 engagements de La Promesse
  (`Politics/EngagementDefinition.cs`, de principe ou concrets, par pôle ; `LoadEngagements`, feuille Engagements du tableur ;
  chaque engagement a ses `rules` : l'acte qui le rompt). La Promesse dans la partie : `World.PromiseState` (`PromiseState` :
  engagements, blessures, mois sous le seuil, `Torn`), `Simulation.Promise.cs` (`ChoosePromise`, `PromiseGauge`, trahisons
  notées à l'entrée en vigueur des lois et sur les actes, méfiance `SocialClass.Distrust`, `RunPromiseMonthly`, `Refound`),
  lecture `GameView.Promise()` ; réglages `Promise…` (feuille Passages). Début de partie et interface : voir « Thème,
  Promesse et fin de partie » et « Interface Godot ».
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

## Thème, Promesse et fin de partie : état actuel (résumé)

Décidé du 21 au 25 novembre 2026, détaillé dans le document des règles (section « Thème : l'exil et la promesse », et
« Fin de partie ») ; lire seulement le passage utile.
- Thème : l'exil, la promesse et la dérive (idées des *Dépossédés*, sans ses noms). Intention : responsabilité envers le
  peuple, pas de dilemmes perdant-perdant. Principes : le jeu pose les problèmes sans proposer de solution ; tout se
  répare, sauf la révolution.
- Deux modes : avec La Promesse (principal) et bac à sable. La Promesse vise 1 à 3 axes (26 combinaisons nommées,
  `promesses.json`) et un engagement par axe (de principe ou concret, 42 au catalogue, `engagements.json`). Un engagement
  porte sur un acte, jamais bloqué ; le rompre est une trahison.
- Deux jauges, deux fins : la tension révolutionnaire (le pouvoir, par paliers, toutes les classes ; décidée, **pas codée** :
  la révolution reste l'ancienne règle) et la jauge de la promesse (le peuple ; **codée** dans la simulation, voir
  Architecture). Pacte déchiré → terminer, bac à sable ou refonder (crise + cicatrice). Le texte signé s'efface avec la
  jauge (codé le 27 novembre 2026, `SignedText`). Une jauge **Autorité** de l'État (dérive autoritaire, pas un 4e axe) est décidée, pas codée.
- Début de partie (décidé et **codé** les 27 et 28 novembre 2026, maquettes : canevas « Démos — début de partie et
  Promesse », https://claude.ai/artifact/UmQbTMaPwytPTuY7DfLtfX) : on nomme son peuple, on choisit La Promesse et un
  engagement par axe, 3 lois de l'An I (acte fondateur, gratuites ; une loi qui romprait un engagement est refusée,
  `PrototypeScenario.FoundingLawBreaches`), on recopie la première et la dernière phrase du serment et on signe à la
  souris. Serment : `PromiseOath` (première phrase par Promesse, champ `opening` de `promesses.json`, colonne G de la
  feuille Promesses ; phrases de pôle dans `PromiseOath.PhraseOf` ; dernière phrase, décidée le 28 novembre 2026 :
  « Que ceci soit notre Promesse : que cette terre en soit témoin, et que nos enfants seuls nous jugent. »). Départ d'exil : `game/data/partie-exil.json`
  (1 000 exilés 900/90/10, au pôle de La Promesse sur ses axes, pôle du bac à sable ailleurs ; camp de base, puits, ferme
  300, moulin 40, boulangerie 40, bûcheron 100, grenier, abris 700 places ; trésor 5 000, richesse au cinquième, 5 jours de
  farine, 10 charrettes, outils et objets durables à moitié, chauffage au bois), `ScenarioDefinition.Provisions` /
  `Carts` / `ToolShare` / `PossessionShare`, `ExileStart`, `PrototypeScenario.Create(content, laws, exile)` (terre d'exil
  tirée au hasard parmi `AllExileSites`, la commune de base si la carte n'en a pas). Contrôle avant un acte :
  `Simulation.PromiseBreachesOf…` (brouillon, vente, nationalisation, fondation, construction), lu par
  `GameView.PromiseBreachesOf…` (`PromiseBreachView`). `PromiseState.PeopleName`, `FoundingLaws`, `Signature`, `Oath`.
  Simulation du départ : `dotnet run --project tools/Demos.Equilibrage -- exil [années]` (un relevé par mois, sans rien
  faire : 993 habitants au bout d'un an, la faim faute de pain monte de 13 % à 40 % car la boulangerie perd des ouvriers,
  la jauge glisse de 1 à 0,82). Terre d'exil = zone ingrate de la carte (prompt `prompt-terre-exil-carte.md`).
- Victoire : le Grand Œuvre, né d'une recherche philosophique au fil de trois actes (noir, blanc, rouge), choisi par le
  joueur (coût d'adhésion), achevé pacte intact ; épilogue selon la société (décidé, **pas codé**).
- Codé à côté : école qui enseigne La Promesse, crise solidarité/repli, axes indépendants (CO collectiviste), moteurs de
  l'axe Propriété, expansion par la loi (briques Extension, Défrichement), vote à crédit, usure de la légitimité, coûts
  fixes indexés. L'état d'exception attend la définition des crises (prompt `prompt-crises-evenements.md`).
- Prompts prêts, à côté du dépôt : `prompt-debut-de-partie.md` (UI et scénario du début de partie),
  `prompt-trois-actes.md` (les trois actes et le Grand Œuvre), `prompt-crises-evenements.md`, `prompt-terre-exil-carte.md`.

## Économie : état actuel (résumé)

Nouvelles communes (20 novembre 2026) : `Simulation.Build(structure, mode, position, size)` → `ConstructionOutcome` ; matériaux
selon `Simulation.PlanConstruction` (`ConstructionPlan` : réserve, stock de l'État, achat ici, puis la commune la plus proche qui
échange, avec perte, trajet ajouté au chantier, une journée de capacité au plus ; texte `GameView.DescribePlan`) ; taille 10 % à
100 % (`BuildingType.Sized`, `ConstructionQuantity`, réglages `buildingMinUnits/Materials`) ; zones limitées (`Map/ZoneCapacity`,
`World.ZoneUsage` / `ZoneProblem`, rayon `zoneRadius`, densités `zoneDensityFertile/Forest/Deposit`) ; les outils comptent dans
`Trade.BuildingDemand`. Interface : `ConstructionPage` (taille − +, origine des matériaux, zone vérifiée à la pose).
Familles de produits (21 novembre 2026, sans effet sur la partie) : `ProductDefinition.Family` (`ProductFamily`, champ
`family` de `products.json`, test `Chaque_produit_a_une_famille`) ; noms et rangement dans `game/scripts/ProductFamilies.cs` ;
Économie → Stocks (`StocksPage` : famille → produit avec son total → répartition par commune ; stockage des communes en
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

- Données produites dans `game/data/cartes/<nom>/carte.json` (`VectorMapDefinition` dans `src/Demos.Domain/Map/MapVectorData.cs`) :
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
    calques, un par fichier, dans `game/scripts/Map/Layers/` (lacs : `LakesLayer.cs`, eau à plat au-dessus du relief, 24 novembre 2026) (plan : `plan-refonte-carte.md`, à côté du dépôt).
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
  `prefectureJurisdictionRadius`). Maisons et arbres presque à l'échelle (les arbres sont devenus une canopée peinte le 24 novembre 2026) (tailles dans `MapPalette`). Barre
  d'échelle : `game/scripts/Map/MapCanvas.Scale.cs`.
- Tailles des cartes neuves (décidé le 24 novembre 2026, `tools/Demos.Carte/MapSizes.cs`) : département 100 × 60, petit pays
  240 × 150, grand pays 800 × 500, petit continent 1 600 × 1 000 km. Grille du relief : 360 × 230 cases au plus jusqu'à
  360 × 225 km (comme avant), au-delà cases d'1 km au moins et 800 × 500 au plus ; images de relief jamais plus grandes
  que celles de la carte d'origine (`ReliefSettings.For`, pixels de ~900 m pour un continent). Accélérations du même jour
  (`ContourExtractor`) : segments reliés par carrés d'1 unité au lieu de tout parcourir, courbes en une passe sur la
  grille ; forêts extraites seules (`ZoneMaskGrid.ExtractForests`). Mesures (`--mesurer-tailles`) : île du continent
  17,5 s → 1,2 s ; courbes après un coup de pinceau 2,4 s → 0,08 s (petit pays 0,5 s → 0,03 s). Jeu sur un continent :
  144 images/s, démarrage 14 s (4 s sur la carte d'origine), ~6,5 millions d'arbres (0,8 sur l'origine). Depuis la canopée (même
  jour, `game/scripts/Map/Layers/ForestsLayer.cs`, shader `shaders/carte/foret.gdshader` : couronnes ~160 m, bosquets ~800 m,
  massifs ~4 km, chaque échelle effacée sous 2 à 6 pixels, conifères au-dessus de 900 m ; plus d'arbres posés un par un dans
  `BiomesLayer`) : continent 10 s, origine 4,5 s, ~780 images/s sans plafond sur les deux. Étape 2 (même jour, `ForestTrees.cs`) : de près (zoom 2 et plus, fondu jusqu'à 3), les arbres un par un avec leur ombre,
  par tuiles de 20 × 20 km préparées à la première vue (2 à 7 ms chacune), 48 gardées au plus ; place tirée d'une grille
  commune à toute la carte (mêmes arbres en revenant). Mesure : 15 tuiles, 97 000 arbres, ~790 images/s. Corrigé en passant
  (`MapGeometry.ForestPieceTriangles`, `Subtract`) : une clairière entièrement dans une forêt était remplie comme la forêt
  (Godot rend le contour et le trou comme deux polygones) ; elle est maintenant retirée (aussi pour la teinte des biomes).
- Éditeur de carte : au chargement, il prend le relief de la carte (`relief-altitude.png` s'il est plus récent que
  `carte.json`, sinon `ReliefBuilder`), échantillonné en `Heightmap.FromRelief`. Pentes des biomes en vrais % (seuils
  `ZoneMaskGrid.CliffMinSlope`… : falaise ≥ 8 %, fertile < 10 %, marais < 1 %, désert et plage < 2 % ; 12 novembre 2026).
  Le canevas garde la carte dessinée en image (`MapCanvasControl.DrawMapFromCache`) : toute modification de la carte
  ou du relief doit passer par `SetMap` (ou `NotifyReliefChanged`), sinon l'affichage ne suit pas. Pinceau de relief
  (24 novembre 2026) : la côte n'est recalculée qu'au dessin complet suivant (`_waterDirty`), pas à chaque mouvement ;
  deux dessins complets sont espacés d'au moins la durée du dernier (`RedrawInterval`) ; les biomes sont gardés en image
  (`Rendering/LayerCache.cs`, refaits seulement si la liste des biomes ou la vue change) : ~250 ms → ~80 ms par dessin.
  Fleuves (24 novembre 2026, `Heightmap.Water.cs`) : le tracé (`SimulateSpontaneousRiver`) prend le chemin qui creuse le
  moins (décidé le 24 novembre 2026) : réseau d'écoulement par remplissage par priorité depuis les sorties (mer, lacs, bord ;
  `Drainage`, gardé tant que le relief et les lacs ne changent pas), chaque case liée à la voisine de plus forte pente sur
  la hauteur d'eau, à pente égale la mer puis un lac puis le bord ; tracé de case en case adouci (Chaikin) puis ondulé (`Meander` : écart 1 km sur le plat, divisé par 1 + pente % ÷ 3, une vague tous les ~17,5 km, propositions ; bouts fixes ; même source, mêmes ondulations), confluence à
  12 unités, arrêt dans un lac. Lacs (même date, `MapEditorForm.Lakes.cs`, outil « Lacs », `LakeData` dans `carte.json`,
  champ `lakes`) : clic dans une cuvette (`FindBasin` : seuil = hauteur à franchir pour rejoindre une vraie sortie), curseur
  de niveau ; au seuil, fleuve de sortie « Sortie du … » (`LakeData.Outlet`) ; bouton « Effacer les lacs » (`ClearAllLakes`, avec leurs sorties, annulable) ; contour par `LakeShape` ; dessin
  `Rendering/LakeRenderer.cs`. Au creusement (`DigRiverThalweg`, paramètre `lakes`), un fleuve qui finit dans un lac garde
  son lit au-dessus du niveau du lac, et la rive d'un lac n'est jamais creusée sous son niveau. Au creusement (`DigRiverThalweg`), le lit visé ne
  remonte jamais vers l'aval et les bosses sont entaillées, la vallée rejoint le terrain en gaussienne. Contrôles dans `--test-sliders` : fleuves d'îles qui ne remontent pas une fois creusés, fleuves de plaine retracés, lacs (`LakesHoldTheirWater`). Le lit creusé descend d'au moins 0,1 m par km (`RiverBedMinSlope`, proposition ; moins si la source est trop basse pour cela) ; une cuvette se vide par son col le plus bas (le creusement l'entaille) ; pour un lac, l'outil Lacs ; un fleuve effacé (la vallée reste) puis retracé suit son lit jusqu'à la mer. Les pinceaux de
  biomes et de gisements recalculent les contours au relâcher seulement. Contours des biomes et gisements
  (`ZoneMaskGrid.ExtractMarchingSquaresLoops`) : la grille est entourée d'un cadre vide, pour qu'une zone qui touche le
  bord de la carte se referme le long du bord (corrigé le 24 novembre 2026 : le contour restait ouvert et la zone
  s'inversait ; test dans `--test-sliders`). La pente au bord de la carte se mesure dans la carte (`ComputeSlope`, 24 novembre 2026 : hors de la carte l'altitude valait 0 m, d'où des falaises le long du bord). Plusieurs cartes (24 novembre 2026, `MapEditorForm.Maps.cs`) : « Ouvrir » (liste des cartes), « Enregistrer » (dans la carte ouverte ; une carte neuve demande un nom), « Enregistrer sous… », « ★ Par défaut » ; chaque enregistrement refait le relief de la carte (`ReliefExport.Run(dossier)`).
- Routes (décidé et codé le 10 novembre 2026, `src/Demos.Domain/Map/Roads.cs`) : le trafic entre communes crée sentier, chemin,
  route (un niveau par mois) ; tracé le moins cher sur la carte d'altitude, sans passer par la mer ni les lacs (`ReliefGrid.SetLakes`, `IsLake`, posés par `PrototypeScenario` ; 24 novembre 2026) (`game/data/cartes/<nom>/relief-*.png`, lue par
  `JsonContentSource.LoadRelief`) ; un chemin retire 25 % de la perte de transport, une route 50 % ; construction et
  entretien payés par la plus petite structure qui contient les deux communes (préfecture : l'État paie pour l'instant).
  Chiffres : réglages `road…` de `BalanceSettings` (validés le 11 novembre 2026). Routes des bâtiments (décidé le
  12 novembre 2026) : ce que produit un bâtiment posé sur la carte fait du trafic avec sa commune
  (`Simulation.AddBuildingTraffic`), mêmes seuils, payé par la commune, **sans perte de transport** ; `GameView.TradeFlows`
  ne garde que les paires de communes.
- Fonder une commune (décidé le 12 novembre 2026, `Map/CommuneSites.cs`, `World.CommuneSiteProblem`, contrôlé par
  `TryFoundCommune`) : sur la carte, pas sur l'eau (altitude sous 0) ni sur un lac (« sur un lac », 24 novembre 2026), une falaise ou un glacier, et à 16 km au moins de
  toute autre commune (`communeMinDistance`) ; pas de distance maximale. Légitimité du camp : `baseCampLegitimacyCost` (20).
- Départs d'une carte (décidé le 23 novembre 2026, règles dans `GEMINI.md`, « Terre d'exil ») : `VectorMapDefinition.ExileSites`
  (`ExileSite` : nom et camp, 1 à 3 par carte, champ `exileSites` facultatif de `carte.json`, `AllExileSites`) ; le départ
  riche du bac à sable est la commune de base (`DefaultCommune`). Fiche et verdict dans `Map/StartSites.cs` : `Report`
  (terre fertile et forêt à 8 et 20 km, emplois de ferme et de bûcheron sur la zone la plus proche du ressort, gisement le
  plus proche, côte, fleuve, altitude), `Judge` (`ExileVerdict` jouable, injouable, trop facile ; seuils `ExileThresholds`,
  propositions, pas encore dans le tableur), `PresetProblems` (le départ préréglé tient-il : chaque bâtiment posé comme
  au départ, zones limitées). Éditeur : outil « Départs » (`EditorTool.PlaceStart`, `MapCanvasControl.Starts.cs`,
  `MapEditorForm.Starts.cs` : panneau des fiches à droite, verdict en couleur sur la carte). Une fiche prend ~0,4 s
  (surtout `ExtractionSites.NearestSite`). Pas encore codé : carte au hasard complète (fleuves, gisements, départs
  proposés), tirage de la terre d'exil dans le jeu, passage par la terre vers les gisements.
- Chantiers suivants ouverts :
  - Bonus logistique de transport fluvial le long des 17 cours d'eau navigables.

## Interface Godot : état actuel (résumé)

- Écran « Nouvelle partie » (24 novembre 2026, `NewGameScreen.cs`) : avant la partie, `Main._Ready` le montre ; il liste les
  cartes de `res://data/cartes/` (nom lu en tête de `carte.json`, `MapFiles.NameFromStart`), la carte par défaut choisie
  d'avance et marquée ★ ; « Commencer » (ou double-clic) appelle `Main.StartGame(carte)`, qui crée la partie
  (`JsonContentSource(…, mapName)`) puis l'écran de jeu (`Hud.Init(…, mapName)` → `MapCanvas.Init(view, mapName)`).
  `-- --carte <nom>` en ligne de commande passe l'écran (essais, captures avec `--write-movie`). Depuis le 28 novembre
  2026, l'écran choisit aussi le mode : avec La Promesse (`PromiseWizard` : peuple, Promesse, engagements, lois de l'An I ;
  puis `OathScreen` : recopier, signer, avec `SignaturePad`) ou bac à sable (départ riche ou terre d'exil) ; il donne un
  `NewGameChoice` à `Main.StartGame`, qui montre l'arrivée en exil (`ArrivalText`). Essais : `-- --carte origine --exil
  eg-ec-co` (départ d'exil direct, premier engagement de chaque pôle), `--menu "La Promesse"` (ouvre ce menu),
  `--assistant` (passe l'écran du mode ; `--assistant promesse` va droit à l'écran de La Promesse).
- La Promesse en jeu (28 novembre 2026) : menu « La Promesse » (`PromisePage` : texte signé `SignedText.Of`, qui pâlit
  par paliers et dont chaque article a son encre ; jauge `GaugeBar` et seuil ; alignement par axe ; blessures ; nom du
  peuple). Avertissement avant un acte qui trahit (`PromiseWarning.Check`, jamais bloquant, « Revenir » en avant) :
  vote d'une loi (`LawCommissionPage`), vente et nationalisation (`BuildingsPage`), pose d'un bâtiment (`ConstructionPage`),
  fondation (`Main.ChooseCommuneSite`). Trahison : message dans la barre du bas (`PromiseBetrayed`). Pacte déchiré :
  `PactTornScreen` (terminer avec un bilan, continuer sans promesse, refonder : `PromiseWizard` sans lois puis
  `OathScreen`) ; actions dans `PlayerDesk.Promise.cs`. Le menu La Promesse n'a pas encore d'icône.
- Menus dans `game/scripts/MainMenu.cs` (Carte, Lois, Structure, Population, Communes, Recherche, Économie, Aide) ; une page
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
- Budget de l'État (25 novembre 2026) : menu Économie (ancien Ressources) → « Budget » (`BudgetPage`, textes `BudgetText.cs`),
  « Impôts » (`RevenuePage`, ancien Revenus), « Stocks ». Domaine `src/Demos.Domain/Economy/Budget.cs` : `BudgetItem`
  (Dotations CO, votées par habitant et par an, `BudgetItems.IsPerResident` ; Chantiers PO, Routes PO, Investissements CO), `BudgetPlan`, `StateBudget` (`World.Budget` : en vigueur,
  dépensé de l'année, brouillon, voté en attente) et `BudgetEnvelope` (`World.EnvelopeOf(item)`, un `Treasury` plafonné :
  `Treasury` n'est plus scellé, ses membres sont virtuels). Chantiers, camp de base et siège de préfecture paient par
  l'enveloppe Chantiers, fondation et rachat par Investissements, routes de l'État par Routes (`World.FundOf`). Vote,
  annulation, 1er du mois et dotations dans `Simulation.Budget.cs` (dotations versées par `PayGrantsIfDue` le 1er
  jour joué de chaque mois : un douzième du montant par habitant × les habitants de chaque commune) ; lecture `GameView.Budget()` (`GameView.Budget.cs`) ; actions
  `PlayerDesk.Budget.cs`. Fin du relais : `WorkDay.PayInMoney` et `Supply.PayerOf` font payer un bâtiment public
  d'une commune par sa caisse seule (hors commune, une préfecture : le trésor). Réglages `Budget*` (feuille Constantes).
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
