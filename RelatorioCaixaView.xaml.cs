using SistemaOficina.ViewModels;
using System.Windows;

namespace SistemaOficina
{
    public partial class RelatorioCaixaView : Window
    {
        public RelatorioCaixaView(RelatorioCaixaViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}