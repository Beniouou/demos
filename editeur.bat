@echo off
chcp 65001 >nul
rem Ouvre l'editeur de contenu : creer des produits, des recettes, des types de batiments et des besoins.
rem Chaque creation applique d'abord le tableur, ecrit dans game/data, puis regenere le tableur (fermez-le dans Excel).

cd /d "%~dp0"

dotnet build tools\Demos.Editeur -nologo -v q
if errorlevel 1 (
    echo La compilation de l'editeur a echoue.
    pause
    exit /b 1
)

start "" "tools\Demos.Editeur\bin\Debug\net10.0-windows\Demos.Editeur.exe"
