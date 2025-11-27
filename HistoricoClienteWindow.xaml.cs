using SistemaOficina.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SistemaOficina
{
    /// <summary>
    /// Lógica interna para HistoricoClienteWindow.xaml
    /// </summary>
    public partial class HistoricoClienteWindow : Window
    {
        private readonly int _clienteId;
        private readonly ConfiguracaoEmpresa _configuracao;
        private readonly string DbPath = Path.Combine(Directory.GetCurrentDirectory(), "OficinaDB.db");

        public HistoricoClienteWindow(int clienteId, ConfiguracaoEmpresa configuracao)
        {
            InitializeComponent();
            _clienteId = clienteId;
            _configuracao = configuracao;
            CarregarDadosCliente();
        }

        private void CarregarDadosCliente()
        {
            Cliente? cliente = null;
            List<OrdemDeServico> todasAsOrdens = new List<OrdemDeServico>();

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();

                    // 1. Carregar dados do cliente
                    var cmdCliente = new SQLiteCommand("SELECT * FROM Clientes WHERE Id = @Id", connection);
                    cmdCliente.Parameters.AddWithValue("@Id", _clienteId);
                    using (var reader = cmdCliente.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cliente = new Cliente
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                Nome = reader["Nome"] as string,
                                Endereco = reader["Endereco"] as string,
                                Cidade = reader["Cidade"] as string,
                                Bairro = reader["Bairro"] as string,
                                Telefone1 = reader["Telefone1"] as string
                            };
                        }
                    }

                    // 2. Carregar todas as ordens de serviço do cliente (com todos os campos)
                    var cmdOrdens = new SQLiteCommand("SELECT * FROM OrdensDeServico WHERE ClienteId = @ClienteId ORDER BY DataAbertura DESC", connection);
                    cmdOrdens.Parameters.AddWithValue("@ClienteId", _clienteId);
                    using (var reader = cmdOrdens.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            todasAsOrdens.Add(new OrdemDeServico
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                NumeroOS = reader["NumeroOS"].ToString(),
                                ClienteId = reader["ClienteId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ClienteId"]),
                                MarcaEquipamento = reader["MarcaEquipamento"].ToString(),
                                ModeloEquipamento = reader["ModeloEquipamento"].ToString(),
                                Aparelho = reader["Aparelho"].ToString(),
                                Valor = reader.IsDBNull(reader.GetOrdinal("Valor")) ? 0 : Convert.ToDouble(reader["Valor"]),
                                Status = reader["Status"].ToString(),
                                DataAbertura = DateTime.Parse(reader["DataAbertura"].ToString()),
                                DataFechamento = reader["DataFechamento"] == DBNull.Value ? (DateTime?)null : DateTime.Parse(reader["DataFechamento"].ToString()),
                                NomeClienteOS = reader["NomeClienteOS"].ToString(),
                                NumeroSerie = reader["NumeroSerie"].ToString(),
                                DefeitoReclamado = reader["DefeitoReclamado"].ToString(),
                                DefeitoReal = reader["DefeitoReal"].ToString(),
                                ServicoExecutado = reader["ServicoExecutado"].ToString(),
                                TecnicoResponsavel = reader["TecnicoResponsavel"].ToString(),
                                Garantia = reader["Garantia"].ToString(),
                                Voltagem = reader["Voltagem"].ToString(),
                                Cor = reader["Cor"].ToString(),
                                Departamento = reader["Departamento"].ToString(),
                                Complemento = reader["Complemento"].ToString(),
                                Observacao = reader["Observacao"].ToString(),
                            });
                        }
                    }
                }

                // 3. Preencher os TextBlocks do cabeçalho
                if (cliente != null)
                {
                    runNomeClienteTitulo.Text = cliente.Nome;
                    txtTelefone.Text = cliente.Telefone1;
                    txtEndereco.Text = $"{cliente.Endereco}, {cliente.Bairro} - {cliente.Cidade}";
                }

                // 4. Separar as ordens e atribuir aos DataGrids (com a lógica correta)
                dgOrdensAbertas.ItemsSource = todasAsOrdens.Where(o => o.Status != "Fechada").ToList();
                dgOrdensConcluidas.ItemsSource = todasAsOrdens.Where(o => o.Status == "Fechada").ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro ao carregar o histórico do cliente: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnImprimirOrdem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is OrdemDeServico selectedOS)
            {
                // Reutilizando a janela de detalhes que já existe no sistema para impressão
                var detailsWindow = new DetalhesOSWindow(selectedOS, null, _configuracao);
                detailsWindow.Owner = this;
                detailsWindow.ShowDialog();
            }
        }
    }
}