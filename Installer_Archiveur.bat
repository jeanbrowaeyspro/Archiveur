@echo off
rem Installe ou met a jour Archiveur avec la derniere version publiee (sans droit administrateur).
setlocal
set "EXE=%TEMP%\Archiveur_installation.exe"

echo Telechargement de la derniere version d'Archiveur...
curl.exe -fsSL -o "%EXE%" https://github.com/jeanbrowaeyspro/Archiveur/releases/latest/download/Archiveur.exe
if errorlevel 1 (
    echo Echec du telechargement.
    pause
    exit /b 1
)

"%EXE%" --install --quiet
if errorlevel 1 (
    echo Echec de l'installation : fermer les fenetres d'archivage en cours et relancer.
    del "%EXE%" >nul 2>&1
    pause
    exit /b 1
)
del "%EXE%" >nul 2>&1

echo Archiveur installe.
pause
