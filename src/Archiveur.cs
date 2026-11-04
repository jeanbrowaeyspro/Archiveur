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
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Archiveur")]
[assembly: AssemblyProduct("Archiveur")]
[assembly: AssemblyCompany("Jean Browaeys")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
[assembly: AssemblyInformationalVersion("1.2.0")]
// Sans cette cible, .NET garde l'ancien comportement : chemins du zip en "\" au lieu de "/".
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

static class Archiveur
{
    internal const string Libelle = "Archivage horodaté";
    const string Cle = "ArchiveurZip";
    static readonly string[] ClesShell =
    {
        @"Software\Classes\Directory\shell\" + Cle,
        @"Software\Classes\Directory\Background\shell\" + Cle,
    };

    // Redirigeable par les essais.
    internal static string DossierInstallation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Archiveur");

    internal static string ExeInstalle
    {
        get { return Path.Combine(DossierInstallation, "Archiveur.exe"); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        // Avant toute fenêtre ou tout contrôle.
        Theme.InitialiserDpi();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        MiseAJour.SupprimerAncienExe();
        bool quiet = Array.IndexOf(args, "--quiet") >= 0;
        if (Array.IndexOf(args, "--install") >= 0) return Installer(quiet);
        if (Array.IndexOf(args, "--uninstall") >= 0) return Desinstaller(quiet);
        if (args.Length == 0) return ProposerInstallation();

        var dossiers = new List<string>();
        foreach (string arg in args) if (!arg.StartsWith("--")) dossiers.Add(arg);

        // L'archivage tourne dans un thread ; la fenêtre de progression s'affiche toujours
        // et se ferme dès qu'il est fini (aucun message de fin).
        var erreurs = new List<string>();
        var progression = new Progression();
        var travail = new Thread(() =>
        {
            for (int i = 0; i < dossiers.Count && !progression.Annule; i++)
            {
                progression.Demarrer(dossiers[i], i + 1, dossiers.Count);
                try { Zipper(dossiers[i], progression); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { erreurs.Add(dossiers[i] + "\n  " + ex.Message); }
            }
        });
        travail.Start();
        Thread verification = null;
        if (!quiet)
        {
            verification = MiseAJour.LancerVerification();
            Application.Run(new FenetreProgression(progression, travail));
        }
        travail.Join();

        if (erreurs.Count > 0)
        {
            if (!quiet) Message("Échec de l'archivage :\n\n" + string.Join("\n\n", erreurs), MessageBoxIcon.Error);
            else Console.Error.WriteLine(string.Join("\n", erreurs));
        }
        if (!quiet) MiseAJour.Proposer(verification);
        return erreurs.Count == 0 ? 0 : 1;
    }

    // Crée <parent>\<nom>_AAAAMMJJ-HHMM.zip contenant le dossier lui-même (comme Compress-Archive).
    // Le zip est écrit sous un nom temporaire puis renommé : jamais de zip incomplet en cas d'échec ou d'annulation.
    public static string Zipper(string chemin, Progression progression)
    {
        // GetFullPath rend aussi le nom long réel d'un nom court 8.3 (PROJET~1) passé par l'Explorateur.
        string dossier = Path.GetFullPath(chemin.Trim('"')).TrimEnd('\\');
        if (string.IsNullOrEmpty(Path.GetDirectoryName(dossier))) throw new InvalidOperationException("Impossible de zipper la racine d'un lecteur.");
        // Tous les chemins qui suivent dérivent de la forme étendue : pas de limite de 260 caractères.
        dossier = Etendu(dossier);
        if (!Directory.Exists(dossier)) throw new DirectoryNotFoundException("Dossier introuvable.");

        string baseNom = dossier + "_" + DateTime.Now.ToString("yyyyMMdd-HHmm");
        string zip = baseNom + ".zip";
        for (int i = 2; File.Exists(zip); i++) zip = baseNom + "_" + i + ".zip";

        string temporaire = zip + ".part";
        try
        {
            if (File.Exists(temporaire)) File.Delete(temporaire);
            using (var flux = new FileStream(temporaire, FileMode.CreateNew))
            using (var archive = new ZipArchive(flux, ZipArchiveMode.Create))
                Remplir(archive, dossier, progression);
            File.Move(temporaire, zip);
        }
        catch
        {
            try { File.Delete(temporaire); } catch { }
            throw;
        }
        return zip;
    }

    // Ajoute le dossier (fichiers et sous-dossiers vides) sous "<nom>/", en suivant l'avancement en octets.
    static void Remplir(ZipArchive archive, string dossier, Progression progression)
    {
        string racine = Path.GetFileName(dossier);
        string[] fichiers = Directory.GetFiles(dossier, "*", SearchOption.AllDirectories);
        long total = 0;
        foreach (string f in fichiers) total += new FileInfo(f).Length;
        progression.Total = total;

        foreach (string sousDossier in Directory.GetDirectories(dossier, "*", SearchOption.AllDirectories))
            if (Directory.GetFileSystemEntries(sousDossier).Length == 0)
                archive.CreateEntry(NomEntree(racine, dossier, sousDossier) + "/");

        var tampon = new byte[1 << 20];
        foreach (string f in fichiers)
        {
            string nom = NomEntree(racine, dossier, f);
            progression.Fichier = nom;
            try
            {
                ZipArchiveEntry entree = archive.CreateEntry(nom, CompressionLevel.Optimal);
                entree.LastWriteTime = DateZip(File.GetLastWriteTime(f));
                // FileShare.ReadWrite : accepte aussi les fichiers ouverts par un autre logiciel (journaux…).
                using (var source = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (Stream cible = entree.Open())
                {
                    int lus;
                    while ((lus = source.Read(tampon, 0, tampon.Length)) > 0)
                    {
                        if (progression.Annule) throw new OperationCanceledException();
                        cible.Write(tampon, 0, lus);
                        Interlocked.Add(ref progression.Fait, lus);
                    }
                }
            }
            catch (IOException ex) { throw new IOException(nom + " : " + ex.Message, ex); }
            catch (UnauthorizedAccessException ex) { throw new IOException(nom + " : " + ex.Message, ex); }
        }
    }

    // Forme étendue d'un chemin complet (\\?\C:\... ou \\?\UNC\serveur\partage\...) : les API Windows
    // acceptent alors plus de 260 caractères, sans le réglage LongPathsEnabled ni droits administrateur.
    internal static string Etendu(string chemin)
    {
        if (chemin.StartsWith(@"\\?\") || chemin.StartsWith(@"\\.\")) return chemin;
        if (chemin.StartsWith(@"\\")) return @"\\?\UNC\" + chemin.Substring(2);
        return @"\\?\" + chemin;
    }

    static string NomEntree(string racine, string dossier, string chemin)
    {
        return racine + "/" + chemin.Substring(dossier.Length + 1).Replace('\\', '/');
    }

    // Le format zip ne stocke que les dates de 1980 à 2107.
    static DateTimeOffset DateZip(DateTime date)
    {
        if (date.Year < 1980) return new DateTime(1980, 1, 1);
        if (date.Year > 2107) return new DateTime(2107, 12, 31);
        return date;
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

    internal static void Message(string texte, MessageBoxIcon icone)
    {
        Dialogue.Afficher(texte, icone, "OK");
    }

    static bool Question(string texte)
    {
        return Dialogue.Afficher(texte, MessageBoxIcon.Question, "Oui", "Non") == 0;
    }
}
