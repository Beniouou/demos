@echo off
chcp 65001 >nul
rem Projet Demos — Outil de remplacement rapide de logo
rem Vous pouvez double-cliquer ou glisser-deposer une image (.png, .svg, .jpg) sur ce fichier.

set "SCRIPT_PATH=%~dp0tools\Demos.Icones\RemplacerLogo.ps1"
if not exist "%SCRIPT_PATH%" set "SCRIPT_PATH=%~dp0demos\tools\Demos.Icones\RemplacerLogo.ps1"

powershell.exe -ExecutionPolicy Bypass -NoProfile -STA -File "%SCRIPT_PATH%" -SourceImageFile "%~1"
