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
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3.0")]
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

        var arguments = new List<string>();
        foreach (string arg in args) if (!arg.StartsWith("--")) arguments.Add(arg);
        var erreurs = new List<string>();
        var dossiers = new List<string>();
        var twinCAT = new List<bool>();
        if (!ChoisirModes(arguments, quiet, dossiers, twinCAT, erreurs)) return 0;

        // L'archivage tourne dans un thread ; la fenêtre de progression s'affiche toujours
        // et se ferme dès qu'il est fini (aucun message de fin).
        var progression = new Progression();
        var travail = new Thread(() =>
        {
            for (int i = 0; i < dossiers.Count && !progression.Annule; i++)
            {
                progression.Demarrer(dossiers[i], i + 1, dossiers.Count);
                progression.TwinCAT = twinCAT[i];
                try { Zipper(dossiers[i], progression); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { erreurs.Add(Normal(dossiers[i]) + "\n  " + ex.Message); }
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

    // Vérifie chaque dossier et, pour une solution TwinCAT, demande le type d'archivage (optimisé sans question
    // avec --quiet). Rend false si l'utilisateur annule ; un dossier en erreur est signalé et écarté.
    internal static bool ChoisirModes(List<string> arguments, bool quiet, List<string> dossiers, List<bool> twinCAT, List<string> erreurs)
    {
        foreach (string arg in arguments)
        {
            string dossier;
            bool solution;
            try
            {
                dossier = Preparer(arg);
                solution = EstSolutionTwinCAT(dossier);
            }
            catch (Exception ex) { erreurs.Add(arg + "\n  " + ex.Message); continue; }

            if (solution && !quiet)
            {
                int choix = Dialogue.Afficher(Path.GetFileName(dossier) + " est une solution TwinCAT.\n\n" +
                    "Archivage optimisé : fichiers suivis par git avec leurs modifications en cours, historique git, " +
                    "dernier .compileinfo de chaque projet PLC (connexion à l'automate sans rechargement), " +
                    "sans Packages, bin, obj, .vs…\n\nArchivage normal : tout le dossier.",
                    MessageBoxIcon.Question, "Optimisé TwinCAT", "Tout le dossier", "Annuler");
                if (choix < 0 || choix == 2) return false;
                solution = choix == 0;
            }
            dossiers.Add(dossier);
            twinCAT.Add(solution);
        }
        return true;
    }

    // Chemin complet du dossier, sous forme étendue : aucune limite de 260 caractères pour les chemins qui en dérivent.
    static string Preparer(string chemin)
    {
        // GetFullPath rend aussi le nom long réel d'un nom court 8.3 (PROJET~1) passé par l'Explorateur.
        string dossier = Path.GetFullPath(chemin.Trim('"')).TrimEnd('\\');
        if (string.IsNullOrEmpty(Path.GetDirectoryName(dossier))) throw new InvalidOperationException("Impossible de zipper la racine d'un lecteur.");
        dossier = Etendu(dossier);
        if (!Directory.Exists(dossier)) throw new DirectoryNotFoundException("Dossier introuvable.");
        return dossier;
    }

    // Crée <parent>\<nom>_AAAAMMJJ-HHMM.zip contenant le dossier lui-même (comme Compress-Archive), en mode
    // TwinCAT optimisé si progression.TwinCAT est vrai.
    // Le zip est écrit sous un nom temporaire puis renommé : jamais de zip incomplet en cas d'échec ou d'annulation.
    public static string Zipper(string chemin, Progression progression)
    {
        string dossier = Preparer(chemin);
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
    // Mode TwinCAT : seulement ce qu'il faut pour reprendre le travail (voir ListerTwinCAT).
    static void Remplir(ZipArchive archive, string dossier, Progression progression)
    {
        string racine = Path.GetFileName(dossier);
        var fichiers = new List<string>();
        var vides = new List<string>();
        if (progression.TwinCAT) ListerTwinCAT(dossier, fichiers, vides);
        else Parcourir(dossier, null, null, fichiers, vides);

        long total = 0;
        foreach (string f in fichiers) total += new FileInfo(f).Length;
        progression.Total = total;

        foreach (string sousDossier in vides)
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

    // Parcourt récursivement un dossier : fichiers et sous-dossiers vides, sauf les dossiers et fichiers exclus
    // (noms comparés sans tenir compte de la casse, motifs de fichier "*.ext" ou nom exact).
    static void Parcourir(string dossier, HashSet<string> dossiersExclus, string[] fichiersExclus, List<string> fichiers, List<string> vides)
    {
        foreach (string f in Directory.GetFiles(dossier))
            if (!Exclu(Path.GetFileName(f), fichiersExclus)) fichiers.Add(f);
        foreach (string d in Directory.GetDirectories(dossier))
        {
            if (dossiersExclus != null && dossiersExclus.Contains(Path.GetFileName(d))) continue;
            if (Directory.GetFileSystemEntries(d).Length == 0) vides.Add(d);
            else Parcourir(d, dossiersExclus, fichiersExclus, fichiers, vides);
        }
    }

    static bool Exclu(string nom, string[] motifs)
    {
        if (motifs == null) return false;
        foreach (string motif in motifs)
        {
            if (motif.StartsWith("*") ? nom.EndsWith(motif.Substring(1), StringComparison.OrdinalIgnoreCase)
                                      : string.Equals(nom, motif, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // Solution TwinCAT : un *.sln à la racine et un *.tsproj ou *.hmiproj à la racine ou jusqu'à deux niveaux en dessous.
    internal static bool EstSolutionTwinCAT(string dossier)
    {
        return Directory.GetFiles(dossier, "*.sln").Length > 0 && ContientProjetTwinCAT(dossier, 2);
    }

    static bool ContientProjetTwinCAT(string dossier, int profondeur)
    {
        if (Directory.GetFiles(dossier, "*.tsproj").Length > 0 || Directory.GetFiles(dossier, "*.hmiproj").Length > 0) return true;
        if (profondeur == 0) return false;
        foreach (string d in Directory.GetDirectories(dossier))
            if (ContientProjetTwinCAT(d, profondeur - 1)) return true;
        return false;
    }

    // Exclusions de repli, reprises du .gitignore TwinCAT3 de GitHub (dossier sans git).
    static readonly HashSet<string> DossiersExclusTwinCAT = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "Packages", ".engineering_servers", "_Boot", "_CompileInfo", "_Libraries",
    };
    static readonly string[] FichiersExclusTwinCAT = { "*.bak", "*.user", "*.~u", "*.tclrs", "debug.log" };

    // Contenu d'une solution TwinCAT : fichiers non ignorés par git (modifications en cours comprises) et dossier
    // .git complet ; sans git, liste d'exclusion fixe. Plus, pour chaque projet PLC, le dernier .compileinfo de son
    // _CompileInfo, qui permet de se connecter à l'automate sans recharger le programme.
    static void ListerTwinCAT(string dossier, List<string> fichiers, List<string> vides)
    {
        string depot = Path.Combine(dossier, ".git");
        List<string> suivis = Directory.Exists(depot) ? ListerGit(dossier) : null;
        if (suivis != null)
        {
            foreach (string f in suivis) fichiers.Add(f);
            Parcourir(depot, null, null, fichiers, vides);
        }
        else Parcourir(dossier, DossiersExclusTwinCAT, FichiersExclusTwinCAT, fichiers, vides);

        // Les .compileinfo que git ne ferait pas ignorer sont remplacés par le seul plus récent.
        fichiers.RemoveAll(f => f.EndsWith(".compileinfo", StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(Path.GetFileName(Path.GetDirectoryName(f)), "_CompileInfo", StringComparison.OrdinalIgnoreCase));
        foreach (string projet in fichiers.FindAll(f => f.EndsWith(".plcproj", StringComparison.OrdinalIgnoreCase)))
        {
            string compileInfo = Path.Combine(Path.GetDirectoryName(projet), "_CompileInfo");
            if (!Directory.Exists(compileInfo)) continue;
            string recent = null;
            DateTime date = DateTime.MinValue;
            foreach (string f in Directory.GetFiles(compileInfo, "*.compileinfo"))
            {
                DateTime d = File.GetLastWriteTimeUtc(f);
                if (recent == null || d > date) { recent = f; date = d; }
            }
            if (recent != null) fichiers.Add(recent);
        }
    }

    // Fichiers suivis (avec leurs modifications) et nouveaux non ignorés, présents sur le disque ;
    // null si git est introuvable ou si la commande échoue (dossier qui n'est pas la racine d'un dépôt…).
    static List<string> ListerGit(string dossier)
    {
        string git = TrouverGit();
        if (git == null) return null;
        var info = new ProcessStartInfo(git, "-C \"" + Normal(dossier) + "\" ls-files --cached --others --exclude-standard -z")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new System.Text.UTF8Encoding(false),
        };
        string sortie;
        using (Process p = Process.Start(info))
        {
            p.ErrorDataReceived += (s, e) => { };
            p.BeginErrorReadLine();
            sortie = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) return null;
        }
        var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fichiers = new List<string>();
        foreach (string relatif in sortie.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string f = Path.Combine(dossier, relatif.Replace('/', '\\'));
            // Une entrée en conflit apparaît plusieurs fois ; un fichier supprimé du disque est écarté.
            if (vus.Add(f) && File.Exists(f)) fichiers.Add(f);
        }
        return fichiers;
    }

    static string TrouverGit()
    {
        foreach (string d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                string exe = Path.Combine(d.Trim().Trim('"'), "git.exe");
                if (d.Trim().Length > 0 && File.Exists(exe)) return exe;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    // Chemin sans le préfixe de forme étendue, pour un programme externe.
    static string Normal(string chemin)
    {
        if (chemin.StartsWith(@"\\?\UNC\")) return @"\\" + chemin.Substring(8);
        if (chemin.StartsWith(@"\\?\")) return chemin.Substring(4);
        return chemin;
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
