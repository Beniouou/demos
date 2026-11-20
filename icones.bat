@echo off
chcp 65001 >nul
rem Lance le gestionnaire et la galerie de logos de Projet Demos (74 logos)

set "HTML_PATH=%~dp0galerie-icones.html"
if not exist "%HTML_PATH%" set "HTML_PATH=%~dp0..\galerie-icones.html"

rem Tente d'ouvrir en mode fenetre autonome Microsoft Edge (Chromium)
if exist "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" (
    start "" "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --app="%HTML_PATH%"
    exit /b 0
)

if exist "C:\Program Files\Microsoft\Edge\Application\msedge.exe" (
    start "" "C:\Program Files\Microsoft\Edge\Application\msedge.exe" --app="%HTML_PATH%"
    exit /b 0
)

rem Repli : ouvre avec le navigateur par defaut du systeme
start "" "%HTML_PATH%"
