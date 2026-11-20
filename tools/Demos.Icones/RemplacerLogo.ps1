# ==============================================================================
# Projet Demos — Remplacement Rapide de Logo
# Permet de choisir un logo du jeu et de le remplacer par une nouvelle image.
# ==============================================================================

param(
    [string]$SourceImageFile = ""
)

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

[void][System.Windows.Forms.Application]::EnableVisualStyles()

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$iconsDir = [System.IO.Path]::GetFullPath((Join-Path $scriptDir "..\..\game\assets\icons"))

if (-not (Test-Path $iconsDir)) {
    $candidate = "c:\Users\garyv\Desktop\Projet Demos\demos\game\assets\icons"
    if (Test-Path $candidate) { $iconsDir = $candidate }
}

# 74 logos du jeu
$logos = [ordered]@{
    # Menus
    "menu-carte" = @{ name = "Menus / Carte"; folder = "menus" }
    "menu-lois" = @{ name = "Menus / Lois"; folder = "menus" }
    "menu-structure" = @{ name = "Menus / Structure"; folder = "menus" }
    "menu-population" = @{ name = "Menus / Population"; folder = "menus" }
    "menu-production" = @{ name = "Menus / Production"; folder = "menus" }
    "menu-recherche" = @{ name = "Menus / Recherche"; folder = "menus" }
    "menu-ressources" = @{ name = "Menus / Ressources"; folder = "menus" }
    "menu-aide" = @{ name = "Menus / Aide"; folder = "menus" }

    # Classes
    "classe-ouvriers" = @{ name = "Classes / Ouvriers"; folder = "classes" }
    "classe-intermediaires" = @{ name = "Classes / Professions intermédiaires"; folder = "classes" }
    "classe-cadres" = @{ name = "Classes / Cadres & Dirigeants"; folder = "classes" }
    "pole-eg-egalitariste" = @{ name = "Classes / Pôle Égalitariste"; folder = "classes" }
    "pole-se-segregationniste" = @{ name = "Classes / Pôle Ségrégationniste"; folder = "classes" }
    "pole-po-positiviste" = @{ name = "Classes / Pôle Positiviste"; folder = "classes" }
    "pole-ec-ecologiste" = @{ name = "Classes / Pôle Écologiste"; folder = "classes" }
    "pole-pr-protectionniste" = @{ name = "Classes / Pôle Protectionniste"; folder = "classes" }
    "pole-li-liberaux" = @{ name = "Classes / Pôle Libéraux"; folder = "classes" }

    # Bâtiments
    "cat-administration" = @{ name = "Bâtiments / Administration"; folder = "batiments" }
    "cat-extraction" = @{ name = "Bâtiments / Extraction"; folder = "batiments" }
    "cat-production" = @{ name = "Bâtiments / Production"; folder = "batiments" }
    "cat-energie" = @{ name = "Bâtiments / Énergie"; folder = "batiments" }
    "cat-services" = @{ name = "Bâtiments / Services"; folder = "batiments" }
    "cat-logistique" = @{ name = "Bâtiments / Logistique"; folder = "batiments" }
    "cat-habitat" = @{ name = "Bâtiments / Habitat"; folder = "batiments" }
    "cat-eau" = @{ name = "Bâtiments / Eau"; folder = "batiments" }
    "cat-culture" = @{ name = "Bâtiments / Culture"; folder = "batiments" }
    "bat-camp-de-base" = @{ name = "Bâtiments / Camp de base"; folder = "batiments" }
    "bat-mairie" = @{ name = "Bâtiments / Mairie"; folder = "batiments" }
    "bat-prefecture" = @{ name = "Bâtiments / Préfecture"; folder = "batiments" }
    "bat-ferme" = @{ name = "Bâtiments / Ferme"; folder = "batiments" }
    "bat-camp-forestier" = @{ name = "Bâtiments / Camp forestier"; folder = "batiments" }
    "bat-mine" = @{ name = "Bâtiments / Mine"; folder = "batiments" }
    "bat-carriere" = @{ name = "Bâtiments / Carrière"; folder = "batiments" }
    "bat-atelier" = @{ name = "Bâtiments / Atelier"; folder = "batiments" }
    "bat-forge" = @{ name = "Bâtiments / Forge"; folder = "batiments" }
    "bat-ecole" = @{ name = "Bâtiments / École"; folder = "batiments" }
    "bat-theatre" = @{ name = "Bâtiments / Théâtre"; folder = "batiments" }

    # Ressources
    "res-ble" = @{ name = "Ressources / Blé"; folder = "ressources" }
    "res-bois" = @{ name = "Ressources / Bois"; folder = "ressources" }
    "res-pierre" = @{ name = "Ressources / Pierre"; folder = "ressources" }
    "res-charbon" = @{ name = "Ressources / Charbon"; folder = "ressources" }
    "res-minerai-fer" = @{ name = "Ressources / Minerai de fer"; folder = "ressources" }
    "res-minerai-cuivre" = @{ name = "Ressources / Minerai de cuivre"; folder = "ressources" }
    "res-or" = @{ name = "Ressources / Or"; folder = "ressources" }
    "res-sable" = @{ name = "Ressources / Sable"; folder = "ressources" }
    "res-argile" = @{ name = "Ressources / Argile"; folder = "ressources" }
    "res-marbre" = @{ name = "Ressources / Marbre"; folder = "ressources" }
    "res-lin" = @{ name = "Ressources / Lin"; folder = "ressources" }
    "res-carcasse" = @{ name = "Ressources / Carcasse"; folder = "ressources" }
    "res-eau" = @{ name = "Ressources / Eau"; folder = "ressources" }
    "res-petrole" = @{ name = "Ressources / Pétrole"; folder = "ressources" }
    "res-farine" = @{ name = "Ressources / Farine"; folder = "ressources" }
    "res-cuir" = @{ name = "Ressources / Cuir"; folder = "ressources" }
    "res-tresor" = @{ name = "Ressources / Trésor / Monnaie"; folder = "ressources" }

    # Objets
    "obj-pain" = @{ name = "Objets / Pain"; folder = "objets" }
    "obj-viande" = @{ name = "Objets / Viande"; folder = "objets" }
    "obj-vetements" = @{ name = "Objets / Vêtements"; folder = "objets" }
    "obj-meubles" = @{ name = "Objets / Meubles"; folder = "objets" }
    "obj-poterie" = @{ name = "Objets / Poterie"; folder = "objets" }
    "obj-alcool" = @{ name = "Objets / Alcool"; folder = "objets" }
    "obj-administration" = @{ name = "Objets / Services administratifs"; folder = "objets" }
    "obj-securite" = @{ name = "Objets / Services de sécurité"; folder = "objets" }
    "obj-energie" = @{ name = "Objets / Énergie"; folder = "objets" }
    "obj-tissus" = @{ name = "Objets / Tissus"; folder = "objets" }
    "obj-planche" = @{ name = "Objets / Planche"; folder = "objets" }
    "obj-lingot-fer" = @{ name = "Objets / Lingot de fer"; folder = "objets" }
    "obj-lingot-cuivre" = @{ name = "Objets / Lingot de cuivre"; folder = "objets" }
    "obj-plaque-de-fer" = @{ name = "Objets / Plaque de fer"; folder = "objets" }
    "obj-outils" = @{ name = "Objets / Outils"; folder = "objets" }
    "obj-sculpture" = @{ name = "Objets / Sculpture"; folder = "objets" }
    "obj-piece-de-theatre" = @{ name = "Objets / Pièce de théâtre"; folder = "objets" }
    "obj-ciment" = @{ name = "Objets / Ciment"; folder = "objets" }
    "obj-materiaux-de-construction" = @{ name = "Objets / Matériaux de construction"; folder = "objets" }
    "obj-materiaux-de-construction-technique" = @{ name = "Objets / Matériaux techniques"; folder = "objets" }
}

# 1. Selection du fichier source si non passe en argument
$src = $SourceImageFile
if (-not $src -or -not (Test-Path $src)) {
    $ofd = New-Object System.Windows.Forms.OpenFileDialog
    $ofd.Title = "Sélectionnez votre nouvelle image (SVG, PNG, JPG...)"
    $ofd.Filter = "Images supportées (*.svg;*.png;*.jpg;*.webp)|*.svg;*.png;*.jpg;*.webp|Fichiers SVG (*.svg)|*.svg|Fichiers PNG (*.png)|*.png|Tous (*.*)|*.*"
    if ($ofd.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        exit 0
    }
    $src = $ofd.FileName
}

# 2. Fenetre de selection du logo a remplacer
$form = New-Object System.Windows.Forms.Form
$form.Text = "Projet Démos — Choisir le logo à remplacer"
$form.Size = New-Object System.Drawing.Size(520, 260)
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.BackColor = [System.Drawing.Color]::FromArgb(20, 23, 28)
$form.ForeColor = [System.Drawing.Color]::FromArgb(230, 232, 236)
$form.Font = New-Object System.Drawing.Font("Segoe UI", 9.5)
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
$form.MaximizeBox = $false
$form.MinimizeBox = $false

$lblSrc = New-Object System.Windows.Forms.Label
$lblSrc.Text = "Nouvelle image sélectionnée : " + [System.IO.Path]::GetFileName($src)
$lblSrc.Location = New-Object System.Drawing.Point(20, 16)
$lblSrc.Size = New-Object System.Drawing.Size(460, 22)
$lblSrc.ForeColor = [System.Drawing.Color]::FromArgb(217, 164, 65)

$lblPrompt = New-Object System.Windows.Forms.Label
$lblPrompt.Text = "Sélectionnez quel logo du jeu vous souhaitez remplacer :"
$lblPrompt.Location = New-Object System.Drawing.Point(20, 44)
$lblPrompt.Size = New-Object System.Drawing.Size(460, 22)

$cbo = New-Object System.Windows.Forms.ComboBox
$cbo.Location = New-Object System.Drawing.Point(20, 72)
$cbo.Size = New-Object System.Drawing.Size(460, 28)
$cbo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
$cbo.BackColor = [System.Drawing.Color]::FromArgb(38, 43, 52)
$cbo.ForeColor = [System.Drawing.Color]::FromArgb(230, 232, 236)

foreach ($key in $logos.Keys) {
    $info = $logos[$key]
    [void]$cbo.Items.Add([PSCustomObject]@{ Id = $key; Text = "$($info.name) ($key.svg)" })
}
$cbo.DisplayMember = "Text"
$cbo.SelectedIndex = 0

$btnOk = New-Object System.Windows.Forms.Button
$btnOk.Text = "✅ Remplacer ce logo dans le jeu"
$btnOk.Location = New-Object System.Drawing.Point(20, 130)
$btnOk.Size = New-Object System.Drawing.Size(260, 42)
$btnOk.BackColor = [System.Drawing.Color]::FromArgb(217, 164, 65)
$btnOk.ForeColor = [System.Drawing.Color]::FromArgb(20, 23, 28)
$btnOk.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$btnOk.Font = New-Object System.Drawing.Font("Segoe UI", 9.5, [System.Drawing.FontStyle]::Bold)
$btnOk.DialogResult = [System.Windows.Forms.DialogResult]::OK

$btnCancel = New-Object System.Windows.Forms.Button
$btnCancel.Text = "Annuler"
$btnCancel.Location = New-Object System.Drawing.Point(300, 130)
$btnCancel.Size = New-Object System.Drawing.Size(180, 42)
$btnCancel.BackColor = [System.Drawing.Color]::FromArgb(38, 43, 52)
$btnCancel.ForeColor = [System.Drawing.Color]::FromArgb(230, 232, 236)
$btnCancel.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$btnCancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel

$form.Controls.AddRange(@($lblSrc, $lblPrompt, $cbo, $btnOk, $btnCancel))
$form.AcceptButton = $btnOk
$form.CancelButton = $btnCancel

if ($form.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
    exit 0
}

$selectedItem = $cbo.SelectedItem
$selectedId = $selectedItem.Id
$info = $logos[$selectedId]

$targetPath = Join-Path $iconsDir "$($info.folder)\$selectedId.svg"
$bakPath = "$targetPath.bak"

# 1. Sauvegarde automatique
if (-not (Test-Path $bakPath)) {
    Copy-Item -Path $targetPath -Destination $bakPath -Force
}

# 2. Remplacement selon le format
$ext = [System.IO.Path]::GetExtension($src).ToLower()
if ($ext -eq ".svg") {
    Copy-Item -Path $src -Destination $targetPath -Force
}
elseif ($ext -in @(".png", ".jpg", ".jpeg", ".webp", ".bmp")) {
    $bytes = [System.IO.File]::ReadAllBytes($src)
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

    # Copie aussi en PNG
    $pngPath = [System.IO.Path]::ChangeExtension($targetPath, ".png")
    Copy-Item -Path $src -Destination $pngPath -Force
}

[System.Windows.Forms.MessageBox]::Show("Le logo '$selectedId' a été remplacé avec succès dans Projet Démos !`n`nEmplacement : $targetPath`nUne sauvegarde (.bak) de l'original est conservée.", "Remplacement réussi", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
