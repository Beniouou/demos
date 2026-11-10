@echo off
chcp 65001 >nul
rem Simule la partie de depart sans joueur et ouvre le rapport (rapport simulation.html, a cote du dossier demos).
rem Par defaut 3 ans. Exemples : "simuler.bat 5" pour 5 ans, "simuler.bat 3 commune" pour fonder une 2e commune
rem a 500 lieues de la capitale, "simuler.bat 3 commune=1500" pour la placer a 1500 lieues.

cd /d "%~dp0"

dotnet run --project tools\Demos.Equilibrage -- simuler %*
if errorlevel 1 (
    echo La simulation a echoue.
    pause
    exit /b 1
)

start "" "%~dp0..\rapport simulation.html"
