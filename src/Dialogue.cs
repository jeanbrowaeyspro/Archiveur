// Dialogue de l'application : titre "Archivage horodaté", icône de l'appli, message et boutons au choix.
// Remplace MessageBox pour tous les messages et questions.
using System;
using System.Drawing;
using System.Windows.Forms;

public class Dialogue : Form
{
    // Point d'injection des essais : reçoit le message et les boutons, rend l'index du bouton choisi (rien n'est affiché).
    internal static Func<string, string[], int> Remplacement = null;

    int choix = -1;

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
        Text = Archiveur.Libelle;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = SystemFonts.MessageBoxFont;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var grille = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        Image image = ImageIcone(icone);
        if (image != null) grille.Controls.Add(new PictureBox { Image = image, Size = image.Size, Margin = new Padding(0, 0, 12, 0) }, 0, 0);
        grille.Controls.Add(new Label { Text = message, AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 6, 0, 18) }, 1, 0);

        // Boutons alignés à droite, dans l'ordre donné ; le premier est le bouton par défaut.
        var ligne = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        for (int i = boutons.Length - 1; i >= 0; i--)
        {
            int index = i;
            var bouton = new Button { Text = boutons[i], AutoSize = true, MinimumSize = new Size(90, 26), Margin = new Padding(8, 0, 0, 0) };
            bouton.Click += (s, e) => { choix = index; Close(); };
            ligne.Controls.Add(bouton);
            if (i == 0) AcceptButton = bouton;
        }
        grille.Controls.Add(ligne, 0, 1);
        grille.SetColumnSpan(ligne, 2);
        Controls.Add(grille);
    }

    static Image ImageIcone(MessageBoxIcon icone)
    {
        switch (icone)
        {
            case MessageBoxIcon.Error: return SystemIcons.Error.ToBitmap();
            case MessageBoxIcon.Warning: return SystemIcons.Warning.ToBitmap();
            case MessageBoxIcon.Question: return SystemIcons.Question.ToBitmap();
            case MessageBoxIcon.Information: return SystemIcons.Information.ToBitmap();
            default: return null;
        }
    }
}
