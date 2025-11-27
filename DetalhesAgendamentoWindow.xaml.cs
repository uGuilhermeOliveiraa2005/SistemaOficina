using SistemaOficina.Models;
using System.Linq;
using System.Windows;

namespace SistemaOficina
{
    public partial class DetalhesAgendamentoWindow : Window
    {
        public DetalhesAgendamentoWindow(Agendamento agendamento, Cliente? cliente)
        {
            InitializeComponent();
            this.DataContext = agendamento;

            if (agendamento.ClienteId.HasValue && cliente != null)
            {
                txtTipoCliente.Text = "Cliente Cadastrado";
                txtTelefoneCliente.Text = !string.IsNullOrWhiteSpace(cliente.Telefone1) ? cliente.Telefone1 : "Não informado";

                if (string.IsNullOrWhiteSpace(agendamento.Endereco))
                {
                    txtEnderecoCompleto.Text = !string.IsNullOrWhiteSpace(cliente.Endereco) ? cliente.Endereco : "Não informado";
                    txtBairro.Text = !string.IsNullOrWhiteSpace(cliente.Bairro) ? cliente.Bairro : "Não informado";
                    txtCidade.Text = !string.IsNullOrWhiteSpace(cliente.Cidade) ? cliente.Cidade : "Não informado";
                    txtNumero.Text = "(informação no endereço)";
                    txtUf.Text = "Não informado";
                    txtCep.Text = "Não informado";
                }
                else
                {
                    PreencherEnderecoAgendamento(agendamento);
                }
            }
            else
            {
                txtTipoCliente.Text = "Cliente Avulso";
                PreencherEnderecoAgendamento(agendamento);
            }
        }

        private void PreencherEnderecoAgendamento(Agendamento agendamento)
        {
            txtEnderecoCompleto.Text = !string.IsNullOrWhiteSpace(agendamento.Endereco) ? agendamento.Endereco : "Não informado";
            txtNumero.Text = !string.IsNullOrWhiteSpace(agendamento.Numero) ? agendamento.Numero : "Não informado";
            txtBairro.Text = !string.IsNullOrWhiteSpace(agendamento.Bairro) ? agendamento.Bairro : "Não informado";
            txtCidade.Text = !string.IsNullOrWhiteSpace(agendamento.Cidade) ? agendamento.Cidade : "Não informado";
            txtUf.Text = !string.IsNullOrWhiteSpace(agendamento.UF) ? agendamento.UF : "Não informado";
            txtCep.Text = !string.IsNullOrWhiteSpace(agendamento.CEP) ? agendamento.CEP : "Não informado";
        }

        private void Fechar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}