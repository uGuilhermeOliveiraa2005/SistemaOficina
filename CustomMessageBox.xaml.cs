using System.Windows;

namespace SistemaOficina
{
    public partial class CustomMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        public CustomMessageBox(string message, string caption, MessageBoxButton buttons)
        {
            InitializeComponent();

            // Define o título da barra da janela (bom para acessibilidade)
            this.Title = caption;

            // Correção: Usa o nome correto do TextBlock (TitleTextBlock) definido no XAML.
            TitleTextBlock.Text = caption;

            MessageText.Text = message;

            if (buttons == MessageBoxButton.OK)
            {
                btnCancel.Visibility = Visibility.Collapsed;
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            // Define o resultado como OK (ou Yes, se preferir) e fecha.
            Result = MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // O resultado padrão já é Cancel, então apenas fechamos.
            DialogResult = false;
            Close();
        }
    }
}
