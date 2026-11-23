# ==============================================================================
# Projet Demos — Gestionnaire des Logos & Icones
# Outil graphique autonome pour visualiser, telecharger et remplacer les logos.
# ==============================================================================

try {
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.IO.Compression.FileSystem

    [void][System.Windows.Forms.Application]::EnableVisualStyles()

    # Chemins
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
    $iconsDir = [System.IO.Path]::GetFullPath((Join-Path $scriptDir "..\..\game\assets\icons"))

    if (-not (Test-Path $iconsDir)) {
        $candidate = "c:\Users\garyv\Desktop\Projet Demos\demos\game\assets\icons"
        if (Test-Path $candidate) { $iconsDir = $candidate }
    }

    # Metadonnees des 74 icones
    $allMeta = [ordered]@{
        # --- MENUS (8) ---
        "menu-carte" = @{ title = "Carte"; cat = "Menus"; folder = "menus"; desc = "Rose des vents cartographique & compas. Vue vectorielle IGN Pastel, relief de Horn 315° NW, hydrographie et biomes." }
        "menu-lois" = @{ title = "Lois"; cat = "Menus"; folder = "menus"; desc = "Balance républicaine et parchemin scellé. Code civil, commission des lois, vote d'amendements et impôts." }
        "menu-structure" = @{ title = "Structure"; cat = "Menus"; folder = "menus"; desc = "Fronton de l'État et arborescence territoriale. Communes, préfectures, rattachements et bureaucratie." }
        "menu-population" = @{ title = "Population"; cat = "Menus"; folder = "menus"; desc = "Triptyque citoyen (ouvrier, cadre, intermédiaire). Vue d'ensemble, démographie, groupes sociaux et satisfaction." }
        "menu-production" = @{ title = "Production"; cat = "Menus"; folder = "menus"; desc = "Engrenage industriel et marteau de forge. Unités de production, construction de bâtiments, transport et logistique." }
        "menu-recherche" = @{ title = "Recherche"; cat = "Menus"; folder = "menus"; desc = "Cornue alchimique & atome d'innovation. Découvertes scientifiques, agronomes et industrielles par domaine." }
        "menu-ressources" = @{ title = "Ressources"; cat = "Menus"; folder = "menus"; desc = "Coffre au trésor & lingots d'or. Trésor national, stocks de denrées, recettes fiscales et réserves." }
        "menu-aide" = @{ title = "Aide"; cat = "Menus"; folder = "menus"; desc = "Encyclopédie républicaine & guide du gouverneur. Commandes (Espace, 1-4, C, Échap), règles et glossaire." }

        # --- CLASSES SOCIALES (9) ---
        "classe-ouvriers" = @{ title = "Ouvriers (Rang)"; cat = "Classes Sociales"; folder = "classes"; desc = "Marteau de forge et clé plate croisés. Travailleurs de production, extraction et chantiers." }
        "classe-intermediaires" = @{ title = "Professions intermédiaires (Rang)"; cat = "Classes Sociales"; folder = "classes"; desc = "Équerre de précision, compas et registre. Techniciens, contremaîtres, instituteurs et encadrement." }
        "classe-cadres" = @{ title = "Cadres & Dirigeants (Rang)"; cat = "Classes Sociales"; folder = "classes"; desc = "Plume d'oie calligraphique et sceau d'État. Direction, haute administration et décisionnaires." }
        "pole-eg-egalitariste" = @{ title = "Égalitariste (Pôle EG)"; cat = "Classes Sociales"; folder = "classes"; desc = "Balance parfaitement horizontale (Rouge #E5484D). Égalité stricte, droits universels et redistribution." }
        "pole-se-segregationniste" = @{ title = "Ségrégationniste (Pôle SE)"; cat = "Classes Sociales"; folder = "classes"; desc = "Pyramide à 3 étages stricts (Bleu #4C8DF6). Ordre hiérarchique et privilèges de rang." }
        "pole-po-positiviste" = @{ title = "Positiviste (Pôle PO)"; cat = "Classes Sociales"; folder = "classes"; desc = "Soleil de la science & dynamo (Orange #F76808). Foi dans l'industrie et maximisation de la production." }
        "pole-ec-ecologiste" = @{ title = "Écologiste (Pôle EC)"; cat = "Classes Sociales"; folder = "classes"; desc = "Feuille de chêne & semence germée (Vert #30A46C). Préservation des ressources et équilibre naturel." }
        "pole-co-collectiviste" = @{ title = "Collectiviste (Pôle CO)"; cat = "Classes Sociales"; folder = "classes"; desc = "Grenier commun (Violet #9A6CE0). Moyens de produire possédés et gérés par la collectivité." }
        "pole-li-liberaux" = @{ title = "Libéraux (Pôle LI)"; cat = "Classes Sociales"; folder = "classes"; desc = "Ailes de Mercure & deniers flottants (Or #F5C542). Liberté d'entreprendre, marché dérégulé et propriété privée." }

        # --- BÂTIMENTS (20) ---
        "cat-administration" = @{ title = "Administration (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Palais civique à colonnade néoclassique. Mairie, préfecture, palais d'État." }
        "cat-extraction" = @{ title = "Extraction (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Pioche et hache croisées sur colline. Mines, carrières, exploitations forestières et agricoles." }
        "cat-production" = @{ title = "Production (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Usine à toits en shed et haute cheminée. Ateliers, forges, manufactures et usines." }
        "cat-energie" = @{ title = "Énergie (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Éclair frappant une turbine dynamo. Centrales à charbon, gaz, solaire et éolien." }
        "cat-services" = @{ title = "Services (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Bouclier civique à croix rayonnante. Santé, écoles, hôpitaux, pompiers et police." }
        "cat-logistique" = @{ title = "Logistique (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Caisse de fret cerclée et roue ferrée. Marchés, entrepôts, gares, ports et routes." }
        "cat-habitat" = @{ title = "Habitat (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Immeuble haussmannien en pierre de taille. Logements du peuple et toits mansardés." }
        "cat-eau" = @{ title = "Eau (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Goutte d'eau pure et réservoir. Puits, châteaux d'eau et pompes souterraines." }
        "cat-culture" = @{ title = "Culture (Catégorie)"; cat = "Bâtiments"; folder = "batiments"; desc = "Lyre dorée et masques de théâtre. Théâtres, arts et divertissements civiques." }
        "bat-camp-de-base" = @{ title = "Camp de base"; cat = "Bâtiments"; folder = "batiments"; desc = "Tente de pionnier & fanion au vent. Bâtiment fondateur niveau 0 pour créer une commune." }
        "bat-mairie" = @{ title = "Mairie"; cat = "Bâtiments"; folder = "batiments"; desc = "Hôtel de ville à beffroi et horloge républicaine (niveau 1). Siège administratif communal." }
        "bat-prefecture" = @{ title = "Préfecture"; cat = "Bâtiments"; folder = "batiments"; desc = "Palais préfectoral à dôme et grille monumentale (niveau 2). Siège départemental." }
        "bat-ferme" = @{ title = "Ferme"; cat = "Bâtiments"; folder = "batiments"; desc = "Grange traditionnelle à colombages et épis de blé. Production de blé, lin et élevage." }
        "bat-camp-forestier" = @{ title = "Camp de bûcheron"; cat = "Bâtiments"; folder = "batiments"; desc = "Cabane en rondins sous les sapins et grumes coupées. Extraction de bois d'œuvre." }
        "bat-mine" = @{ title = "Mine"; cat = "Bâtiments"; folder = "batiments"; desc = "Chevalement d'extraction à molette et wagonnet de minerai. Extraction de fer et charbon." }
        "bat-carriere" = @{ title = "Carrière"; cat = "Bâtiments"; folder = "batiments"; desc = "Front de taille en gradins et blocs de pierre équarris. Pierre, sable, marbre et argile." }
        "bat-atelier" = @{ title = "Atelier"; cat = "Bâtiments"; folder = "batiments"; desc = "Établi de menuisier avec rabot et outils. Transformation de farine, vêtements, meubles et outils." }
        "bat-forge" = @{ title = "Forge"; cat = "Bâtiments"; folder = "batiments"; desc = "Foyer incandescent et enclume lourdement martelée. Métallurgie, lingots et poterie." }
        "bat-ecole" = @{ title = "École"; cat = "Bâtiments"; folder = "batiments"; desc = "École de village à campanile, tableau noir et abécédaire. Éducation de base des ouvriers." }
        "bat-theatre" = @{ title = "Théâtre"; cat = "Bâtiments"; folder = "batiments"; desc = "Scène à l'italienne, rideau carmin drapé et feux de rampe. Pièces de théâtre et spectacles." }

        # --- RESSOURCES NATURELLES & MATIÈRES (17) ---
        "res-ble" = @{ title = "Blé"; cat = "Ressources"; folder = "ressources"; desc = "Gerbe d'épis d'or nouée avec ruban républicain. Matière première agricole de base pour le pain et l'alcool." }
        "res-bois" = @{ title = "Bois"; cat = "Ressources"; folder = "ressources"; desc = "Rondins de bois coupés empilés avec écorce et cernes. Exploitation forestière pour planches, meubles et chauffage." }
        "res-pierre" = @{ title = "Pierre"; cat = "Ressources"; folder = "ressources"; desc = "Blocs de roche brute équarris de carrière. Matériau de construction pour les bâtiments." }
        "res-charbon" = @{ title = "Charbon"; cat = "Ressources"; folder = "ressources"; desc = "Houille noire minérale aux facettes brillantes. Combustible pour la métallurgie, les centrales et le chauffage." }
        "res-minerai-fer" = @{ title = "Minerai de fer"; cat = "Ressources"; folder = "ressources"; desc = "Roche ferrugineuse veinée de rouge et métal. Extraction minière pour la forge et les lingots de fer." }
        "res-minerai-cuivre" = @{ title = "Minerai de cuivre"; cat = "Ressources"; folder = "ressources"; desc = "Roche minérale aux reflets cuivrés et vert-de-gris malachite. Extraction pour les lingots de cuivre." }
        "res-or" = @{ title = "Minerai d'or"; cat = "Ressources"; folder = "ressources"; desc = "Pépites d'or pur étincelantes sur quartz. Ressource précieuse pour le trésor et l'orfèvrerie." }
        "res-sable" = @{ title = "Sable"; cat = "Ressources"; folder = "ressources"; desc = "Cône de sable fin siliceux. Extrait en carrière pour la fabrication du ciment et du verre." }
        "res-argile" = @{ title = "Argile"; cat = "Ressources"; folder = "ressources"; desc = "Motte de glaise malléable rouge terracotta. Extrait en carrière pour la poterie et les tuiles." }
        "res-marbre" = @{ title = "Marbre"; cat = "Ressources"; folder = "ressources"; desc = "Bloc de marbre blanc poli veiné de gris. Pierre noble pour sculptures et monuments civiques." }
        "res-lin" = @{ title = "Lin"; cat = "Ressources"; folder = "ressources"; desc = "Tiges et fleurs de lin azurées. Culture textile pour la filature de tissus et vêtements." }
        "res-carcasse" = @{ title = "Carcasse"; cat = "Ressources"; folder = "ressources"; desc = "Quartier d'élevage de boucherie avec os. Découpé à l'atelier en viande fraîche et cuir." }
        "res-eau" = @{ title = "Eau"; cat = "Ressources"; folder = "ressources"; desc = "Gouttelette d'eau douce limpide avec ondelettes. Ressource vitale pompée pour les habitants et l'industrie." }
        "res-petrole" = @{ title = "Pétrole"; cat = "Ressources"; folder = "ressources"; desc = "Baril d'or noir et goutte de naphte. Hydrocarbure extrait par tour de forage pour le raffinage." }
        "res-farine" = @{ title = "Farine"; cat = "Ressources"; folder = "ressources"; desc = "Sac de meunier en toile noué déversant de la farine blanche. Issue du blé moulu pour la boulangerie." }
        "res-cuir" = @{ title = "Cuir"; cat = "Ressources"; folder = "ressources"; desc = "Rouleau de peau tannée avec lanière. Issu de la carcasse pour vêtements robustes et bottes." }
        "res-tresor" = @{ title = "Trésor / Monnaie"; cat = "Ressources"; folder = "ressources"; desc = "Pile de pièces de monnaie d'or étincelantes. Caisse de l'État et des communes, collecte de l'impôt." }

        # --- OBJETS MANUFACTURÉS & BIENS (20) ---
        "obj-pain" = @{ title = "Pain"; cat = "Objets"; folder = "objets"; desc = "Miche de pain artisanale croustillante dorée. Aliment de base quotidien pour satisfaire le besoin Se nourrir." }
        "obj-viande" = @{ title = "Viande"; cat = "Objets"; folder = "objets"; desc = "Rôti ficelé préparé de boucherie. Aliment noble couvrant le besoin alimentaire des classes aisées." }
        "obj-vetements" = @{ title = "Vêtements"; cat = "Objets"; folder = "objets"; desc = "Costume républicain avec col et boutons d'or. Couvre le besoin essentiel S'habiller." }
        "obj-meubles" = @{ title = "Meubles"; cat = "Objets"; folder = "objets"; desc = "Commode en bois ouvragé à tiroirs et laiton. Couvre le besoin Se meubler." }
        "obj-poterie" = @{ title = "Poterie"; cat = "Objets"; folder = "objets"; desc = "Vase / amphore en terre cuite à anses. Vaisselle et récipients domestiques pour l'aménagement." }
        "obj-alcool" = @{ title = "Alcool"; cat = "Objets"; folder = "objets"; desc = "Bouteille de cru et verre à pied de vin. Produit de réjouissance couvrant le besoin Se distraire." }
        "obj-administration" = @{ title = "Services administratifs"; cat = "Objets"; folder = "objets"; desc = "Tampon officiel et parapheur d'actes d'État. Services produits par la mairie, préfecture et palais." }
        "obj-securite" = @{ title = "Services de sécurité"; cat = "Objets"; folder = "objets"; desc = "Écusson de maintien de l'ordre avec glaive républicain. Produits par la police et les pompiers." }
        "obj-energie" = @{ title = "Énergie"; cat = "Objets"; folder = "objets"; desc = "Éclair électrique et champ d'induction. Électricité et force motrice pour les ateliers et l'éclairage." }
        "obj-tissus" = @{ title = "Tissus"; cat = "Objets"; folder = "objets"; desc = "Rouleau de drap tissé avec navette. Demi-produit textile transformé en vêtements." }
        "obj-planche" = @{ title = "Planche"; cat = "Objets"; folder = "objets"; desc = "Planches de bois de sciage équarries. Matériau intermédiaire pour meubles et construction." }
        "obj-lingot-fer" = @{ title = "Lingot de fer"; cat = "Objets"; folder = "objets"; desc = "Barre de fonte rectangulaire estampillée 'FE'. Produit de la forge pour outils, plaques et armatures." }
        "obj-lingot-cuivre" = @{ title = "Lingot de cuivre"; cat = "Objets"; folder = "objets"; desc = "Barre de cuivre raffinée 'CU'. Alliages, tuyauteries et câblages industriels." }
        "obj-plaque-de-fer" = @{ title = "Plaque de fer"; cat = "Objets"; folder = "objets"; desc = "Tôle d'acier laminée aux coins rivetés. Composant essentiel pour matériaux techniques et rails." }
        "obj-outils" = @{ title = "Outils"; cat = "Objets"; folder = "objets"; desc = "Marteau et scie de menuisier/forgeron croisés. Biens d'équipement augmentant la productivité." }
        "obj-sculpture" = @{ title = "Sculpture"; cat = "Objets"; folder = "objets"; desc = "Buste de marbre classique sur piédouche. Objet d'art et prestige civique." }
        "obj-piece-de-theatre" = @{ title = "Pièce de théâtre"; cat = "Objets"; folder = "objets"; desc = "Masque de scène doré et rouleau de manuscrit. Bien culturel vivant produit par le théâtre." }
        "obj-ciment" = @{ title = "Ciment"; cat = "Objets"; folder = "objets"; desc = "Sac de liant hydraulique et truelle de maçon. Ingrédient des matériaux de construction technique." }
        "obj-materiaux-de-construction" = @{ title = "Matériaux de construction"; cat = "Objets"; folder = "objets"; desc = "Pile de briques de terre cuite et madrier. Bien indispensable pour ériger tout bâtiment de niveau 1." }
        "obj-materiaux-de-construction-technique" = @{ title = "Matériaux techniques"; cat = "Objets"; folder = "objets"; desc = "Poutrelle d'acier en I rivetée avec équerre. Bien avancé pour bâtiments de niveau 2 et 3." }
    }

    # Couleurs du theme Projet Demos
    $cBg = [System.Drawing.Color]::FromArgb(20, 23, 28)        # #14171c
    $cSurface = [System.Drawing.Color]::FromArgb(29, 33, 40)   # #1d2128
    $cRaised = [System.Drawing.Color]::FromArgb(38, 43, 52)    # #262b34
    $cBorder = [System.Drawing.Color]::FromArgb(54, 61, 74)    # #363d4a
    $cGold = [System.Drawing.Color]::FromArgb(217, 164, 65)     # #d9a441
    $cText = [System.Drawing.Color]::FromArgb(230, 232, 236)    # #e6e8ec
    $cMuted = [System.Drawing.Color]::FromArgb(154, 161, 173)  # #9aa1ad
    $cGreen = [System.Drawing.Color]::FromArgb(48, 164, 108)   # #30a46c

    # Fenetre principale
    $form = New-Object System.Windows.Forms.Form
    $form.Text = "Projet Démos — Gestionnaire des Logos & Icônes (74 logos)"
    $form.Size = New-Object System.Drawing.Size(1120, 760)
    $form.MinimumSize = New-Object System.Drawing.Size(950, 620)
    $form.BackColor = $cBg
    $form.ForeColor = $cText
    $form.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen

    # Barre superieure (En-tete)
    $topPanel = New-Object System.Windows.Forms.Panel
    $topPanel.Dock = [System.Windows.Forms.DockStyle]::Top
    $topPanel.Height = 70
    $topPanel.BackColor = $cSurface
    $topPanel.Padding = New-Object System.Windows.Forms.Padding(16, 10, 16, 10)

    $lblMainTitle = New-Object System.Windows.Forms.Label
    $lblMainTitle.Text = "🏛️  PROJET DÉMOS — GESTIONNAIRE DES LOGOS & ICÔNES"
    $lblMainTitle.Font = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
    $lblMainTitle.ForeColor = $cGold
    $lblMainTitle.AutoSize = $true
    $lblMainTitle.Location = New-Object System.Drawing.Point(16, 12)

    $lblMainSub = New-Object System.Windows.Forms.Label
    $lblMainSub.Text = "74 insignes vectoriels officiels • Visualisez, téléchargez et remplacez vos logos en 1 clic"
    $lblMainSub.Font = New-Object System.Drawing.Font("Segoe UI", 8.5)
    $lblMainSub.ForeColor = $cMuted
    $lblMainSub.AutoSize = $true
    $lblMainSub.Location = New-Object System.Drawing.Point(18, 38)

    # Boutons actions globales en haut a droite
    $btnExportAll = New-Object System.Windows.Forms.Button
    $btnExportAll.Text = "📦 Exporter tout en ZIP..."
    $btnExportAll.Size = New-Object System.Drawing.Size(180, 36)
    $btnExportAll.Anchor = [System.Windows.Forms.AnchorStyles]::Top -bor [System.Windows.Forms.AnchorStyles]::Right
    $btnExportAll.Location = New-Object System.Drawing.Point(710, 16)
    $btnExportAll.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $btnExportAll.FlatAppearance.BorderColor = $cBorder
    $btnExportAll.BackColor = $cRaised
    $btnExportAll.ForeColor = $cText
    $btnExportAll.Cursor = [System.Windows.Forms.Cursors]::Hand

    $btnOpenDir = New-Object System.Windows.Forms.Button
    $btnOpenDir.Text = "📁 Ouvrir le dossier"
    $btnOpenDir.Size = New-Object System.Drawing.Size(160, 36)
    $btnOpenDir.Anchor = [System.Windows.Forms.AnchorStyles]::Top -bor [System.Windows.Forms.AnchorStyles]::Right
    $btnOpenDir.Location = New-Object System.Drawing.Point(905, 16)
    $btnOpenDir.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $btnOpenDir.FlatAppearance.BorderColor = $cGold
    $btnOpenDir.BackColor = $cRaised
    $btnOpenDir.ForeColor = $cGold
    $btnOpenDir.Cursor = [System.Windows.Forms.Cursors]::Hand

    $topPanel.Controls.AddRange(@($lblMainTitle, $lblMainSub, $btnExportAll, $btnOpenDir))

    # Barre de statut inferieure
    $statusStrip = New-Object System.Windows.Forms.StatusStrip
    $statusStrip.BackColor = $cSurface
    $statusStrip.ForeColor = $cMuted
    $statusLabel = New-Object System.Windows.Forms.ToolStripStatusLabel
    $statusLabel.Text = "Prêt • 74 icônes chargées. Glissez-déposez un fichier SVG ou PNG sur l'aperçu pour remplacer."
    $statusLabel.Spring = $true
    $statusLabel.TextAlign = [System.Drawing.ContentAlignment]::MiddleLeft
    [void]$statusStrip.Items.Add($statusLabel)

    # Conteneur principal (2 colonnes)
    $mainPanel = New-Object System.Windows.Forms.Panel
    $mainPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
    $mainPanel.Padding = New-Object System.Windows.Forms.Padding(12)

    # Colonne Gauche : Liste & Filtres
    $leftPanel = New-Object System.Windows.Forms.Panel
    $leftPanel.Dock = [System.Windows.Forms.DockStyle]::Left
    $leftPanel.Width = 380
    $leftPanel.BackColor = $cSurface
    $leftPanel.Padding = New-Object System.Windows.Forms.Padding(10)

    # Barre de recherche
    $txtSearch = New-Object System.Windows.Forms.TextBox
    $txtSearch.Dock = [System.Windows.Forms.DockStyle]::Top
    $txtSearch.Height = 30
    $txtSearch.BackColor = $cRaised
    $txtSearch.ForeColor = $cText
    $txtSearch.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $txtSearch.Font = New-Object System.Drawing.Font("Segoe UI", 10)

    $lblSearchTitle = New-Object System.Windows.Forms.Label
    $lblSearchTitle.Text = "Rechercher par nom ou ressource :"
    $lblSearchTitle.Dock = [System.Windows.Forms.DockStyle]::Top
    $lblSearchTitle.Height = 22
    $lblSearchTitle.ForeColor = $cMuted
    $lblSearchTitle.Font = New-Object System.Drawing.Font("Segoe UI", 8.5)

    # Filtre par categorie
    $cboCategory = New-Object System.Windows.Forms.ComboBox
    $cboCategory.Dock = [System.Windows.Forms.DockStyle]::Top
    $cboCategory.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
    $cboCategory.BackColor = $cRaised
    $cboCategory.ForeColor = $cText
    $cboCategory.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $cboCategory.Items.AddRange(@("Toutes les catégories (74)", "Ressources (17)", "Objets & Produits (20)", "Bâtiments (20)", "Classes Sociales (9)", "Menus (8)"))
    $cboCategory.SelectedIndex = 0

    $lblCatTitle = New-Object System.Windows.Forms.Label
    $lblCatTitle.Text = "Filtrer par catégorie :"
    $lblCatTitle.Dock = [System.Windows.Forms.DockStyle]::Top
    $lblCatTitle.Height = 22
    $lblCatTitle.ForeColor = $cMuted
    $lblCatTitle.Font = New-Object System.Drawing.Font("Segoe UI", 8.5)

    # Liste des icones
    $lstIcons = New-Object System.Windows.Forms.ListBox
    $lstIcons.Dock = [System.Windows.Forms.DockStyle]::Fill
    $lstIcons.BackColor = $cRaised
    $lstIcons.ForeColor = $cText
    $lstIcons.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $lstIcons.Font = New-Object System.Drawing.Font("Segoe UI", 10)
    $lstIcons.IntegralHeight = $false

    # Assemblage colonne gauche
    $spacer1 = New-Object System.Windows.Forms.Panel; $spacer1.Dock = [System.Windows.Forms.DockStyle]::Top; $spacer1.Height = 8
    $spacer2 = New-Object System.Windows.Forms.Panel; $spacer2.Dock = [System.Windows.Forms.DockStyle]::Top; $spacer2.Height = 10

    $leftPanel.Controls.Add($lstIcons)
    $leftPanel.Controls.Add($spacer2)
    $leftPanel.Controls.Add($cboCategory)
    $leftPanel.Controls.Add($lblCatTitle)
    $leftPanel.Controls.Add($spacer1)
    $leftPanel.Controls.Add($txtSearch)
    $leftPanel.Controls.Add($lblSearchTitle)

    # Separateur central
    $splitter = New-Object System.Windows.Forms.Splitter
    $splitter.Dock = [System.Windows.Forms.DockStyle]::Left
    $splitter.Width = 10
    $splitter.BackColor = $cBg

    # Colonne Droite : Fiche & Actions
    $rightPanel = New-Object System.Windows.Forms.Panel
    $rightPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
    $rightPanel.BackColor = $cSurface
    $rightPanel.Padding = New-Object System.Windows.Forms.Padding(20)

    # Titre & Categorie de l'icone choisie
    $lblCardCategory = New-Object System.Windows.Forms.Label
    $lblCardCategory.Text = "CATÉGORIE"
    $lblCardCategory.ForeColor = $cGold
    $lblCardCategory.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
    $lblCardCategory.AutoSize = $true
    $lblCardCategory.Location = New-Object System.Drawing.Point(20, 18)

    $lblCardTitle = New-Object System.Windows.Forms.Label
    $lblCardTitle.Text = "Sélectionnez une icône..."
    $lblCardTitle.ForeColor = $cText
    $lblCardTitle.Font = New-Object System.Drawing.Font("Segoe UI", 15, [System.Drawing.FontStyle]::Bold)
    $lblCardTitle.AutoSize = $true
    $lblCardTitle.Location = New-Object System.Drawing.Point(20, 38)

    $lblCardId = New-Object System.Windows.Forms.Label
    $lblCardId.Text = ""
    $lblCardId.ForeColor = $cMuted
    $lblCardId.Font = New-Object System.Drawing.Font("Consolas", 9)
    $lblCardId.AutoSize = $true
    $lblCardId.Location = New-Object System.Drawing.Point(22, 68)

    # Cadre de previsualisation (WebBrowser pour rendu SVG vectoriel net)
    $pnlPreviewBox = New-Object System.Windows.Forms.Panel
    $pnlPreviewBox.Size = New-Object System.Drawing.Size(220, 220)
    $pnlPreviewBox.Location = New-Object System.Drawing.Point(22, 100)
    $pnlPreviewBox.BackColor = $cBg
    $pnlPreviewBox.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $pnlPreviewBox.AllowDrop = $true

    $wbPreview = New-Object System.Windows.Forms.WebBrowser
    $wbPreview.Dock = [System.Windows.Forms.DockStyle]::Fill
    $wbPreview.ScrollBarsEnabled = $false
    $wbPreview.IsWebBrowserContextMenuEnabled = $false
    $wbPreview.ScriptErrorsSuppressed = $true
    $wbPreview.AllowWebBrowserDrop = $false

    $pnlPreviewBox.Controls.Add($wbPreview)

    # Zone d'information et description a droite de l'apercu
    $lblDescHeader = New-Object System.Windows.Forms.Label
    $lblDescHeader.Text = "SYMBOLIQUE & RÔLE DANS LE JEU :"
    $lblDescHeader.ForeColor = $cMuted
    $lblDescHeader.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
    $lblDescHeader.Location = New-Object System.Drawing.Point(260, 100)
    $lblDescHeader.AutoSize = $true

    $lblDescription = New-Object System.Windows.Forms.Label
    $lblDescription.Text = "Choisissez une icône dans la liste pour voir sa symbolique, la télécharger ou la remplacer par un nouveau dessin."
    $lblDescription.ForeColor = $cText
    $lblDescription.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
    $lblDescription.Location = New-Object System.Drawing.Point(260, 122)
    $lblDescription.Size = New-Object System.Drawing.Size(380, 75)

    $lblPathHeader = New-Object System.Windows.Forms.Label
    $lblPathHeader.Text = "CHEMIN DANS LE JEU (GODOT) :"
    $lblPathHeader.ForeColor = $cMuted
    $lblPathHeader.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
    $lblPathHeader.Location = New-Object System.Drawing.Point(260, 205)
    $lblPathHeader.AutoSize = $true

    $lblCardPath = New-Object System.Windows.Forms.TextBox
    $lblCardPath.ReadOnly = $true
    $lblCardPath.BackColor = $cBg
    $lblCardPath.ForeColor = $cGold
    $lblCardPath.Font = New-Object System.Drawing.Font("Consolas", 9)
    $lblCardPath.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    $lblCardPath.Location = New-Object System.Drawing.Point(260, 225)
    $lblCardPath.Size = New-Object System.Drawing.Size(380, 24)

    # Zone d'actions principales
    $actionBox = New-Object System.Windows.Forms.GroupBox
    $actionBox.Text = "Actions sur ce logo"
    $actionBox.ForeColor = $cGold
    $actionBox.Location = New-Object System.Drawing.Point(22, 340)
    $actionBox.Size = New-Object System.Drawing.Size(620, 230)
    $actionBox.AllowDrop = $true

    $btnDownload = New-Object System.Windows.Forms.Button
    $btnDownload.Text = "📥 Télécharger ce logo (SVG)..."
    $btnDownload.Size = New-Object System.Drawing.Size(280, 42)
    $btnDownload.Location = New-Object System.Drawing.Point(20, 32)
    $btnDownload.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $btnDownload.FlatAppearance.BorderColor = $cBorder
    $btnDownload.BackColor = $cRaised
    $btnDownload.ForeColor = $cText
    $btnDownload.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
    $btnDownload.Cursor = [System.Windows.Forms.Cursors]::Hand

    $btnReplace = New-Object System.Windows.Forms.Button
    $btnReplace.Text = "🔄 Remplacer par un nouveau fichier..."
    $btnReplace.Size = New-Object System.Drawing.Size(280, 42)
    $btnReplace.Location = New-Object System.Drawing.Point(315, 32)
    $btnReplace.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $btnReplace.FlatAppearance.BorderColor = $cGold
    $btnReplace.BackColor = $cGold
    $btnReplace.ForeColor = $cBg
    $btnReplace.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
    $btnReplace.Cursor = [System.Windows.Forms.Cursors]::Hand
    $btnReplace.AllowDrop = $true

    $lblDragHint = New-Object System.Windows.Forms.Label
    $lblDragHint.Text = "💡 Astuce : Vous pouvez aussi glisser-déposer directement une image (SVG ou PNG) sur le cadre ou le bouton Remplacer !"
    $lblDragHint.ForeColor = $cMuted
    $lblDragHint.Font = New-Object System.Drawing.Font("Segoe UI", 8.5)
    $lblDragHint.Location = New-Object System.Drawing.Point(20, 86)
    $lblDragHint.Size = New-Object System.Drawing.Size(575, 24)

    $btnRestore = New-Object System.Windows.Forms.Button
    $btnRestore.Text = "↩️ Rétablir le logo d'origine"
    $btnRestore.Size = New-Object System.Drawing.Size(200, 34)
    $btnRestore.Location = New-Object System.Drawing.Point(20, 120)
    $btnRestore.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $btnRestore.FlatAppearance.BorderColor = $cBorder
    $btnRestore.BackColor = $cRaised
    $btnRestore.ForeColor = [System.Drawing.Color]::FromArgb(240, 140, 140)
    $btnRestore.Cursor = [System.Windows.Forms.Cursors]::Hand
    $btnRestore.Enabled = $false

    $btnExploreFile = New-Object System.Windows.Forms.Button
    $btnExploreFile.Text = "🔍 Afficher dans l'Explorateur"
    $btnExploreFile.Size = New-Object System.Drawing.Size(200, 34)
    $btnExploreFile.Location = New-Object System.Drawing.Point(230, 120)
    $btnExploreFile.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $btnExploreFile.FlatAppearance.BorderColor = $cBorder
    $btnExploreFile.BackColor = $cRaised
    $btnExploreFile.ForeColor = $cText
    $btnExploreFile.Cursor = [System.Windows.Forms.Cursors]::Hand

    $btnCopySvg = New-Object System.Windows.Forms.Button
    $btnCopySvg.Text = "📋 Copier le code SVG"
    $btnCopySvg.Size = New-Object System.Drawing.Size(165, 34)
    $btnCopySvg.Location = New-Object System.Drawing.Point(440, 120)
    $btnCopySvg.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $btnCopySvg.FlatAppearance.BorderColor = $cBorder
    $btnCopySvg.BackColor = $cRaised
    $btnCopySvg.ForeColor = $cText
    $btnCopySvg.Cursor = [System.Windows.Forms.Cursors]::Hand

    $lblBackupStatus = New-Object System.Windows.Forms.Label
    $lblBackupStatus.Text = ""
    $lblBackupStatus.ForeColor = $cGreen
    $lblBackupStatus.Font = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Bold)
    $lblBackupStatus.Location = New-Object System.Drawing.Point(20, 175)
    $lblBackupStatus.Size = New-Object System.Drawing.Size(575, 40)

    $actionBox.Controls.AddRange(@($btnDownload, $btnReplace, $lblDragHint, $btnRestore, $btnExploreFile, $btnCopySvg, $lblBackupStatus))

    $rightPanel.Controls.AddRange(@($lblCardCategory, $lblCardTitle, $lblCardId, $pnlPreviewBox, $lblDescHeader, $lblDescription, $lblPathHeader, $lblCardPath, $actionBox))

    $mainPanel.Controls.Add($rightPanel)
    $mainPanel.Controls.Add($splitter)
    $mainPanel.Controls.Add($leftPanel)

    $form.Controls.Add($mainPanel)
    $form.Controls.Add($statusStrip)
    $form.Controls.Add($topPanel)

    # ==============================================================================
    # LOGIQUE & ÉVÉNEMENTS
    # ==============================================================================

    $global:currentSelectedId = $null

    function Get-IconPath($id) {
        if (-not $allMeta.Contains($id)) { return $null }
        $folder = $allMeta[$id].folder
        return [System.IO.Path]::Combine($iconsDir, $folder, "$id.svg")
    }

    function Refresh-List() {
        $lstIcons.BeginUpdate()
        $lstIcons.Items.Clear()
        $filter = $cboCategory.SelectedItem.ToString()
        $query = $txtSearch.Text.Trim().ToLower()

        foreach ($id in $allMeta.Keys) {
            $meta = $allMeta[$id]
            
            # Filtre categorie
            if ($filter -ne "Toutes les catégories (74)") {
                if ($filter.StartsWith("Ressources") -and $meta.cat -ne "Ressources") { continue }
                if ($filter.StartsWith("Objets") -and $meta.cat -ne "Objets") { continue }
                if ($filter.StartsWith("Bâtiments") -and $meta.cat -ne "Bâtiments") { continue }
                if ($filter.StartsWith("Classes") -and $meta.cat -ne "Classes Sociales") { continue }
                if ($filter.StartsWith("Menus") -and $meta.cat -ne "Menus") { continue }
            }

            # Filtre texte
            if ($query -and -not ($meta.title.ToLower().Contains($query) -or $id.ToLower().Contains($query) -or $meta.desc.ToLower().Contains($query))) {
                continue
            }

            $filePath = Get-IconPath $id
            $bakPath = "$filePath.bak"
            $modifiedTag = if (Test-Path $bakPath) { " [modifié]" } else { "" }
            $displayText = "$($meta.title)$modifiedTag"
            [void]$lstIcons.Items.Add([PSCustomObject]@{ Id = $id; Text = $displayText })
        }

        $lstIcons.DisplayMember = "Text"
        $lstIcons.EndUpdate()

        if ($lstIcons.Items.Count -gt 0 -and $lstIcons.SelectedIndex -lt 0) {
            $lstIcons.SelectedIndex = 0
        }
    }

    function Load-IconDetails($id) {
        if (-not $allMeta.Contains($id)) { return }
        $global:currentSelectedId = $id
        $meta = $allMeta[$id]
        $filePath = Get-IconPath $id
        $bakPath = "$filePath.bak"

        $lblCardCategory.Text = $meta.cat.ToUpper()
        $lblCardTitle.Text = $meta.title
        $lblCardId.Text = "$id.svg"
        $lblDescription.Text = $meta.desc
        $lblCardPath.Text = "res://assets/icons/$($meta.folder)/$id.svg"

        # Vérification sauvegarde
        if (Test-Path $bakPath) {
            $btnRestore.Enabled = $true
            $lblBackupStatus.Text = "ℹ️ Ce logo a été personnalisé par vos soins. Une sauvegarde (.bak) de l'original est conservée."
            $lblBackupStatus.ForeColor = $cGold
        } else {
            $btnRestore.Enabled = $false
            $lblBackupStatus.Text = "Logo officiel d'origine."
            $lblBackupStatus.ForeColor = $cMuted
        }

        # Rendu SVG vectoriel dans le WebBrowser
        if (Test-Path $filePath) {
            $svgContent = [System.IO.File]::ReadAllText($filePath)
            $html = @"
<!DOCTYPE html>
<html>
<head>
  <meta http-equiv="X-UA-Compatible" content="IE=edge"/>
  <style>
    body { margin: 0; padding: 0; background-color: #14171c; display: flex; align-items: center; justify-content: center; height: 100vh; overflow: hidden; }
    svg { width: 170px; height: 170px; }
  </style>
</head>
<body>
  $svgContent
</body>
</html>
"@
            $wbPreview.DocumentText = $html
        }
    }

    function Replace-CurrentIcon($sourceFile) {
        if (-not $global:currentSelectedId) { return }
        if (-not (Test-Path $sourceFile)) { return }

        $targetPath = Get-IconPath $global:currentSelectedId
        $bakPath = "$targetPath.bak"

        # Sauvegarde automatique de l'original
        if (-not (Test-Path $bakPath)) {
            Copy-Item -Path $targetPath -Destination $bakPath -Force
        }

        $ext = [System.IO.Path]::GetExtension($sourceFile).ToLower()

        if ($ext -eq ".svg") {
            Copy-Item -Path $sourceFile -Destination $targetPath -Force
        }
        elseif ($ext -in @(".png", ".jpg", ".jpeg", ".webp", ".bmp")) {
            # Encapsulation propre en SVG standard
            $bytes = [System.IO.File]::ReadAllBytes($sourceFile)
            $b64 = [Convert]::ToBase64String($bytes)
            $mime = if ($ext -eq ".png") { "image/png" } elseif ($ext -eq ".webp") { "image/webp" } else { "image/jpeg" }
            $wrappedSvg = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
  <circle cx="32" cy="32" r="29" fill="#14171c" stroke="#363d4a" stroke-width="1.5"/>
  <clipPath id="circleClip"><circle cx="32" cy="32" r="27"/></clipPath>
  <image width="64" height="64" clip-path="url(#circleClip)" href="data:$mime;base64,$b64" preserveAspectRatio="xMidYMid slice"/>
  <circle cx="32" cy="32" r="27" fill="none" stroke="#d9a441" stroke-width="1"/>
</svg>
"@
            [System.IO.File]::WriteAllText($targetPath, $wrappedSvg, [System.Text.Encoding]::UTF8)

            # Copie aussi en PNG à côté pour commodité
            $pngCompanion = [System.IO.Path]::ChangeExtension($targetPath, ".png")
            Copy-Item -Path $sourceFile -Destination $pngCompanion -Force
        }
        else {
            [void][System.Windows.Forms.MessageBox]::Show("Format non supporté. Veuillez choisir un fichier .svg, .png, .jpg ou .webp.", "Format invalide", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning)
            return
        }

        $statusLabel.Text = "✅ Logo '$($global:currentSelectedId).svg' remplacé avec succès !"
        $statusLabel.ForeColor = $cGreen
        Load-IconDetails $global:currentSelectedId
        Refresh-List
    }

    # --- Événements Interface ---

    $lstIcons.add_SelectedIndexChanged({
        if ($lstIcons.SelectedItem) {
            Load-IconDetails $lstIcons.SelectedItem.Id
        }
    })

    $cboCategory.add_SelectedIndexChanged({
        Refresh-List
    })

    $txtSearch.add_TextChanged({
        Refresh-List
    })

    # Télécharger / Exporter SVG
    $btnDownload.add_Click({
        if (-not $global:currentSelectedId) { return }
        $sourcePath = Get-IconPath $global:currentSelectedId
        if (-not (Test-Path $sourcePath)) { return }

        $sfd = New-Object System.Windows.Forms.SaveFileDialog
        $sfd.Title = "Enregistrer le logo sous..."
        $sfd.FileName = "$($global:currentSelectedId).svg"
        $sfd.Filter = "Vecteur SVG (*.svg)|*.svg|Tous les fichiers (*.*)|*.*"

        if ($sfd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            Copy-Item -Path $sourcePath -Destination $sfd.FileName -Force
            $statusLabel.Text = "✅ Logo exporté avec succès vers : $($sfd.FileName)"
            [void][System.Windows.Forms.MessageBox]::Show("Logo enregistré avec succès !", "Téléchargement terminé", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
        }
    })

    # Remplacer par fichier
    $btnReplace.add_Click({
        if (-not $global:currentSelectedId) { return }
        $ofd = New-Object System.Windows.Forms.OpenFileDialog
        $ofd.Title = "Sélectionnez une nouvelle image pour $($global:currentSelectedId)"
        $ofd.Filter = "Images (*.svg;*.png;*.jpg;*.webp)|*.svg;*.png;*.jpg;*.webp|Fichiers SVG (*.svg)|*.svg|Fichiers PNG (*.png)|*.png|Tous les fichiers (*.*)|*.*"

        if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            Replace-CurrentIcon $ofd.FileName
        }
    })

    # Rétablir original
    $btnRestore.add_Click({
        if (-not $global:currentSelectedId) { return }
        $targetPath = Get-IconPath $global:currentSelectedId
        $bakPath = "$targetPath.bak"
        if (Test-Path $bakPath) {
            Copy-Item -Path $bakPath -Destination $targetPath -Force
            Remove-Item -Path $bakPath -Force
            $statusLabel.Text = "✅ Logo original rétabli pour '$($global:currentSelectedId)'."
            Load-IconDetails $global:currentSelectedId
            Refresh-List
        }
    })

    # Afficher dans Explorateur
    $btnExploreFile.add_Click({
        if (-not $global:currentSelectedId) { return }
        $filePath = Get-IconPath $global:currentSelectedId
        if (Test-Path $filePath) {
            & explorer.exe /select,"$filePath"
        }
    })

    # Copier code SVG
    $btnCopySvg.add_Click({
        if (-not $global:currentSelectedId) { return }
        $filePath = Get-IconPath $global:currentSelectedId
        if (Test-Path $filePath) {
            $content = [System.IO.File]::ReadAllText($filePath)
            [System.Windows.Forms.Clipboard]::SetText($content)
            $statusLabel.Text = "✅ Code SVG copié dans le presse-papiers !"
        }
    })

    # Ouvrir dossier icônes
    $btnOpenDir.add_Click({
        if (Test-Path $iconsDir) {
            & explorer.exe "$iconsDir"
        }
    })

    # Exporter tout en ZIP
    $btnExportAll.add_Click({
        $sfd = New-Object System.Windows.Forms.SaveFileDialog
        $sfd.Title = "Enregistrer l'archive ZIP de toutes les icônes..."
        $sfd.FileName = "Demos_Icones_74_Logos.zip"
        $sfd.Filter = "Archive ZIP (*.zip)|*.zip"

        if ($sfd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            if (Test-Path $sfd.FileName) { Remove-Item $sfd.FileName -Force }
            [System.IO.Compression.ZipFile]::CreateFromDirectory($iconsDir, $sfd.FileName)
            $statusLabel.Text = "✅ Archive ZIP créée avec succès : $($sfd.FileName)"
            [void][System.Windows.Forms.MessageBox]::Show("L'archive contenant les 74 logos a été créée avec succès !", "Export terminé", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
        }
    })

    # Drag & Drop sur les contrôles
    $dropHandler = {
        param($sender, $e)
        if ($e.Data.GetDataPresent([System.Windows.Forms.DataFormats]::FileDrop)) {
            $e.Effect = [System.Windows.Forms.DragDropEffects]::Copy
        }
    }

    $dropAction = {
        param($sender, $e)
        $files = $e.Data.GetData([System.Windows.Forms.DataFormats]::FileDrop)
        if ($files -and $files.Length -gt 0) {
            Replace-CurrentIcon $files[0]
        }
    }

    $pnlPreviewBox.add_DragEnter($dropHandler)
    $pnlPreviewBox.add_DragDrop($dropAction)
    $btnReplace.add_DragEnter($dropHandler)
    $btnReplace.add_DragDrop($dropAction)

    # Initialisation de la liste
    Refresh-List

    # Affichage de la fenetre
    [void][System.Windows.Forms.Application]::Run($form)

} catch {
    [void][System.Windows.Forms.MessageBox]::Show("Une erreur est survenue lors du lancement :`n`n$($_.Exception.Message)`n`n$($_.ScriptStackTrace)", "Projet Démos - Erreur", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error)
    exit 1
}
