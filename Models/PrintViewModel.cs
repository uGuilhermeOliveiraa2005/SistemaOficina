using SistemaOficina.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SistemaOficina.ViewModels
{
    /// <summary>
    /// Agrupa todos os dados necessários para a janela de impressão da Ordem de Serviço.
    /// </summary>
    public class PrintViewModel : INotifyPropertyChanged
    {
        private OrdemDeServico _os;
        private Cliente _cliente;
        private ConfiguracaoEmpresa _configuracao;
        private DateTime _dataImpressao; // Nova propriedade para a data de impressão

        public OrdemDeServico OS
        {
            get { return _os; }
            set
            {
                _os = value;
                OnPropertyChanged();
                // Assumindo que NomeClienteDisplay, EnderecoCompleto e TelefoneCompleto dependem de OS ou Cliente
                OnPropertyChanged(nameof(NomeClienteDisplay));
                OnPropertyChanged(nameof(EnderecoCompleto));
                OnPropertyChanged(nameof(TelefoneCompleto));
            }
        }

        public Cliente Cliente
        {
            get { return _cliente; }
            set
            {
                _cliente = value;
                OnPropertyChanged();
                // Assumindo que NomeClienteDisplay, EnderecoCompleto e TelefoneCompleto dependem de OS ou Cliente
                OnPropertyChanged(nameof(NomeClienteDisplay));
                OnPropertyChanged(nameof(EnderecoCompleto));
                OnPropertyChanged(nameof(TelefoneCompleto));
            }
        }

        public ConfiguracaoEmpresa Configuracao
        {
            get { return _configuracao; }
            set
            {
                _configuracao = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Obtém ou define a data e hora em que a impressão da OS foi solicitada.
        /// </summary>
        public DateTime DataImpressao
        {
            get { return _dataImpressao; }
            set
            {
                _dataImpressao = value;
                OnPropertyChanged();
            }
        }

        public string NomeClienteDisplay
        {
            get
            {
                // Prioriza o nome do cliente cadastrado, depois o nome avulso da OS, caso contrário "Não informado"
                return Cliente?.Nome ?? OS?.NomeClienteOS ?? "Não informado";
            }
        }

        public string EnderecoCompleto
        {
            get
            {
                if (Cliente == null) return "Endereço não cadastrado";

                string endereco = Cliente.Endereco ?? "";
                string bairro = !string.IsNullOrWhiteSpace(Cliente.Bairro) ? $", {Cliente.Bairro}" : "";
                string cidade = !string.IsNullOrWhiteSpace(Cliente.Cidade) ? $" - {Cliente.Cidade}" : "";

                return $"{endereco}{bairro}{cidade}".TrimStart(',', ' ').Trim(); // .Trim() para remover espaços extras no início/fim
            }
        }

        public string TelefoneCompleto
        {
            get
            {
                if (Cliente == null) return "Telefone não cadastrado";

                string telefone1 = Cliente.Telefone1 ?? "";
                string telefone2 = Cliente.Telefone2 ?? "";

                if (!string.IsNullOrWhiteSpace(telefone1) && !string.IsNullOrWhiteSpace(telefone2))
                {
                    return $"{telefone1} / {telefone2}";
                }
                // Retorna apenas um se o outro for nulo/vazio, ou vazio se ambos forem
                return telefone1 + telefone2;
            }
        }

        // Construtor padrão necessário para o XAML, se for instanciado diretamente.
        // No entanto, é mais comum passar os dados no construtor.
        public PrintViewModel()
        {
            OS = new OrdemDeServico();
            Cliente = new Cliente();
            Configuracao = new ConfiguracaoEmpresa();
            DataImpressao = DateTime.Now; // Define a data de impressão ao inicializar, pode ser sobrescrita depois.
        }

        // Construtor que permite passar os dados ao criar o ViewModel
        public PrintViewModel(OrdemDeServico os, Cliente cliente, ConfiguracaoEmpresa configuracao)
        {
            OS = os;
            Cliente = cliente;
            Configuracao = configuracao;
            DataImpressao = DateTime.Now; // Define a data/hora da criação do ViewModel para impressão
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}