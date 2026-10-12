// Archiveur : zippe un dossier en <dossier>_AAAAMMJJ-HHMM.zip, à côté du dossier.
// Utilisation :
//   Archiveur.exe <dossier> [<dossier> ...]   zippe chaque dossier
//   Archiveur.exe                              propose d'installer / désinstaller le menu contextuel
//   Archiveur.exe --install | --uninstall [--quiet]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Archiveur")]
[assembly: AssemblyProduct("Archiveur")]
[assembly: AssemblyCompany("Jean Browaeys")]
[assembly: AssemblyVersion("1.0.0.0")]
// Sans cette cible, .NET garde l'ancien comportement : chemins du zip en "\" au lieu de "/".
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

static class Archiveur
{
    const string Titre = "Archiveur";
    const string Libelle = "Archivage horodaté";
    const string Cle = "ArchiveurZip";
    static readonly string[] ClesShell =
    {
        @"Software\Classes\Directory\shell\" + Cle,
        @"Software\Classes\Directory\Background\shell\" + Cle,
    };

    static string DossierInstallation
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Archiveur"); }
    }

    static string ExeInstalle
    {
        get { return Path.Combine(DossierInstallation, "Archiveur.exe"); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        bool quiet = Array.IndexOf(args, "--quiet") >= 0;
        if (Array.IndexOf(args, "--install") >= 0) return Installer(quiet);
        if (Array.IndexOf(args, "--uninstall") >= 0) return Desinstaller(quiet);
        if (args.Length == 0) return ProposerInstallation();

        var erreurs = new List<string>();
        foreach (string arg in args)
        {
            if (arg.StartsWith("--")) continue;
            try { Zipper(arg); }
            catch (Exception ex) { erreurs.Add(arg + "\n  " + ex.Message); }
        }
        if (erreurs.Count == 0) return 0;
        if (!quiet) Message("Échec de l'archivage :\n\n" + string.Join("\n\n", erreurs), MessageBoxIcon.Error);
        else Console.Error.WriteLine(string.Join("\n", erreurs));
        return 1;
    }

    // Crée <parent>\<nom>_AAAAMMJJ-HHMM.zip contenant le dossier lui-même (comme Compress-Archive).
    // Le zip est écrit sous un nom temporaire puis renommé : jamais de zip incomplet en cas d'échec.
    public static string Zipper(string chemin)
    {
        string dossier = Path.GetFullPath(chemin.Trim('"')).TrimEnd('\\');
        if (!Directory.Exists(dossier)) throw new DirectoryNotFoundException("Dossier introuvable.");
        string parent = Path.GetDirectoryName(dossier);
        if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Impossible de zipper la racine d'un lecteur.");

        string baseNom = Path.Combine(parent, Path.GetFileName(dossier) + "_" + DateTime.Now.ToString("yyyyMMdd-HHmm"));
        string zip = baseNom + ".zip";
        for (int i = 2; File.Exists(zip); i++) zip = baseNom + "_" + i + ".zip";

        string temporaire = zip + ".part";
        try
        {
            if (File.Exists(temporaire)) File.Delete(temporaire);
            ZipFile.CreateFromDirectory(dossier, temporaire, CompressionLevel.Optimal, true);
            File.Move(temporaire, zip);
        }
        catch
        {
            try { File.Delete(temporaire); } catch { }
            throw;
        }
        return zip;
    }

    static int ProposerInstallation()
    {
        bool installe = Registry.CurrentUser.OpenSubKey(ClesShell[0]) != null;
        if (installe)
        {
            if (Question("Le menu \"" + Libelle + "\" est installé.\n\nLe désinstaller ?")) return Desinstaller(false);
        }
        else if (Question("Installer le menu contextuel \"" + Libelle + "\" pour les dossiers ?\n\n" +
                          "L'outil sera copié dans " + DossierInstallation + " (aucun droit administrateur requis)."))
        {
            return Installer(false);
        }
        return 0;
    }

    static int Installer(bool quiet)
    {
        try
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            Directory.CreateDirectory(DossierInstallation);
            if (!string.Equals(Path.GetFullPath(exe), Path.GetFullPath(ExeInstalle), StringComparison.OrdinalIgnoreCase))
                File.Copy(exe, ExeInstalle, true);

            // Clic droit sur un dossier : %1 ; clic droit dans le fond d'un dossier ouvert : %V (dossier courant).
            EcrireVerbe(ClesShell[0], "\"%1\"");
            EcrireVerbe(ClesShell[1], "\"%V\"");
            if (!quiet) Message("Menu \"" + Libelle + "\" installé.", MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception ex)
        {
            if (!quiet) Message("Échec de l'installation :\n\n" + ex.Message, MessageBoxIcon.Error);
            else Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static void EcrireVerbe(string cle, string argument)
    {
        using (RegistryKey verbe = Registry.CurrentUser.CreateSubKey(cle))
        {
            verbe.SetValue("", Libelle);
            verbe.SetValue("Icon", "\"" + ExeInstalle + "\",0");
            verbe.SetValue("MultiSelectModel", "Player");
            using (RegistryKey commande = verbe.CreateSubKey("command"))
                commande.SetValue("", "\"" + ExeInstalle + "\" " + argument);
        }
    }

    static int Desinstaller(bool quiet)
    {
        foreach (string cle in ClesShell) Registry.CurrentUser.DeleteSubKeyTree(cle, false);

        // L'exe installé ne peut pas se supprimer lui-même pendant qu'il tourne : suppression différée.
        string exe = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
        if (string.Equals(exe, Path.GetFullPath(ExeInstalle), StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo("cmd.exe",
                "/c timeout /t 2 /nobreak >nul & rd /s /q \"" + DossierInstallation + "\"")
            { CreateNoWindow = true, UseShellExecute = false });
        }
        else if (Directory.Exists(DossierInstallation))
        {
            try { Directory.Delete(DossierInstallation, true); } catch { }
        }
        if (!quiet) Message("Menu \"" + Libelle + "\" désinstallé.", MessageBoxIcon.Information);
        return 0;
    }

    static void Message(string texte, MessageBoxIcon icone)
    {
        MessageBox.Show(texte, Titre, MessageBoxButtons.OK, icone);
    }

    static bool Question(string texte)
    {
        return MessageBox.Show(texte, Titre, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
