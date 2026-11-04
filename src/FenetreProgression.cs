// Fenêtre de progression de l'archivage : barre, octets traités, fichier en cours, bouton Annuler,
// pourcentage dans le titre et sur le bouton de la barre des tâches. Se ferme seule à la fin du travail.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// État partagé entre le thread d'archivage et la fenêtre.
public class Progression
{
    public long Total;
    public long Fait;
    public volatile string Fichier = "";
    public volatile string Dossier = "";
    public volatile int Numero, Nombre;
    public volatile bool Annule;

    public void Demarrer(string dossier, int numero, int nombre)
    {
        Dossier = dossier; Numero = numero; Nombre = nombre; Fichier = "";
        Interlocked.Exchange(ref Total, 0);
        Interlocked.Exchange(ref Fait, 0);
    }

    public int Pourcentage
    {
        get
        {
            long total = Interlocked.Read(ref Total);
            return total <= 0 ? 0 : (int)Math.Min(100, Interlocked.Read(ref Fait) * 100 / total);
        }
    }
}

public class FenetreProgression : FenetreTheme
{
    readonly Progression progression;
    readonly Thread travail;
    readonly Label lblDossier = new Label();
    readonly Label lblDetail = new Label();
    readonly BarreProgression barre = new BarreProgression();
    readonly Button btnAnnuler = new BoutonTheme("Annuler", false);
    readonly System.Windows.Forms.Timer minuterie = new System.Windows.Forms.Timer();
    ITaskbarList3 barreDesTaches;

    public FenetreProgression(Progression progression, Thread travail)
    {
        this.progression = progression;
        this.travail = travail;

        MinimizeBox = true;
        const int largeur = 480, contenu = 92, pied = Theme.MargePied * 2 + Theme.HauteurBouton;
        const int interieur = largeur - 2 * Theme.Marge;
        ClientSize = new Size(largeur, contenu + pied);

        lblDossier.SetBounds(Theme.Marge, 20, interieur, 20);
        lblDossier.AutoEllipsis = true;
        lblDossier.BackColor = Theme.Fond;
        lblDossier.ForeColor = Theme.Texte;
        barre.SetBounds(Theme.Marge, 48, interieur, 6);
        barre.BackColor = Theme.Fond;
        lblDetail.SetBounds(Theme.Marge, 62, interieur, 20);
        lblDetail.AutoEllipsis = true;
        lblDetail.BackColor = Theme.Fond;
        lblDetail.ForeColor = Theme.TexteSecondaire;

        // Barre de boutons en bas, sur un fond distinct comme le dialogue.
        var barreBoutons = new Panel { Dock = DockStyle.Bottom, Height = pied, BackColor = Theme.FondPied };
        btnAnnuler.AutoSize = false;
        btnAnnuler.SetBounds(largeur - Theme.Marge - Theme.LargeurBouton, Theme.MargePied, Theme.LargeurBouton, Theme.HauteurBouton);
        btnAnnuler.Click += (s, e) => Annuler();
        barreBoutons.Controls.Add(btnAnnuler);
        CancelButton = btnAnnuler;
        Controls.AddRange(new Control[] { lblDossier, barre, lblDetail, barreBoutons });

        minuterie.Interval = 100;
        minuterie.Tick += (s, e) => Rafraichir();
        Rafraichir();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try { barreDesTaches = (ITaskbarList3)new TaskbarList(); barreDesTaches.HrInit(); }
        catch { barreDesTaches = null; }
        minuterie.Start();
        Rafraichir();
    }

    // Fermeture par la croix = annulation ; la fenêtre se ferme quand le thread s'est arrêté.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (travail.IsAlive) { e.Cancel = true; Annuler(); }
        base.OnFormClosing(e);
    }

    void Annuler()
    {
        progression.Annule = true;
        btnAnnuler.Enabled = false;
        lblDetail.Text = "Annulation…";
    }

    public void Rafraichir()
    {
        if (!travail.IsAlive && IsHandleCreated) { minuterie.Stop(); Close(); return; }

        int pct = progression.Pourcentage;
        string nom = System.IO.Path.GetFileName(progression.Dossier);
        Text = pct + " % - " + Archiveur.Libelle;
        lblDossier.Text = "Archivage de " + nom + (progression.Nombre > 1 ? "  (" + progression.Numero + "/" + progression.Nombre + ")" : "");
        barre.Valeur = pct;
        if (!progression.Annule)
            lblDetail.Text = Taille(Interlocked.Read(ref progression.Fait)) + " / " + Taille(Interlocked.Read(ref progression.Total)) + "   " + progression.Fichier;

        if (barreDesTaches != null && IsHandleCreated)
        {
            try
            {
                barreDesTaches.SetProgressState(Handle, progression.Annule ? TBPF.PAUSED : TBPF.NORMAL);
                barreDesTaches.SetProgressValue(Handle, (ulong)pct, 100);
            }
            catch { barreDesTaches = null; }
        }
    }

    static string Taille(long octets)
    {
        if (octets >= 1L << 30) return (octets / (double)(1L << 30)).ToString("0.00") + " Go";
        if (octets >= 1L << 20) return (octets / (double)(1L << 20)).ToString("0.0") + " Mo";
        return (octets / 1024.0).ToString("0") + " Ko";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) minuterie.Dispose();
        base.Dispose(disposing);
    }

    // Progression sur le bouton de la barre des tâches (ITaskbarList3, Windows 7 et plus).
    enum TBPF { NOPROGRESS = 0, INDETERMINATE = 1, NORMAL = 2, ERROR = 4, PAUSED = 8 }

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
    class TaskbarList { }

    [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        // ITaskbarList3 (seules les deux premières méthodes servent)
        void SetProgressValue(IntPtr hwnd, ulong fait, ulong total);
        void SetProgressState(IntPtr hwnd, TBPF etat);
    }
}
