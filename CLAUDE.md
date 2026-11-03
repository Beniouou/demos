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

Le tableur (`equilibrage demos.xlsx`) et le rapport (`rapport simulation.html`) sont **à côté** du dossier `demos/`,
hors du dépôt. Référence actuelle de la simulation : trésor **217 647** la 3e année (`simuler`, 3 ans).

## Architecture et données

- `src/Demos.Domain` : règles, sans dépendance. `src/Demos.Application` : `Simulation` (journée, mois, saison, année),
  `World`, `PrototypeScenario`, ports. `src/Demos.Infrastructure` : `JsonContentSource`. `game/` : Godot.
  `tools/Demos.Equilibrage` : tableur, appliquer, simuler, création de contenu (`ContentCreator`).
  `tools/Demos.Editeur` : fenêtre de l'éditeur. `tests/Demos.Tests` : xUnit.
- Contenu dans `game/data/` : `products.json`, `buildings.json`, `besoins.json`, `equilibrage.json` (réglages de
  l'économie, `BalanceSettings`, lus via `Balance.Current`), `partie-depart.json` (partie de départ).
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
