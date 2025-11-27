using SistemaOficina.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SistemaOficina
{
    public partial class AdicionarGastosOSWindow : Window
    {
        private readonly OrdemDeServico _ordemDeServico;
        private readonly string _responsavel;
        private readonly ObservableCollection<CompraGasto> _listaGastos = new ObservableCollection<CompraGasto>();
        private readonly string DbPath = Path.Combine(Directory.GetCurrentDirectory(), "OficinaDB.db");

        public AdicionarGastosOSWindow(OrdemDeServico os, string responsavel)
        {
            InitializeComponent();
            _ordemDeServico = os;
            _responsavel = responsavel;

            runNumeroOS.Text = _ordemDeServico.NumeroOS;
            dgGastosAdicionados.ItemsSource = _listaGastos;
        }

        private void Valor_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            string newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !Regex.IsMatch(newText, @"^[0-9]*(,|\.)?[0-9]{0,2}$");
        }

        private void btnAdicionarGasto_Click(object sender, RoutedEventArgs e)
        {
            string descricao = txtDescricaoGasto.Text.Trim();
            if (string.IsNullOrWhiteSpace(descricao))
            {
                MessageBox.Show("A descrição do gasto é obrigatória.", "Campo Vazio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(txtValorGasto.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double valor) || valor <= 0)
            {
                MessageBox.Show("O valor do gasto deve ser um número positivo.", "Valor Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _listaGastos.Add(new CompraGasto
            {
                Descricao = descricao,
                Valor = valor,
                Data = DateTime.Now,
                Responsavel = _responsavel,
                Tipo = "Gasto" // Definindo o tipo como Gasto por padrão para estes lançamentos
            });

            txtDescricaoGasto.Clear();
            txtValorGasto.Clear();
            txtDescricaoGasto.Focus();
            AtualizarTotal();
        }

        private void AtualizarTotal()
        {
            double total = _listaGastos.Sum(g => g.Valor);
            txtTotalGastos.Text = $"Total dos Gastos: {total:C}";
        }

        private void btnConcluir_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show(
                $"Você está prestes a fechar a OS {_ordemDeServico.NumeroOS} e adicionar {_listaGastos.Count} gasto(s), totalizando {_listaGastos.Sum(g => g.Valor):C}.\n\nEsta ação não pode ser desfeita. Deseja continuar?",
                "Confirmar Fechamento com Gastos",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 1. Salva cada gasto e cria a saída no caixa
                        foreach (var gasto in _listaGastos)
                        {
                            var cmdGasto = new SQLiteCommand("INSERT INTO ComprasGastos (Data, Descricao, Tipo, Valor, Responsavel) VALUES (@Data, @Descricao, @Tipo, @Valor, @Responsavel); SELECT last_insert_rowid();", connection);
                            cmdGasto.Parameters.AddWithValue("@Data", gasto.Data.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmdGasto.Parameters.AddWithValue("@Descricao", gasto.Descricao);
                            cmdGasto.Parameters.AddWithValue("@Tipo", gasto.Tipo);
                            cmdGasto.Parameters.AddWithValue("@Valor", gasto.Valor);
                            cmdGasto.Parameters.AddWithValue("@Responsavel", gasto.Responsavel);
                            long gastoId = (long)cmdGasto.ExecuteScalar();

                            var cmdCaixa = new SQLiteCommand("INSERT INTO Caixa (Data, Descricao, Tipo, Valor, CompraGastoId, OrdemServicoId) VALUES (@Data, @Descricao, @Tipo, @Valor, @CompraGastoId, @OrdemServicoId)", connection);
                            cmdCaixa.Parameters.AddWithValue("@Data", gasto.Data.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmdCaixa.Parameters.AddWithValue("@Descricao", $"Gasto OS {_ordemDeServico.NumeroOS}: {gasto.Descricao}");
                            cmdCaixa.Parameters.AddWithValue("@Tipo", "Saida");
                            cmdCaixa.Parameters.AddWithValue("@Valor", gasto.Valor);
                            cmdCaixa.Parameters.AddWithValue("@CompraGastoId", gastoId);
                            cmdCaixa.Parameters.AddWithValue("@OrdemServicoId", _ordemDeServico.Id);
                            cmdCaixa.ExecuteNonQuery();
                        }

                        // 2. Atualiza a OS para 'Fechada'
                        var cmdOS = new SQLiteCommand("UPDATE OrdensDeServico SET Status = 'Fechada', DataFechamento = @DataFechamento WHERE Id = @Id", connection);
                        cmdOS.Parameters.AddWithValue("@DataFechamento", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmdOS.Parameters.AddWithValue("@Id", _ordemDeServico.Id);
                        cmdOS.ExecuteNonQuery();

                        // 3. Lança o valor total da OS como 'Entrada' no caixa (se houver valor)
                        if (_ordemDeServico.Valor > 0)
                        {
                            string caixaDescricao = $"Recebimento OS {_ordemDeServico.NumeroOS} (Cliente: {_ordemDeServico.NomeClienteDisplay})";
                            var commandCaixaEntrada = new SQLiteCommand("INSERT INTO Caixa (Data, Descricao, Tipo, Valor, OrdemServicoId) VALUES (@Data, @Descricao, @Tipo, @Valor, @OrdemServicoId)", connection);
                            commandCaixaEntrada.Parameters.AddWithValue("@Data", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            commandCaixaEntrada.Parameters.AddWithValue("@Descricao", caixaDescricao);
                            commandCaixaEntrada.Parameters.AddWithValue("@Tipo", "Entrada");
                            commandCaixaEntrada.Parameters.AddWithValue("@Valor", _ordemDeServico.Valor);
                            commandCaixaEntrada.Parameters.AddWithValue("@OrdemServicoId", _ordemDeServico.Id);
                            commandCaixaEntrada.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        DialogResult = true; // Sinaliza sucesso para a MainWindow
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro grave ao salvar os dados: {ex.Message}", "Erro de Banco de Dados", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}