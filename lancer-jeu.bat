@echo off
rem Compile le code C# puis lance le jeu dans Godot.
rem Si Godot est installé ailleurs, modifier le chemin ci-dessous.
set GODOT=%USERPROFILE%\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe

cd /d "%~dp0"

echo Compilation...
dotnet build Demos.Game.sln -nologo -v q
if errorlevel 1 (
    echo La compilation a echoue.
    pause
    exit /b 1
)

if not exist "%GODOT%" (
    echo Godot introuvable : %GODOT%
    pause
    exit /b 1
)

start "" "%GODOT%" --path game
