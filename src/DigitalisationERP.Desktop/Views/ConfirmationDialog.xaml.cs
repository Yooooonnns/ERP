using System.Windows;

namespace DigitalisationERP.Desktop.Views
{
    /// <summary>
    /// Dialogue de confirmation pour les actions critiques (Safety Feature)
    /// Utilisé pour les commandes robots vers zones dangereuses, arrêts d'urgence, etc.
    /// </summary>
    public partial class ConfirmationDialog : Window
    {
        public bool IsConfirmed { get; private set; }

        public ConfirmationDialog(string title, string message, string details)
        {
            InitializeComponent();
            
            TitleText.Text = title;
            MessageText.Text = message;
            DetailsText.Text = details;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        /// <summary>
        /// Affiche un dialogue de confirmation et retourne le résultat
        /// </summary>
        public static bool Show(string title, string message, string details, Window? owner = null)
        {
            var dialog = new ConfirmationDialog(title, message, details);
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            dialog.ShowDialog();
            return dialog.IsConfirmed;
        }
    }
}
