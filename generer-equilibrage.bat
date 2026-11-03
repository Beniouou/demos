@echo off
chcp 65001 >nul
rem Genere le tableur d'equilibrage (equilibrage demos.xlsx, a cote du dossier demos) a partir des donnees du jeu.
rem Attention : les modifications du tableur qui n'ont pas ete appliquees sont perdues.
rem Fermer le tableur avant de lancer la generation : un fichier ouvert ne peut pas etre remplace.

cd /d "%~dp0"

dotnet run --project tools\Demos.Equilibrage -- generer
if errorlevel 1 (
    echo La generation a echoue.
    pause
    exit /b 1
)

pause
