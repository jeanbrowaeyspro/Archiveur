// Mise à jour depuis la dernière release GitHub : vérification en arrière-plan pendant l'archivage,
// proposition à la fin, remplacement de l'exe installé.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Contenu de %APPDATA%\Archiveur\settings.json.
public class Reglages
{
    public string DerniereVersion { get; set; }
    public string UrlTelechargement { get; set; }
    public string VersionIgnoree { get; set; }
}

static class MiseAJour
{
    // Redirigeables par les essais.
    internal static string UrlDerniereVersion = "https://api.github.com/repos/jeanbrowaeyspro/Archiveur/releases/latest";
    internal static string FichierReglages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Archiveur\settings.json");

    static readonly object verrou = new object();
    static readonly Version VersionCourante = Assembly.GetExecutingAssembly().GetName().Version;

    // Interroge GitHub dans un thread d'arrière-plan : l'archivage n'est pas ralenti et la fin du processus
    // n'attend pas la réponse. Toute erreur (hors ligne, proxy, quota) est ignorée.
    public static void LancerVerification()
    {
        new Thread(Verifier) { IsBackground = true }.Start();
    }

    internal static void Verifier()
    {
        try
        {
            var reponse = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(
                Encoding.UTF8.GetString(Telecharger(UrlDerniereVersion, 5000)));
            foreach (Dictionary<string, object> piece in (object[])reponse["assets"])
                if ((string)piece["name"] == "Archiveur.exe")
                    ModifierReglages(r =>
                    {
                        r.DerniereVersion = (string)reponse["tag_name"];
                        r.UrlTelechargement = (string)piece["browser_download_url"];
                    });
        }
        catch { }
    }

    // À la fin de l'archivage : propose la dernière version connue si elle est plus récente et pas ignorée.
    public static void Proposer()
    {
        if (!File.Exists(Archiveur.ExeInstalle)) return;
        Reglages reglages;
        lock (verrou) reglages = LireReglages();
        Version derniere = VersionDe(reglages.DerniereVersion);
        if (derniere == null || derniere <= VersionCourante || reglages.DerniereVersion == reglages.VersionIgnoree) return;

        int choix = Dialogue.Afficher("Une nouvelle version d'Archiveur est disponible.\n\n" +
                                      "Version installée : " + VersionCourante.ToString(3) + "\n" +
                                      "Nouvelle version : " + derniere.ToString(3),
                                      MessageBoxIcon.Information, "Mettre à jour", "Plus tard", "Ignorer cette version");
        if (choix == 0) Installer(reglages.UrlTelechargement, derniere);
        else if (choix == 2) ModifierReglages(r => r.VersionIgnoree = reglages.DerniereVersion);
    }

    // Télécharge Archiveur.exe.new, le vérifie, puis échange les noms (l'exe en cours d'exécution peut être renommé).
    // Le .old est supprimé au démarrage suivant ; le registre ne change pas.
    static void Installer(string url, Version attendue)
    {
        string nouveau = Archiveur.ExeInstalle + ".new";
        string ancien = Archiveur.ExeInstalle + ".old";
        try
        {
            File.WriteAllBytes(nouveau, Telecharger(url, 60000));
            AssemblyName nom = AssemblyName.GetAssemblyName(nouveau);
            if (nom.Name != "Archiveur" || VersionDe(nom.Version.ToString()) != attendue)
                throw new InvalidDataException("Le fichier téléchargé n'est pas Archiveur " + attendue.ToString(3) + ".");
            if (File.Exists(ancien)) File.Delete(ancien);
            File.Move(Archiveur.ExeInstalle, ancien);
            try { File.Move(nouveau, Archiveur.ExeInstalle); }
            catch
            {
                File.Move(ancien, Archiveur.ExeInstalle);
                throw;
            }
            Archiveur.Message("Archiveur est passé en version " + attendue.ToString(3) + ".", MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            try { File.Delete(nouveau); } catch { }
            Archiveur.Message("Échec de la mise à jour :\n\n" + ex.Message, MessageBoxIcon.Error);
        }
    }

    public static void SupprimerAncienExe()
    {
        try { File.Delete(Archiveur.ExeInstalle + ".old"); } catch { }
    }

    static byte[] Telecharger(string url, int delai)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        WebRequest requete = WebRequest.Create(url);
        requete.Timeout = delai;
        var http = requete as HttpWebRequest;
        if (http != null)
        {
            http.UserAgent = "Archiveur/" + VersionCourante.ToString(3);
            http.ReadWriteTimeout = delai;
        }
        using (WebResponse reponse = requete.GetResponse())
        using (var memoire = new MemoryStream())
        {
            reponse.GetResponseStream().CopyTo(memoire);
            return memoire.ToArray();
        }
    }

    // "v1.2.0" ou "1.2.0.0" -> 1.2.0.0 (composants absents à 0, pour comparer) ; null si illisible.
    static Version VersionDe(string texte)
    {
        Version v;
        if (texte == null || !Version.TryParse(texte.TrimStart('v'), out v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }

    // Lecture, modification et écriture atomique (fichier temporaire puis remplacement), sous verrou :
    // le thread de vérification et la réponse au dialogue peuvent écrire en même temps. Échec ignoré.
    static void ModifierReglages(Action<Reglages> modification)
    {
        lock (verrou)
        {
            Reglages reglages = LireReglages();
            modification(reglages);
            string temporaire = FichierReglages + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FichierReglages));
                File.WriteAllText(temporaire, new JavaScriptSerializer().Serialize(reglages));
                if (File.Exists(FichierReglages)) File.Replace(temporaire, FichierReglages, null);
                else File.Move(temporaire, FichierReglages);
            }
            catch { try { File.Delete(temporaire); } catch { } }
        }
    }

    static Reglages LireReglages()
    {
        try { return new JavaScriptSerializer().Deserialize<Reglages>(File.ReadAllText(FichierReglages)) ?? new Reglages(); }
        catch { return new Reglages(); }
    }
}
