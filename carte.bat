@echo off
chcp 65001 >nul
rem Ouvre l'editeur graphique de carte topographique IGN : reglage du relief,
rem courbes de niveau en direct, filtres et enregistrement dans game/data/carte.json.

cd /d "%~dp0"

dotnet build tools\Demos.Carte -nologo -v q
if errorlevel 1 (
    echo La compilation de l'editeur de carte a echoue.
    pause
    exit /b 1
)

start "" "tools\Demos.Carte\bin\Debug\net10.0-windows\Demos.Carte.exe"
