using SistemaOficina.Models;
using SistemaOficina.ViewModels;
using System.Windows;

namespace SistemaOficina
{
    public partial class DetalhesOSWindow : Window
    {
        // Campos para armazenar as informações extras necessárias para a impressão
        private readonly Cliente? _cliente;
        private readonly ConfiguracaoEmpresa? _configuracao;

        // Construtor atualizado para receber os dados do cliente e da configuração
        public DetalhesOSWindow(OrdemDeServico os, Cliente? cliente, ConfiguracaoEmpresa? config)
        {
            InitializeComponent();

            // Armazena as informações recebidas
            _cliente = cliente;
            _configuracao = config;

            // Define o objeto OrdemDeServico como o contexto de dados da janela
            this.DataContext = os;
        }

        private void Fechar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Evento de clique para o novo botão de visualização de impressão
        private void btnImprimirPreview_Click(object sender, RoutedEventArgs e)
        {
            // Garante que os dados necessários existem antes de tentar imprimir
            if (this.DataContext is OrdemDeServico os && _configuracao != null)
            {
                // Cria o ViewModel para a impressão, usando os dados armazenados
                var viewModel = new PrintViewModel
                {
                    OS = os,
                    Cliente = _cliente, // O _cliente pode ser nulo (para clientes avulsos)
                    Configuracao = _configuracao
                };

                // Cria e exibe a janela de impressão/preview
                var printWindow = new OrdemServicoPrintView(viewModel);
                printWindow.Owner = this;
                printWindow.Show();
            }
            else
            {
                MessageBox.Show("Não foi possível gerar a visualização. Faltam dados de configuração da empresa.",
                                "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}