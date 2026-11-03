@echo off
chcp 65001 >nul
rem Reporte les cellules jaunes du tableur d'equilibrage (equilibrage demos.xlsx) dans les donnees du jeu (game/data).
rem Enregistrer le tableur avant de lancer. Si une valeur est fausse, rien n'est ecrit et la cellule est indiquee.

cd /d "%~dp0"

dotnet run --project tools\Demos.Equilibrage -- appliquer
if errorlevel 1 (
    echo Rien n'a ete applique.
    pause
    exit /b 1
)

pause
