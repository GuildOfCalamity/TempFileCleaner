using System;
using System.Windows;

namespace TempFileCleaner
{
    /// <summary>
    /// Better than <see cref="System.Windows.MessageBox.Show(string)"/>
    /// </summary>
    public partial class WpfMessageBox : Window
    {
        public WpfMessageBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// TODO: Change this to use an enum for the image instead of bool <paramref name="isError"/>.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="showCancel"></param>
        /// <param name="isError"></param>
        /// <param name="fontSize"></param>
        public WpfMessageBox(string message, bool showCancel, bool isError, double fontSize = 16.0) : this()
        {
            MessageText.Text = message;
            MessageText.FontSize = fontSize;
            btnCancel.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
            imgOk.Visibility = isError ? Visibility.Collapsed : Visibility.Visible;
            imgErr.Visibility = isError ? Visibility.Visible : Visibility.Collapsed;
        }


        public static bool? Show(string message, bool showCancel = false, bool isError = false, double fontSize = 16.0, Window owner = null)
        {
            var msg = new WpfMessageBox(message, showCancel, isError, fontSize);
            if (owner != null && owner.Dispatcher != null)
                msg.Owner = owner;

            return msg.ShowDialog();
        }

        void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
