@echo off
rem Compile Archiveur.exe avec le compilateur C# du .NET Framework 4.8 fourni avec Windows.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set NET=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
cd /d "%~dp0"
if not exist Outils mkdir Outils
rem L'icône src\Archiveur.ico est produite par src\GenererIcone.cs (à recompiler et relancer pour la modifier).
"%CSC%" /nologo /target:winexe /optimize+ /utf8output /codepage:65001 /out:Outils\Archiveur.exe /win32icon:src\Archiveur.ico ^
  /resource:src\Archiveur.ico,Archiveur.ico ^
  /r:"%NET%\System.IO.Compression.dll" /r:"%NET%\System.IO.Compression.FileSystem.dll" /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /r:"%NET%\System.Web.Extensions.dll" ^
  src\Archiveur.cs src\FenetreProgression.cs src\Dialogue.cs src\Theme.cs src\MiseAJour.cs
exit /b %ERRORLEVEL%
