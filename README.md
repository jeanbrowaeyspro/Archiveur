# Archiveur

Entrée "Archivage horodaté" dans le menu contextuel de l'Explorateur Windows : un clic droit sur un dossier crée à côté de lui l'archive `<dossier>_AAAAMMJJ-HHMM.zip`.

- Plusieurs dossiers sélectionnés : une archive par dossier.
- Clic droit dans le fond d'un dossier ouvert : archive du dossier courant.
- Fenêtre de progression (pourcentage aussi sur le bouton de la barre des tâches), fermée dès la fin de l'archivage, bouton Annuler.
- Archive écrite sous un nom temporaire puis renommée : jamais de zip incomplet en cas d'erreur ou d'annulation.
- Deux archives dans la même minute : suffixe `_2`, `_3`…
- Fichiers ouverts en écriture par un autre logiciel (journaux…) archivés aussi.
- Chemins de plus de 260 caractères acceptés, sans réglage Windows (LongPathsEnabled) ni droit administrateur.
- Aucun script PowerShell, aucun droit administrateur, aucun runtime à installer (.NET Framework 4.8, fourni avec Windows 10 et 11).

Sous Windows 11, l'entrée apparaît dans le menu contextuel classique ("Afficher plus d'options" ou Maj + clic droit).

## Solution TwinCAT : connexion à l'automate sans rechargement

Pour qu'un collègue qui dézippe une solution TwinCAT 3 puisse se connecter à l'automate en ligne sans recharger le programme, l'archive doit contenir le fichier `.compileinfo` de la dernière compilation (dossier `_CompileInfo` du projet PLC). À la connexion, TwinCAT compile à partir de ce fichier et compare le résultat avec le programme de l'automate : si rien ne change, il se connecte sans rechargement. Conditions :

- Archive faite après le dernier chargement dans l'automate : le `.compileinfo` le plus récent doit correspondre au programme qui tourne (pas de compilation de sources modifiées entre le chargement et l'archivage).
- Pas de "Clean" ni de "Rebuild all" sur le poste du collègue avant la connexion : ces commandes effacent le `.compileinfo` et recompilent tout, ce qui oblige à recharger.
- Mêmes bibliothèques sur le poste du collègue (mêmes versions des bibliothèques Beckhoff et des bibliothèques maison) et version de TwinCAT compatible : sinon le code compilé diffère et TwinCAT demande un changement en ligne ou un rechargement.

Ces conditions ne concernent pas un projet d'IHM TwinCAT HMI, qui se compile et se publie normalement.

## Installation

1. Télécharger `Archiveur.exe` depuis la [dernière release](https://github.com/jeanbrowaeyspro/Archiveur/releases/latest) (ou le compiler avec `build.cmd`, voir plus bas).
2. Double-cliquer sur `Archiveur.exe` et répondre "Oui".

Ou, sans question : télécharger et lancer [`Installer_Archiveur.bat`](https://github.com/jeanbrowaeyspro/Archiveur/raw/main/Installer_Archiveur.bat), qui installe la dernière release (ou met à jour une version déjà installée).

L'exe se copie dans `%LOCALAPPDATA%\Programs\Archiveur` et enregistre le menu dans `HKEY_CURRENT_USER\Software\Classes\Directory`. Un nouveau double-clic propose la désinstallation.

En ligne de commande :

```bat
Archiveur.exe --install [--quiet]
Archiveur.exe --uninstall [--quiet]
Archiveur.exe <dossier> [<dossier> ...] [--quiet]
```

`--quiet` : ni fenêtre ni message, erreurs sur la sortie d'erreur et code de retour 1.

## Mise à jour

Pendant un archivage, l'outil installé consulte la dernière release GitHub en arrière-plan (sans ralentir l'archivage ; hors ligne ou derrière un proxy bloquant, rien ne se passe). Si une version plus récente existe, elle est proposée à la fin de l'archivage : "Mettre à jour", "Plus tard" ou "Ignorer cette version".

"Mettre à jour" télécharge le nouvel `Archiveur.exe` et remplace celui de `%LOCALAPPDATA%\Programs\Archiveur` (l'ancien est remis en place en cas d'échec) ; le menu contextuel ne change pas. La dernière version connue et la version ignorée sont mémorisées dans `%APPDATA%\Archiveur\settings.json`. Aucune vérification avec `--quiet`.

## Compilation

```bat
build.cmd
```

Utilise le compilateur C# du .NET Framework 4.8 présent dans Windows (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) : rien à installer.

L'icône `src\Archiveur.ico` est dessinée par `src\GenererIcone.cs` ; pour la modifier, compiler et lancer ce programme avec le chemin de l'icône en argument.

## Licence

MIT, voir [LICENSE](LICENSE).

Jean Browaeys — Automaticien Indépendant
