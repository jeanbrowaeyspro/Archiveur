// Dialogue de l'application : titre "Archivage horodaté", icône de l'appli, message et boutons au choix.
// Remplace MessageBox pour tous les messages et questions. Mise en page façon Windows 11 : contenu aéré,
// barre de boutons en bas sur un fond distinct, boutons de même largeur, le premier en couleur d'accent.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public class Dialogue : FenetreTheme
{
    // Point d'injection des essais : reçoit le message et les boutons, rend l'index du bouton choisi (rien n'est affiché).
    internal static Func<string, string[], int> Remplacement = null;

    int choix = -1;
    readonly List<Button> listeBoutons = new List<Button>();

    // Rend l'index du bouton cliqué, -1 si le dialogue est fermé par la croix ou Échap.
    public static int Afficher(string message, MessageBoxIcon icone, params string[] boutons)
    {
        if (Remplacement != null) return Remplacement(message, boutons);
        using (var dialogue = new Dialogue(message, icone, boutons))
        {
            dialogue.ShowDialog();
            return dialogue.choix;
        }
    }

    internal Dialogue(string message, MessageBoxIcon icone, string[] boutons)
    {
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var contenu = new TableLayoutPanel
        {
            AutoSize = true, ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty,
            Padding = new Padding(Theme.Marge), MinimumSize = new Size(400, 0), BackColor = Theme.Fond,
        };
        if (IconeMessage.Existe(icone))
            contenu.Controls.Add(new IconeMessage(icone) { Margin = new Padding(0, 0, 16, 0), BackColor = Theme.Fond }, 0, 0);
        contenu.Controls.Add(new Label
        {
            Text = message, AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 7, 0, 0),
            BackColor = Theme.Fond, ForeColor = Theme.Texte,
        }, 1, 0);

        // Boutons alignés à droite (colonne 0 extensible), dans l'ordre donné ; le premier est le bouton par défaut.
        var pied = new TableLayoutPanel
        {
            AutoSize = true, ColumnCount = boutons.Length + 1, RowCount = 1, Dock = DockStyle.Fill,
            Margin = Padding.Empty, Padding = new Padding(Theme.Marge, Theme.MargePied, Theme.Marge, Theme.MargePied),
            BackColor = Theme.FondPied,
        };
        pied.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < boutons.Length; i++)
        {
            int index = i;
            var bouton = new BoutonTheme(boutons[i], i == 0) { Margin = new Padding(i == 0 ? 0 : Theme.EcartBoutons, 0, 0, 0) };
            bouton.Click += (s, e) => { choix = index; Close(); };
            pied.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pied.Controls.Add(bouton, i + 1, 0);
            listeBoutons.Add(bouton);
            if (i == 0) AcceptButton = bouton;
        }

        var racine = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty };
        racine.Controls.Add(contenu, 0, 0);
        racine.Controls.Add(pied, 0, 1);
        Controls.Add(racine);
    }

    // Après la mise à l'échelle DPI : tous les boutons à la largeur du plus large, focus sur le bouton par défaut.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        int largeur = 0;
        foreach (Button b in listeBoutons) largeur = Math.Max(largeur, Math.Max(b.Width, b.PreferredSize.Width));
        foreach (Button b in listeBoutons) b.MinimumSize = new Size(largeur, b.MinimumSize.Height);
        if (listeBoutons.Count > 0) ActiveControl = listeBoutons[0];
    }
}
