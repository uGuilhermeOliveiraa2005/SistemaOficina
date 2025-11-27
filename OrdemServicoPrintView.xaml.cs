using SistemaOficina.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SistemaOficina
{
    public partial class OrdemServicoPrintView : Window
    {
        public OrdemServicoPrintView(PrintViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        // Lógica para o botão de Imprimir
        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Esconde o painel de botões para não aparecer na impressão
                ButtonPanel.Visibility = Visibility.Collapsed;

                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Imprime apenas a área do documento (o Grid "PrintableArea")
                    printDialog.PrintVisual(PrintableArea, $"Ordem de Serviço - {((PrintViewModel)DataContext).OS.NumeroOS}");
                }
            }
            finally
            {
                // Garante que o painel de botões reapareça, mesmo se a impressão for cancelada
                ButtonPanel.Visibility = Visibility.Visible;
            }
        }

        // Lógica para o botão Fechar
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}