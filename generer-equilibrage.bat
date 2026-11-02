@echo off
rem Génère le tableur d'équilibrage (equilibrage demos.xlsx, à côté du dossier demos) à partir des données du jeu.
rem Fermer le tableur avant de lancer la génération : un fichier ouvert ne peut pas être remplacé.

cd /d "%~dp0"

dotnet run --project tools\Demos.Equilibrage -nologo
if errorlevel 1 (
    echo La generation a echoue.
    pause
    exit /b 1
)

pause
