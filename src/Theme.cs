// Apparence commune des fenêtres, façon Windows 11 : thème clair / sombre des applications Windows,
// police Segoe UI, icône de l'appli, barre de titre sombre et coins arrondis (DWM), prise en charge DPI.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

static class Theme
{
    // Thème des applications Windows (Paramètres > Personnalisation > Couleurs) ; redirigeable par les essais.
    internal static bool Sombre = LireSombre();

    // Mise en page commune (pixels à 96 ppp, mis à l'échelle par AutoScaleMode.Dpi).
    internal const int Marge = 24;
    internal const int MargePied = 20;
    internal const int HauteurBouton = 32;
    internal const int LargeurBouton = 100;
    internal const int EcartBoutons = 8;

    internal static readonly Font Police = new Font("Segoe UI", 9f);

    // Couleurs : fond et texte toujours fixés ensemble.
    internal static Color Fond { get { return Sombre ? Rvb(0x2B2B2B) : Rvb(0xFFFFFF); } }
    internal static Color FondPied { get { return Sombre ? Rvb(0x202020) : Rvb(0xF3F3F3); } }
    internal static Color Texte { get { return Sombre ? Rvb(0xFFFFFF) : Rvb(0x1B1B1B); } }
    internal static Color TexteSecondaire { get { return Sombre ? Rvb(0xC8C8C8) : Rvb(0x5D5D5D); } }
    internal static Color FondBouton { get { return Sombre ? Rvb(0x373737) : Rvb(0xFDFDFD); } }
    internal static Color FondBoutonSurvol { get { return Sombre ? Rvb(0x3E3E3E) : Rvb(0xF5F5F5); } }
    internal static Color FondBoutonAppui { get { return Sombre ? Rvb(0x333333) : Rvb(0xEDEDED); } }
    internal static Color BordureBouton { get { return Sombre ? Rvb(0x4A4A4A) : Rvb(0xD1D1D1); } }
    internal static Color Accent { get { return Sombre ? Rvb(0x60CDFF) : Rvb(0x005FB8); } }
    internal static Color AccentSurvol { get { return Sombre ? Rvb(0x5ABCEB) : Rvb(0x1A6FC0); } }
    internal static Color AccentAppui { get { return Sombre ? Rvb(0x55ACD6) : Rvb(0x3381C9); } }
    internal static Color TexteAccent { get { return Sombre ? Rvb(0x000000) : Rvb(0xFFFFFF); } }
    internal static Color PisteProgression { get { return Sombre ? Rvb(0x4A4A4A) : Rvb(0xDDDDDD); } }

    static Color Rvb(int rvb) { return Color.FromArgb(255, Color.FromArgb(rvb)); }

    static bool LireSombre()
    {
        try
        {
            using (RegistryKey cle = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object valeur = cle == null ? null : cle.GetValue("AppsUseLightTheme");
                return valeur is int && (int)valeur == 0;
            }
        }
        catch { return false; }
    }

    // Icône de l'appli, chargée une seule fois : toutes les tailles de l'.ico intégré (titre net en petit,
    // barre des tâches en grand), à défaut l'icône de l'exe.
    static Icon icone;
    internal static Icon Icone
    {
        get
        {
            if (icone == null)
            {
                using (var flux = Assembly.GetExecutingAssembly().GetManifestResourceStream("Archiveur.ico"))
                    icone = flux != null ? new Icon(flux) : Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            return icone;
        }
    }

    // À appeler avant toute fenêtre : écran par écran (PerMonitorV2), à défaut DPI du système, pour éviter le flou.
    internal static void InitialiserDpi()
    {
        try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; }
        catch (EntryPointNotFoundException) { }
        try { SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
    }

    // Barre de titre sombre ou claire, coins arrondis (Windows 11 ; sans effet ailleurs).
    internal static void AppliquerCadre(IntPtr fenetre)
    {
        try
        {
            int sombre = Sombre ? 1 : 0;
            // 20 depuis Windows 10 20H1, 19 sur les versions précédentes.
            if (DwmSetWindowAttribute(fenetre, 20, ref sombre, 4) != 0) DwmSetWindowAttribute(fenetre, 19, ref sombre, 4);
            int arrondi = 2;
            DwmSetWindowAttribute(fenetre, 33, ref arrondi, 4);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    // Rectangle aux coins arrondis.
    internal static GraphicsPath Arrondi(RectangleF r, float rayon)
    {
        var chemin = new GraphicsPath();
        float d = Math.Min(rayon * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { chemin.AddRectangle(r); return chemin; }
        chemin.AddArc(r.X, r.Y, d, d, 180, 90);
        chemin.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        chemin.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        chemin.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        chemin.CloseFigure();
        return chemin;
    }

    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr contexte);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr fenetre, int attribut, ref int valeur, int taille);
}

// Base des fenêtres de l'appli : titre, icône, police, couleurs, cadre DWM, changement de DPI d'un écran à l'autre.
public class FenetreTheme : Form
{
    int dpi;

    public FenetreTheme()
    {
        Text = Archiveur.Libelle;
        Icon = Theme.Icone;
        // FixedSingle et non FixedDialog : FixedDialog masque l'icône de la barre de titre.
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = Theme.Police;
        BackColor = Theme.Fond;
        ForeColor = Theme.Texte;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.AppliquerCadre(Handle);
        // La mise en page suit le DPI du système ; la fenêtre peut naître sur un écran à un autre DPI.
        dpi = (int)CurrentAutoScaleDimensions.Width;
        try
        {
            int dpiFenetre = (int)GetDpiForWindow(Handle);
            if (dpiFenetre > 0 && dpiFenetre != dpi)
            {
                Redimensionner(dpiFenetre);
                if (StartPosition == FormStartPosition.CenterScreen) CenterToScreen();
            }
        }
        catch (EntryPointNotFoundException) { }
    }

    const int WM_DPICHANGED = 0x02E0;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_DPICHANGED && dpi > 0)
        {
            Redimensionner(m.WParam.ToInt32() & 0xFFFF);
            var rect = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
            Location = new Point(rect.Left, rect.Top);
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    void Redimensionner(int nouveauDpi)
    {
        float facteur = nouveauDpi / (float)dpi;
        dpi = nouveauDpi;
        Font = new Font(Font.FontFamily, Font.Size * facteur);
        Scale(new SizeF(facteur, facteur));
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr fenetre);
}

// Bouton plat façon Windows 11 : bordure discrète, bouton principal aux couleurs d'accent.
public class BoutonTheme : Button
{
    public BoutonTheme(string texte, bool principal)
    {
        Text = texte;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        AutoSize = true;
        MinimumSize = new Size(Theme.LargeurBouton, Theme.HauteurBouton);
        Padding = new Padding(8, 0, 8, 0);
        FlatAppearance.BorderSize = 1;
        BackColor = principal ? Theme.Accent : Theme.FondBouton;
        ForeColor = principal ? Theme.TexteAccent : Theme.Texte;
        FlatAppearance.BorderColor = principal ? Theme.Accent : Theme.BordureBouton;
        FlatAppearance.MouseOverBackColor = principal ? Theme.AccentSurvol : Theme.FondBoutonSurvol;
        FlatAppearance.MouseDownBackColor = principal ? Theme.AccentAppui : Theme.FondBoutonAppui;
    }

    // Le bouton par défaut garde Entrée (AcceptButton) sans le cadre noir épais du style plat.
    public override void NotifyDefault(bool value)
    {
        base.NotifyDefault(false);
    }
}

// Barre de progression fine aux couleurs du thème (la barre système reste claire en thème sombre).
public class BarreProgression : Control
{
    int valeur;

    public BarreProgression()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.Selectable, false);
    }

    // De 0 à 100.
    public int Valeur
    {
        get { return valeur; }
        set
        {
            value = Math.Max(0, Math.Min(100, value));
            if (value != valeur) { valeur = value; Invalidate(); }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var piste = new RectangleF(0, 0, Width - 1, Height - 1);
        float rayon = piste.Height / 2;
        using (var chemin = Theme.Arrondi(piste, rayon))
        using (var pinceau = new SolidBrush(Theme.PisteProgression))
            e.Graphics.FillPath(pinceau, chemin);
        if (valeur > 0)
        {
            var plein = new RectangleF(0, 0, Math.Max(piste.Height, piste.Width * valeur / 100f), piste.Height);
            using (var chemin = Theme.Arrondi(plein, rayon))
            using (var pinceau = new SolidBrush(Theme.Accent))
                e.Graphics.FillPath(pinceau, chemin);
        }
    }
}

// Icône de message dessinée (pastille colorée et symbole), nette à tout DPI et adaptée au thème.
public class IconeMessage : Control
{
    readonly MessageBoxIcon type;

    public IconeMessage(MessageBoxIcon type)
    {
        this.type = type;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.Selectable, false);
        Size = new Size(32, 32);
    }

    // Valeurs de MessageBoxIcon : Error = Hand = Stop, Warning = Exclamation, Information = Asterisk.
    internal static bool Existe(MessageBoxIcon type)
    {
        return type == MessageBoxIcon.Error || type == MessageBoxIcon.Warning || type == MessageBoxIcon.Question || type == MessageBoxIcon.Information;
    }

    Color Couleur
    {
        get
        {
            if (type == MessageBoxIcon.Error) return Theme.Sombre ? Color.FromArgb(0xFF, 0x99, 0xA4) : Color.FromArgb(0xC4, 0x2B, 0x1C);
            if (type == MessageBoxIcon.Warning) return Theme.Sombre ? Color.FromArgb(0xFC, 0xE1, 0x00) : Color.FromArgb(0x9D, 0x5D, 0x00);
            return Theme.Accent;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float cote = Math.Min(Width, Height) - 1;
        using (var pinceau = new SolidBrush(Couleur)) g.FillEllipse(pinceau, 0, 0, cote, cote);

        Color symbole = Theme.Sombre ? Color.Black : Color.White;
        float c = cote / 2;
        if (type == MessageBoxIcon.Error)
        {
            float b = cote * 0.17f;
            using (var crayon = new Pen(symbole, cote * 0.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(crayon, c - b, c - b, c + b, c + b);
                g.DrawLine(crayon, c + b, c - b, c - b, c + b);
            }
            return;
        }
        string texte = type == MessageBoxIcon.Warning ? "!" : type == MessageBoxIcon.Question ? "?" : "i";
        using (var chemin = new GraphicsPath())
        using (var format = new StringFormat(StringFormat.GenericTypographic))
        {
            chemin.AddString(texte, Theme.Police.FontFamily, (int)FontStyle.Bold, cote * 0.62f, PointF.Empty, format);
            RectangleF bornes = chemin.GetBounds();
            using (var m = new Matrix())
            {
                m.Translate(c - bornes.X - bornes.Width / 2, c - bornes.Y - bornes.Height / 2);
                chemin.Transform(m);
            }
            using (var pinceau = new SolidBrush(symbole)) g.FillPath(pinceau, chemin);
        }
    }
}
