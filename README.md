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

## Installation

1. Télécharger `Archiveur.exe` depuis la [dernière release](https://github.com/jeanbrowaeyspro/Archiveur/releases/latest) (ou le compiler avec `build.cmd`, voir plus bas).
2. Double-cliquer sur `Archiveur.exe` et répondre "Oui".

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
