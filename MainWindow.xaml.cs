using Microsoft.Win32;
using SistemaOficina.Models;
using SistemaOficina.ViewModels;
using SistemaOficina.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Windows.Media.Imaging;
using System.Globalization;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Net.Http;
using System.Collections.ObjectModel;
using System.Media;


namespace SistemaOficina
{
    public partial class MainWindow : Window
    {
        private readonly List<Cliente> _clientes = new List<Cliente>();
        private int _currentClientId = 0;
        private readonly List<OrdemDeServico> _ordens = new List<OrdemDeServico>();
        private int _currentOsId = 0;
        private readonly List<LancamentoCaixa> _lancamentosCaixa = new List<LancamentoCaixa>();
        private readonly List<CompraGasto> _comprasGastos = new List<CompraGasto>();
        private readonly string DbPath = Path.Combine(Directory.GetCurrentDirectory(), "OficinaDB.db");
        private readonly ObservableCollection<User> _setupFuncionarios = new ObservableCollection<User>();

        private readonly string _currentUsername;
        private readonly string _currentUserRole;
        private ConfiguracaoEmpresa? _configuracaoAtual;
        private byte[]? _logoDataPerfil;
        private byte[]? _logoDataSetup;

        public bool IsAdmin => _currentUserRole == "Admin";

        private DriveService? _googleDriveService;
        private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
        private static readonly string ApplicationName = "Sistema Oficina Backup";
        private DispatcherTimer _autoBackupTimer;

        // #################### INÍCIO DA NOVA SEÇÃO ####################
        private readonly ObservableCollection<AcertoUsuarioViewModel> _acertoUsuarios = new ObservableCollection<AcertoUsuarioViewModel>();
        private readonly ObservableCollection<GastoSelecionavelViewModel> _gastosParaAcerto = new ObservableCollection<GastoSelecionavelViewModel>();
        private double _lucroPeriodoAcerto = 0;
        // #################### FIM DA NOVA SEÇÃO ####################


        private readonly List<Agendamento> _agendamentos = new List<Agendamento>();
        private DispatcherTimer _agendamentoTimer;
        private readonly List<int> _notifiedAgendamentoIds = new List<int>();

        private readonly ObservableCollection<AgendamentoViewModel> _agendamentosDashboard = new ObservableCollection<AgendamentoViewModel>();
        private DispatcherTimer _countdownTimer;



        public MainWindow(string username, string role)
        {
            InitializeComponent();
            this.DataContext = this;
            NotificationManager.Initialize(NotificationHost);
            _currentUsername = username;
            _currentUserRole = role;

            CarregarOpcoesComboBox();
            CarregarTodosOsDados();
            AplicarPrivilegios();
            InicializarTimerBackup();
            CarregarConfiguracoesBackup();
            // #################### INÍCIO DA ALTERAÇÃO ####################
            dgAcertoUsuarios.ItemsSource = _acertoUsuarios;
            CarregarUsuariosParaAcerto(); // Esta chamada agora virá depois de CarregarTecnicos/Responsaveis para garantir que a lista de usuários para acerto esteja correta
            // #################### FIM DA ALTERAÇÃO ####################



            InicializarTimerAgendamento();

            // Inicializa o ListView do Dashboard e o timer da contagem regressiva
            lvAgendamentosDashboard.ItemsSource = _agendamentosDashboard;
            InicializarTimerContagemRegressiva();
        }

        private void AplicarPrivilegios()
        {
            if (!IsAdmin)
            {
                CaixaTabItem.Visibility = Visibility.Collapsed;
                ComprasGastosTabItem.Visibility = Visibility.Collapsed;
                PerfilTabItem.Visibility = Visibility.Collapsed;
                ImportarExportarTabItem.Visibility = Visibility.Collapsed;
                // #################### INÍCIO DA ALTERAÇÃO ####################
                AcertoContasTabItem.Visibility = Visibility.Collapsed;
                // #################### FIM DA ALTERAÇÃO ####################
            }
        }

        private void ShowValidationError(string message)
        {
            NotificationManager.Show(message, NotificationType.Error, 10);
        }

        private void CarregarOpcoesComboBox()
        {
            cmbGarantia.ItemsSource = new List<string> { "Não", "Sim" };
            var temposGarantia = new List<string>();
            for (int i = 1; i <= 12; i++)
            {
                temposGarantia.Add(i == 1 ? "1 Mês" : $"{i} Meses");
            }
            for (int i = 2; i <= 3; i++) temposGarantia.Add($"{i} Anos");
            cmbTempoGarantia.ItemsSource = temposGarantia;

            cmbVoltagem.ItemsSource = new List<string> { "5V", "9V", "12V", "24V", "110V", "220V", "Bivolt" };
            cmbDepartamento.ItemsSource = new List<string> { "Diversos", "Som", "Imagem" };

            var tipos = new List<string> { "Compra", "Gasto", "Receita Extra" };
            cmbTipoCompraGasto.ItemsSource = tipos;

            cmbEquipamentoAgendamento.ItemsSource = new List<string> { "Televisão", "Caixa de Som", "Aparelho de Som", "Video Game", "Outro" };


            PopularHorasMinutos();

        }

        // #################### INÍCIO DOS NOVOS MÉTODOS ####################
        private void InicializarTimerContagemRegressiva()
        {
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            // Percorre a lista de agendamentos do dashboard para atualizar o tempo restante
            foreach (var vm in _agendamentosDashboard)
            {
                var tempoRestante = vm.DataAgendamento - DateTime.Now;
                if (tempoRestante.TotalSeconds > 0)
                {
                    // Formata a string para Dias:Horas:Minutos:Segundos
                    vm.TempoRestante = string.Format("{0:00}:{1:00}:{2:00}:{3:00}",
                        tempoRestante.Days, tempoRestante.Hours, tempoRestante.Minutes, tempoRestante.Seconds);
                }
                else
                {
                    vm.TempoRestante = "Atrasado";
                }
            }
        }

        private void CarregarAgendamentosDashboard()
        {
            _agendamentosDashboard.Clear();
            var proximosAgendamentos = _agendamentos
                .Where(a => a.DataAgendamento > DateTime.Now) // O filtro principal está aqui
                .OrderBy(a => a.DataAgendamento)
                .Take(10); // Limita a 10 agendamentos para não poluir a tela

            foreach (var agendamento in proximosAgendamentos)
            {
                _agendamentosDashboard.Add(new AgendamentoViewModel(agendamento));
            }

            // Atualiza os timers imediatamente após carregar
            CountdownTimer_Tick(null, EventArgs.Empty);
        }
    

        private void CarregarTodosOsDados()
        {
            CarregarConfiguracaoEmpresa();
            CarregarClientes();
            CarregarOrdens();
            CarregarClientesParaOS();
            CarregarComprasGastos();
            CarregarAgendamentos(); // Esta chamada já irá disparar a atualização do dashboard
            CarregarClientesParaAgendamento();
            CarregarTecnicos();
            CarregarResponsaveisFinanceiros();
            CarregarCaixa();
            AtualizarDashboard();
            CarregarOrdensParaAssociar();
        }

        private void CarregarConfiguracaoEmpresa()
        {
            dgSetupFuncionarios.ItemsSource = _setupFuncionarios;
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT * FROM ConfiguracaoEmpresa WHERE Id = 1", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            _configuracaoAtual = new ConfiguracaoEmpresa
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                NomeEmpresa = reader["NomeEmpresa"] as string,
                                Logo = reader["Logo"] as byte[],
                                Endereco = reader["Endereco"] as string,
                                Telefone = reader["Telefone"] as string,
                                Proprietario = reader["Proprietario"] as string,
                                FuncionarioPrincipal = reader["FuncionarioPrincipal"] as string,
                                PrimeiroAcessoConcluido = Convert.ToBoolean(reader.GetInt32(reader.GetOrdinal("PrimeiroAcessoConcluido"))),
                                CpfCnpj = reader["CpfCnpj"] as string
                            };
                        }
                    }
                }

                if (_configuracaoAtual != null && !_configuracaoAtual.PrimeiroAcessoConcluido && _currentUserRole == "Admin")
                {
                    SetupOverlayGrid.Visibility = Visibility.Visible;
                    txtSetupNomeEmpresa.Text = _configuracaoAtual.NomeEmpresa;
                    txtSetupEnderecoEmpresa.Text = _configuracaoAtual.Endereco;
                    txtSetupTelefoneEmpresa.Text = _configuracaoAtual.Telefone;
                    txtSetupProprietario.Text = _configuracaoAtual.Proprietario;
                    txtSetupFuncionario.Text = _configuracaoAtual.FuncionarioPrincipal;
                    txtSetupCpfCnpjEmpresa.Text = _configuracaoAtual.CpfCnpj;
                    if (_configuracaoAtual.Logo != null)
                    {
                        _logoDataSetup = _configuracaoAtual.Logo;
                        BitmapImage bitmap = new BitmapImage();
                        using (MemoryStream stream = new MemoryStream(_logoDataSetup))
                        {
                            stream.Position = 0;
                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                        }
                        imgSetupLogo.Source = bitmap;
                    }
                }
                else
                {
                    SetupOverlayGrid.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro crítico ao carregar as configurações da empresa: {ex.Message}");
            }
        }

        #region Configuração Inicial (Setup)

        // Este método é novo. Ele controla a navegação para o Passo 2.
        private void btnSetupAvancar_Click(object sender, RoutedEventArgs e)
        {
            pnlSetupPasso1.Visibility = Visibility.Collapsed;
            pnlSetupPasso2.Visibility = Visibility.Visible;

            btnSetupVoltar.IsEnabled = true;
            btnSetupAvancar.Visibility = Visibility.Collapsed;
            btnSetupConcluir.Visibility = Visibility.Visible;
        }

        // Este método é novo. Ele controla a navegação de volta para o Passo 1.
        private void btnSetupVoltar_Click(object sender, RoutedEventArgs e)
        {
            pnlSetupPasso1.Visibility = Visibility.Visible;
            pnlSetupPasso2.Visibility = Visibility.Collapsed;

            btnSetupVoltar.IsEnabled = false;
            btnSetupAvancar.Visibility = Visibility.Visible;
            btnSetupConcluir.Visibility = Visibility.Collapsed;
        }

        // Este método é novo. Adiciona um funcionário à lista temporária.
        private void btnSetupAdicionarFuncionario_Click(object sender, RoutedEventArgs e)
        {
            var nome = txtSetupNomeFuncionario.Text.Trim();
            var cargo = (cmbSetupCargoFuncionario.SelectedItem as string) ?? string.Empty;
            var senha = pwdSetupSenhaFuncionario.Password;

            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(cargo) || string.IsNullOrWhiteSpace(senha))
            {
                ShowValidationError("Preencha Nome, Cargo e Senha para adicionar um funcionário.");
                return;
            }

            if (_setupFuncionarios.Any(u => u.Username.Equals(nome, StringComparison.OrdinalIgnoreCase)))
            {
                ShowValidationError("Este nome de usuário já foi adicionado à lista.");
                return;
            }

            _setupFuncionarios.Add(new User { Username = nome, Role = cargo, PasswordHash = senha }); // A senha será codificada ao salvar

            txtSetupNomeFuncionario.Clear();
            pwdSetupSenhaFuncionario.Clear();
            cmbSetupCargoFuncionario.SelectedIndex = -1;
        }

        // Este método é novo. Remove um funcionário da lista temporária.
        private void btnSetupRemoverFuncionario_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is User user)
            {
                _setupFuncionarios.Remove(user);
            }
        }

        // Método antigo, sem alterações na lógica interna
        private void btnSelecionarLogoSetup_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Selecione uma imagem para a logo",
                Filter = "Arquivos de Imagem (*.jpg; *.jpeg; *.png)|*.jpg;*.jpeg;*.png"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _logoDataSetup = File.ReadAllBytes(openFileDialog.FileName);
                    BitmapImage bitmap = new BitmapImage();
                    using (MemoryStream stream = new MemoryStream(_logoDataSetup))
                    {
                        stream.Position = 0;
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                    }
                    imgSetupLogo.Source = bitmap;
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Erro ao carregar a imagem: {ex.Message}", NotificationType.Error);
                }
            }
        }

        // O antigo btnSalvarSetup_Click agora é o btnSetupConcluir_Click, com lógica para salvar os funcionários também.
        private void btnSetupConcluir_Click(object sender, RoutedEventArgs e)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(txtSetupNomeEmpresa.Text)) errors.Add("• O Nome da Empresa é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtSetupEnderecoEmpresa.Text)) errors.Add("• O Endereço da Empresa é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtSetupTelefoneEmpresa.Text)) errors.Add("• O Telefone da Empresa é obrigatório.");

            string cnpjDigitsSetup = new string(txtSetupCpfCnpjEmpresa.Text.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(cnpjDigitsSetup) && cnpjDigitsSetup.Length < 14)
            {
                errors.Add("• O CNPJ da empresa está incompleto. Por favor, preencha todos os 14 dígitos.");
            }

            if (errors.Any())
            {
                ShowValidationError("Por favor, corrija os seguintes erros:\n\n" + string.Join("\n", errors));
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 1. Salvar os funcionários adicionados
                        foreach (var funcionario in _setupFuncionarios)
                        {
                            var insertCmd = new SQLiteCommand("INSERT INTO Users (Username, PasswordHash, Role) VALUES (@username, @passwordHash, @role)", connection);
                            insertCmd.Parameters.AddWithValue("@username", funcionario.Username);
                            insertCmd.Parameters.AddWithValue("@passwordHash", PasswordHelper.HashPassword(funcionario.PasswordHash)); // Codifica a senha
                            insertCmd.Parameters.AddWithValue("@role", funcionario.Role); // Salva o cargo como 'Role'
                            insertCmd.ExecuteNonQuery();
                        }

                        // 2. Salvar os dados da empresa
                        var command = new SQLiteCommand(@"
                            UPDATE ConfiguracaoEmpresa 
                            SET NomeEmpresa = @NomeEmpresa, Logo = @Logo, Endereco = @Endereco, Telefone = @Telefone, 
                                Proprietario = @Proprietario, FuncionarioPrincipal = @FuncionarioPrincipal, 
                                PrimeiroAcessoConcluido = 1, CpfCnpj = @CpfCnpj
                            WHERE Id = 1", connection);

                        command.Parameters.AddWithValue("@NomeEmpresa", txtSetupNomeEmpresa.Text);
                        command.Parameters.AddWithValue("@Logo", _logoDataSetup ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Endereco", txtSetupEnderecoEmpresa.Text);
                        command.Parameters.AddWithValue("@Telefone", txtSetupTelefoneEmpresa.Text);
                        command.Parameters.AddWithValue("@Proprietario", txtSetupProprietario.Text);
                        command.Parameters.AddWithValue("@FuncionarioPrincipal", txtSetupFuncionario.Text);
                        command.Parameters.AddWithValue("@CpfCnpj", txtSetupCpfCnpjEmpresa.Text);
                        command.ExecuteNonQuery();

                        transaction.Commit();
                    }
                }

                NotificationManager.Show("Configurações da empresa salvas com sucesso!", NotificationType.Success);
                SetupOverlayGrid.Visibility = Visibility.Collapsed;
                CarregarTodosOsDados(); // Recarrega tudo para a aplicação funcionar com os novos dados
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro ao salvar as configurações: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion // Fim da região Setup

        // #################### INÍCIO DA ALTERAÇÃO (MÉTODO NOVO) ####################
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Esta chamada garante que o indicador de agendamentos pendentes seja
            // atualizado corretamente após a janela e seus templates serem totalmente carregados.
            // O problema original ocorria porque a tentativa de atualização no construtor
            // acontecia antes do template do botão estar disponível.
            AtualizarIndicadorAgendamentos();
        }
        // #################### FIM DA ALTERAÇÃO (MÉTODO NOVO) ####################

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!e.Source.Equals(MainTabControl))
            {
                return;
            }

            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem addedTab)
            {
                if (addedTab == DashboardTabItem)
                {
                    AtualizarDashboard();
                }
                else if (addedTab == PerfilTabItem)
                {
                    CarregarDadosPerfil();
                }
                else if (addedTab == OrdemServicoTabItem && _currentOsId == 0)
                {
                    if (string.IsNullOrEmpty(txtNumeroOS.Text))
                    {
                        GenerateNumeroOS();
                    }
                }
            }

            if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem removedTab)
            {
                if (removedTab == OrdemServicoTabItem && _currentOsId != 0)
                {
                    LimparCamposOS();
                }
                else if (removedTab == CadastroClienteTabItem && _currentClientId != 0)
                {
                    LimparCamposCliente();
                }
            }
        }

        #region Perfil
        private void CarregarDadosPerfil()
        {
            if (txtPerfilUsername == null) return;
            txtPerfilUsername.Text = _currentUsername;
            pwdPerfilSenhaAtual.Password = string.Empty;
            pwdPerfilNovaSenha.Password = string.Empty;
            pwdPerfilConfirmarNovaSenha.Password = string.Empty;
            if (_configuracaoAtual != null)
            {
                txtPerfilNomeEmpresa.Text = _configuracaoAtual.NomeEmpresa;
                txtPerfilEnderecoEmpresa.Text = _configuracaoAtual.Endereco;
                txtPerfilTelefoneEmpresa.Text = _configuracaoAtual.Telefone;
                txtPerfilProprietario.Text = _configuracaoAtual.Proprietario;
                txtPerfilFuncionario.Text = _configuracaoAtual.FuncionarioPrincipal;
                txtPerfilCpfCnpjEmpresa.Text = _configuracaoAtual.CpfCnpj;
                _logoDataPerfil = _configuracaoAtual.Logo;
                if (_logoDataPerfil != null && _logoDataPerfil.Length > 0)
                {
                    BitmapImage bitmap = new BitmapImage();
                    using (MemoryStream stream = new MemoryStream(_logoDataPerfil))
                    {
                        stream.Position = 0;
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                    }
                    imgPerfilLogo.Source = bitmap;
                }
                else
                {
                    imgPerfilLogo.Source = null;
                }
            }

            if (IsAdmin)
            {
                pnlGerenciarContaSecundaria.Visibility = Visibility.Visible;
                CarregarDadosContaSecundaria();
            }
            else
            {
                pnlGerenciarContaSecundaria.Visibility = Visibility.Collapsed;
            }
        }

        private void CarregarDadosContaSecundaria()
        {
            var secondaryUsers = new List<User>();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    // ALTERAÇÃO: Adicionado 'IncluirNoAcerto' na consulta
                    var command = new SQLiteCommand("SELECT Id, Username, Role, IncluirNoAcerto FROM Users WHERE Role != 'Admin' ORDER BY Username", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            secondaryUsers.Add(new User
                            {
                                Id = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                Role = reader.GetString(2),
                                // ALTERAÇÃO: Lendo o novo valor do banco
                                IncluirNoAcerto = Convert.ToBoolean(reader.GetInt32(3))
                            });
                        }
                    }
                }
                dgContasSecundarias.ItemsSource = secondaryUsers;
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao carregar contas secundárias: {ex.Message}", NotificationType.Error);
            }
        }

        // #################### INÍCIO DA ALTERAÇÃO (MÉTODO NOVO) ####################
        private void chkIncluirNoAcerto_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.DataContext is User user)
            {
                try
                {
                    using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                    {
                        connection.Open();
                        var command = new SQLiteCommand("UPDATE Users SET IncluirNoAcerto = @Incluir WHERE Id = @Id", connection);
                        command.Parameters.AddWithValue("@Incluir", user.IncluirNoAcerto ? 1 : 0);
                        command.Parameters.AddWithValue("@Id", user.Id);
                        command.ExecuteNonQuery();
                    }
                    // Atualiza a lista de usuários na aba "Acerto de Contas" para refletir a mudança imediatamente.
                    CarregarUsuariosParaAcerto();
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Erro ao atualizar a preferência de acerto: {ex.Message}", NotificationType.Error);
                }
            }
        }
        // #################### FIM DA ALTERAÇÃO ####################

        private void btnCriarContaSecundaria_Click(object sender, RoutedEventArgs e)
        {
            string username = txtNovoUsuarioSecundario.Text.Trim();
            string password = pwdNovoUsuarioSecundario.Password;
            string confirmPassword = pwdConfirmarNovoUsuarioSecundario.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowValidationError("Nome de usuário e senha são obrigatórios.");
                return;
            }
            if (password != confirmPassword)
            {
                ShowValidationError("As senhas não coincidem.");
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var checkCmd = new SQLiteCommand("SELECT COUNT(1) FROM Users WHERE Username = @username", connection);
                    checkCmd.Parameters.AddWithValue("@username", username);
                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    {
                        ShowValidationError("Este nome de usuário já está em uso.");
                        return;
                    }

                    string passwordHash = PasswordHelper.HashPassword(password);
                    var insertCmd = new SQLiteCommand("INSERT INTO Users (Username, PasswordHash, Role) VALUES (@username, @passwordHash, 'User')", connection);
                    insertCmd.Parameters.AddWithValue("@username", username);
                    insertCmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                    insertCmd.Parameters.AddWithValue("@role", "User"); // Salva como 'User' para contas secundárias
                    insertCmd.ExecuteNonQuery();
                    NotificationManager.Show("Conta secundária criada com sucesso!", NotificationType.Success);

                    txtNovoUsuarioSecundario.Clear();
                    pwdNovoUsuarioSecundario.Clear();
                    pwdConfirmarNovoUsuarioSecundario.Clear();
                    CarregarDadosContaSecundaria();

                    // ##### CORREÇÃO AQUI #####
                    // Chamando as funções para recarregar as listas de técnicos e responsáveis financeiros
                    CarregarTecnicos();
                    CarregarResponsaveisFinanceiros();
                    CarregarUsuariosParaAcerto(); // Atualiza a lista de usuários para acerto de contas
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao criar conta: {ex.Message}", NotificationType.Error);
            }
        }

        private void btnExcluirContaSecundaria_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.DataContext is User userToDelete)) return;
            var confirmBox = new CustomMessageBox($"Tem certeza que deseja excluir permanentemente a conta secundária '{userToDelete.Username}'?", "Confirmar Exclusão", MessageBoxButton.OKCancel);
            confirmBox.Owner = this;
            if (confirmBox.ShowDialog() == true && confirmBox.Result == MessageBoxResult.OK)
            {
                try
                {
                    using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                    {

                        connection.Open();
                        var command = new SQLiteCommand("DELETE FROM Users WHERE Id = @Id", connection);
                        command.Parameters.AddWithValue("@Id", userToDelete.Id);
                        command.ExecuteNonQuery();
                        NotificationManager.Show("Conta secundária excluída com sucesso!", NotificationType.Success);
                        CarregarDadosContaSecundaria();

                        // ##### CORREÇÃO AQUI #####
                        // Chamando as funções para recarregar as listas de técnicos e responsáveis financeiros
                        CarregarTecnicos();
                        CarregarResponsaveisFinanceiros();
                        CarregarUsuariosParaAcerto(); // Atualiza a lista de usuários para acerto de contas
                    }
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Ocorreu um erro ao excluir a conta: {ex.Message}", NotificationType.Error);
                }
            }
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.KeepLoggedIn = false;
            Properties.Settings.Default.SavedUsername = string.Empty;
            Properties.Settings.Default.Save();

            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();

            this.Close();
        }

        private void btnLogoutIcon_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.KeepLoggedIn = false;
            Properties.Settings.Default.SavedUsername = string.Empty;
            Properties.Settings.Default.Save();

            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();

            this.Close();
        }

        private void btnSalvarDadosAcesso_Click(object sender, RoutedEventArgs e)
        {
            string senhaAtual = pwdPerfilSenhaAtual.Password;
            string novaSenha = pwdPerfilNovaSenha.Password;
            string confirmarNovaSenha = pwdPerfilConfirmarNovaSenha.Password;

            if (string.IsNullOrWhiteSpace(senhaAtual))
            {
                ShowValidationError("A senha atual é obrigatória para fazer alterações.");
                return;
            }

            if (novaSenha != confirmarNovaSenha)
            {
                ShowValidationError("A nova senha e a confirmação não coincidem.");
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var commandCheck = new SQLiteCommand("SELECT PasswordHash FROM Users WHERE Username = @username", connection);
                    commandCheck.Parameters.AddWithValue("@username", _currentUsername);
                    var storedHash = commandCheck.ExecuteScalar() as string;

                    if (storedHash == null || !PasswordHelper.VerifyPassword(senhaAtual, storedHash))
                    {
                        ShowValidationError("A senha atual está incorreta.");
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(novaSenha))
                    {
                        string novoPasswordHash = PasswordHelper.HashPassword(novaSenha);
                        var commandUpdate = new SQLiteCommand("UPDATE Users SET PasswordHash = @passwordHash WHERE Username = @username", connection);
                        commandUpdate.Parameters.AddWithValue("@passwordHash", novoPasswordHash);
                        commandUpdate.Parameters.AddWithValue("@username", _currentUsername);
                        commandUpdate.ExecuteNonQuery();
                        NotificationManager.Show("Senha alterada com sucesso!", NotificationType.Success);
                    }
                    else
                    {
                        NotificationManager.Show("Nenhuma alteração de senha foi fornecida.", NotificationType.Warning);
                    }

                    pwdPerfilSenhaAtual.Password = string.Empty;
                    pwdPerfilNovaSenha.Password = string.Empty;
                    pwdPerfilConfirmarNovaSenha.Password = string.Empty;
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao atualizar dados de acesso: {ex.Message}", NotificationType.Error);
            }
        }

        private void btnSelecionarNovaLogo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Selecione uma imagem para a logo",
                Filter = "Arquivos de Imagem (*.jpg; *.jpeg; *.png)|*.jpg;*.jpeg;*.png"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _logoDataPerfil = File.ReadAllBytes(openFileDialog.FileName);
                    BitmapImage bitmap = new BitmapImage();
                    using (MemoryStream stream = new MemoryStream(_logoDataPerfil))
                    {
                        stream.Position = 0;
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                    }
                    imgPerfilLogo.Source = bitmap;
                    NotificationManager.Show("Logo carregada. Clique em 'Salvar Alterações' para confirmar.", NotificationType.Success);
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Erro ao carregar a imagem: {ex.Message}", NotificationType.Error);
                }
            }
        }

        private void btnSalvarDadosEmpresa_Click(object sender, RoutedEventArgs e)
        {
            if (!IsAdmin)
            {
                ShowValidationError("Apenas administradores podem alterar os dados da empresa.");
                return;
            }

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(txtPerfilNomeEmpresa.Text)) errors.Add("• O Nome da Empresa é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtPerfilEnderecoEmpresa.Text)) errors.Add("• O Endereço da Empresa é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtPerfilTelefoneEmpresa.Text)) errors.Add("• O Telefone da Empresa é obrigatório.");

            string cnpjDigitsPerfil = new string(txtPerfilCpfCnpjEmpresa.Text.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(cnpjDigitsPerfil) && cnpjDigitsPerfil.Length < 14)
            {
                errors.Add("• O CNPJ da empresa está incompleto. Por favor, preencha todos os 14 dígitos.");
            }

            if (errors.Any())
            {
                ShowValidationError("Por favor, corrija os seguintes erros:\n\n" + string.Join("\n", errors));
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand(@"
                        UPDATE ConfiguracaoEmpresa 
                        SET NomeEmpresa = @NomeEmpresa, 
                            Logo = @Logo, 
                            Endereco = @Endereco, 
                            Telefone = @Telefone, 
                            Proprietario = @Proprietario, 
                            FuncionarioPrincipal = @FuncionarioPrincipal,
                            CpfCnpj = @CpfCnpj
                        WHERE Id = 1", connection);
                    command.Parameters.AddWithValue("@NomeEmpresa", txtPerfilNomeEmpresa.Text);
                    command.Parameters.AddWithValue("@Logo", _logoDataPerfil ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Endereco", txtPerfilEnderecoEmpresa.Text);
                    command.Parameters.AddWithValue("@Telefone", txtPerfilTelefoneEmpresa.Text);
                    command.Parameters.AddWithValue("@Proprietario", txtPerfilProprietario.Text);
                    command.Parameters.AddWithValue("@FuncionarioPrincipal", txtPerfilFuncionario.Text);
                    command.Parameters.AddWithValue("@CpfCnpj", txtPerfilCpfCnpjEmpresa.Text);

                    command.ExecuteNonQuery();
                }

                NotificationManager.Show("Dados da empresa salvos com sucesso!", NotificationType.Success);
                CarregarConfiguracaoEmpresa();
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Ocorreu um erro ao salvar as configurações: {ex.Message}", NotificationType.Error);
            }
        }
        #endregion

        #region Dashboard
        private void AtualizarDashboard()
        {
            if (txtDashboardOrdensAbertas == null) return;
            int ordensAbertas = _ordens.Count(o => o.Status != "Fechada");
            txtDashboardOrdensAbertas.Text = ordensAbertas.ToString();

            int totalClientes = _clientes.Count;
            txtDashboardClientes.Text = totalClientes.ToString();
            DateTime hoje = DateTime.Today;
            double entradasHoje = _lancamentosCaixa
                .Where(l => l.Data.Date == hoje && l.Tipo == "Entrada")
                .Sum(l => l.Valor);
            double saidasHoje = _lancamentosCaixa
                .Where(l => l.Data.Date == hoje && l.Tipo == "Saida")
                .Sum(l => l.Valor);
            double saldoHoje = entradasHoje - saidasHoje;
            txtDashboardSaldoDia.Text = saldoHoje.ToString("C");
            txtDashboardSaldoDia.Foreground = saldoHoje >= 0 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E67E22")) : Brushes.Red;
            int mesAtual = DateTime.Now.Month;
            int anoAtual = DateTime.Now.Year;
            int osNoMes = _ordens.Count(o => o.DataAbertura.Month == mesAtual && o.DataAbertura.Year == anoAtual);
            txtDashboardOsMes.Text = osNoMes.ToString();
        }
        private void btnAtalhoNovaOS_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedItem = OrdemServicoTabItem;
        }

        private void btnAtalhoNovoAgendamento_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedItem = AgendamentoTabItem;
        }
        private void btnAtalhoNovoCliente_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedItem = CadastroClienteTabItem;
            btnAdicionarCliente_Click(sender, e);
        }
        private void btnAtalhoCaixa_Click(object sender, RoutedEventArgs e)
        {
            MainTabControl.SelectedItem = CaixaTabItem;
        }
        #endregion

        #region Máscaras de Formatação
        private void txtTelefone_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            var text = new string(tb.Text.Where(char.IsDigit).ToArray());
            if (text.StartsWith("55"))
            {
                text = text.Substring(2);
            }

            if (text.Length > 11)
            {
                text = text.Substring(0, 11);
            }

            string formattedText = "";
            if (text.Length > 0)
            {
                if (text.Length <= 2) formattedText = $"({text}";
                else if (text.Length <= 6) formattedText = $"({text.Substring(0, 2)}) {text.Substring(2)}";
                else if (text.Length <= 10) formattedText = $"({text.Substring(0, 2)}) {text.Substring(2, 4)}-{text.Substring(6)}";
                else formattedText = $"({text.Substring(0, 2)}) {text.Substring(2, 5)}-{text.Substring(7)}";

                formattedText = "+55 " + formattedText;
            }

            tb.TextChanged -= txtTelefone_TextChanged;
            tb.Text = formattedText;
            tb.CaretIndex = tb.Text.Length;
            tb.TextChanged += txtTelefone_TextChanged;

        }

        private void txtCpfCnpj_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            int selectionStart = tb.SelectionStart;
            string originalText = tb.Text;

            tb.TextChanged -= txtCpfCnpj_TextChanged;

            try
            {
                string digitsOnly = new string(originalText.Where(char.IsDigit).ToArray());
                string formattedText = digitsOnly;

                if (digitsOnly.Length > 11)
                {
                    if (digitsOnly.Length > 14) digitsOnly = digitsOnly.Substring(0, 14);
                    if (digitsOnly.Length > 12)
                        formattedText = $"{digitsOnly.Substring(0, 2)}.{digitsOnly.Substring(2, 3)}.{digitsOnly.Substring(5, 3)}/{digitsOnly.Substring(8, 4)}-{digitsOnly.Substring(12)}";
                    else if (digitsOnly.Length > 8)
                        formattedText = $"{digitsOnly.Substring(0, 2)}.{digitsOnly.Substring(2, 3)}.{digitsOnly.Substring(5, 3)}/{digitsOnly.Substring(8)}";
                    else if (digitsOnly.Length > 5)
                        formattedText = $"{digitsOnly.Substring(0, 2)}.{digitsOnly.Substring(2, 3)}.{digitsOnly.Substring(5)}";
                    else if (digitsOnly.Length > 2)
                        formattedText = $"{digitsOnly.Substring(0, 2)}.{digitsOnly.Substring(2)}";
                }
                else
                {
                    if (digitsOnly.Length > 9)
                        formattedText = $"{digitsOnly.Substring(0, 3)}.{digitsOnly.Substring(3, 3)}.{digitsOnly.Substring(6, 3)}-{digitsOnly.Substring(9)}";
                    else if (digitsOnly.Length > 6)
                        formattedText = $"{digitsOnly.Substring(0, 3)}.{digitsOnly.Substring(3, 3)}.{digitsOnly.Substring(6)}";
                    else if (digitsOnly.Length > 3)
                        formattedText = $"{digitsOnly.Substring(0, 3)}.{digitsOnly.Substring(3)}";
                }

                tb.Text = formattedText;
                int newCursorPos = selectionStart + (formattedText.Length - originalText.Length);
                tb.SelectionStart = Math.Max(0, Math.Min(tb.Text.Length, newCursorPos));
            }
            finally
            {
                tb.TextChanged += txtCpfCnpj_TextChanged;
            }
        }
        #endregion

        #region Clientes
        private void LimparCamposCliente()
        {
            txtNomeCliente.Text = string.Empty;
            txtEnderecoCliente.Text = string.Empty;
            txtCidadeCliente.Text = string.Empty;
            txtBairroCliente.Text = string.Empty;
            txtTelefone1Cliente.Text = string.Empty;
            txtTelefone2Cliente.Text = string.Empty;
            txtCpfCnpjCliente.Text = string.Empty;
            _currentClientId = 0;
        }
        private void btnAdicionarCliente_Click(object sender, RoutedEventArgs e)
        {
            LimparCamposCliente();
            NotificationManager.Show("Campos prontos para um novo cliente.", NotificationType.Success);
        }

        private void btnSalvarCliente_Click(object sender, RoutedEventArgs e)
        {
            if (_currentClientId != 0 && !IsAdmin)
            {
                ShowValidationError("Apenas administradores podem editar clientes existentes.");
                return;
            }

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(txtNomeCliente.Text)) errors.Add("• O campo 'Nome Completo' é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtEnderecoCliente.Text)) errors.Add("• O campo 'Endereço' é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtBairroCliente.Text)) errors.Add("• O campo 'Bairro' é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtCidadeCliente.Text)) errors.Add("• O campo 'Cidade' é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtTelefone1Cliente.Text)) errors.Add("• O campo 'Telefone 1' é obrigatório.");

            if (errors.Any())
            {
                ShowValidationError("Por favor, corrija os seguintes erros:\n\n" + string.Join("\n", errors));
                return;
            }

            string nome = txtNomeCliente.Text.Trim();
            string endereco = txtEnderecoCliente.Text.Trim();
            string cidade = txtCidadeCliente.Text.Trim();
            string bairro = txtBairroCliente.Text.Trim();
            string tel1 = txtTelefone1Cliente.Text.Trim();
            string tel2 = txtTelefone2Cliente.Text.Trim();
            string cpfCnpj = txtCpfCnpjCliente.Text.Trim();

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    if (_currentClientId == 0)
                    {
                        var command = new SQLiteCommand("INSERT INTO Clientes (Nome, Endereco, Cidade, Bairro, Telefone1, Telefone2, CpfCnpj) VALUES (@Nome, @Endereco, @Cidade, @Bairro, @Telefone1, @Telefone2, @CpfCnpj)", connection);
                        command.Parameters.AddWithValue("@Nome", nome);
                        command.Parameters.AddWithValue("@Endereco", endereco);
                        command.Parameters.AddWithValue("@Cidade", cidade);
                        command.Parameters.AddWithValue("@Bairro", bairro);
                        command.Parameters.AddWithValue("@Telefone1", tel1);
                        command.Parameters.AddWithValue("@Telefone2", tel2);
                        command.Parameters.AddWithValue("@CpfCnpj", cpfCnpj);
                        command.ExecuteNonQuery();
                        NotificationManager.Show("Cliente adicionado com sucesso!", NotificationType.Success);
                    }
                    else
                    {
                        var command = new SQLiteCommand("UPDATE Clientes SET Nome = @Nome, Endereco = @Endereco, Cidade = @Cidade, Bairro = @Bairro, Telefone1 = @Telefone1, Telefone2 = @Telefone2, CpfCnpj = @CpfCnpj WHERE Id = @Id", connection);
                        command.Parameters.AddWithValue("@Nome", nome);
                        command.Parameters.AddWithValue("@Endereco", endereco);
                        command.Parameters.AddWithValue("@Cidade", cidade);
                        command.Parameters.AddWithValue("@Bairro", bairro);
                        command.Parameters.AddWithValue("@Telefone1", tel1);
                        command.Parameters.AddWithValue("@Telefone2", tel2);
                        command.Parameters.AddWithValue("@CpfCnpj", cpfCnpj);
                        command.Parameters.AddWithValue("@Id", _currentClientId);
                        command.ExecuteNonQuery();
                        NotificationManager.Show("Cliente atualizado com sucesso!", NotificationType.Success);
                    }
                    LimparCamposCliente();
                    CarregarTodosOsDados();
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Ocorreu um erro inesperado: {ex.Message}", NotificationType.Error);
            }
        }

        private void btnExcluirCliente_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.DataContext is Cliente selectedClient)) return;
            bool hasOrders = false;
            using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
            {
                connection.Open();
                var commandCheck = new SQLiteCommand("SELECT COUNT(*) FROM OrdensDeServico WHERE ClienteId = @ClienteId", connection);
                commandCheck.Parameters.AddWithValue("@ClienteId", selectedClient.Id);
                long count = (long)commandCheck.ExecuteScalar();
                if (count > 0)
                {
                    hasOrders = true;
                }
            }

            if (hasOrders)
            {
                ShowValidationError($"Não é possível excluir o cliente '{selectedClient.Nome}' porque ele possui ordens de serviço associadas.");
                return;
            }

            var confirmBox = new CustomMessageBox($"Tem certeza que deseja excluir permanentemente o cliente '{selectedClient.Nome}'?\n\nEsta ação não pode ser desfeita.", "Confirmar Exclusão", MessageBoxButton.OKCancel);
            confirmBox.Owner = this;
            if (confirmBox.ShowDialog() == true && confirmBox.Result == MessageBoxResult.OK)
            {
                try
                {
                    using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                    {
                        connection.Open();
                        var command = new SQLiteCommand("DELETE FROM Clientes WHERE Id = @Id", connection);
                        command.Parameters.AddWithValue("@Id", selectedClient.Id);
                        command.ExecuteNonQuery();

                        NotificationManager.Show("Cliente excluído com sucesso!", NotificationType.Success);
                        CarregarTodosOsDados();
                    }
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Ocorreu um erro ao excluir o cliente: {ex.Message}", NotificationType.Error);
                }
            }
        }
        private void CarregarClientes()
        {
            _clientes.Clear();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT Id, Nome, Endereco, Cidade, Bairro, Telefone1, Telefone2, CpfCnpj FROM Clientes ORDER BY Nome", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _clientes.Add(new Cliente
                            {
                                Id = reader.GetInt32(0),
                                Nome = reader.GetString(1),
                                Endereco = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Cidade = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Bairro = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Telefone1 = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Telefone2 = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                CpfCnpj = reader.IsDBNull(7) ? "" : reader.GetString(7)
                            });
                        }
                    }
                }
                dgClientes.ItemsSource = null;
                dgClientes.ItemsSource = _clientes;
            }
            catch (SQLiteException ex)
            {
                NotificationManager.Show($"Erro ao carregar clientes: {ex.Message}", NotificationType.Error);
            }
        }
        private void txtPesquisaCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = txtPesquisaCliente.Text.ToLower().Trim();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                dgClientes.ItemsSource = _clientes;
            }
            else
            {
                var filteredClients = _clientes.Where(c =>
                    c.Nome.ToLower().Contains(searchText)).ToList();
                dgClientes.ItemsSource = filteredClients;
            }
        }
        private void btnEditarClienteLista_Click(object sender, RoutedEventArgs e)
        {
            if (dgClientes.SelectedItem is Cliente selectedClient)
            {
                txtNomeCliente.Text = selectedClient.Nome;
                txtEnderecoCliente.Text = selectedClient.Endereco;
                txtCidadeCliente.Text = selectedClient.Cidade;
                txtBairroCliente.Text = selectedClient.Bairro;
                txtTelefone1Cliente.Text = selectedClient.Telefone1;
                txtTelefone2Cliente.Text = selectedClient.Telefone2;
                txtCpfCnpjCliente.Text = selectedClient.CpfCnpj;
                _currentClientId = selectedClient.Id;
                MainTabControl.SelectedItem = CadastroClienteTabItem;
            }
            else
            {
                NotificationManager.Show("Selecione um cliente na lista para editar.", NotificationType.Warning);
            }
        }
        private void btnVerOrdensRelacionadas_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Cliente selectedClient && _configuracaoAtual != null)
            {
                HistoricoClienteWindow historicoWindow = new HistoricoClienteWindow(selectedClient.Id, _configuracaoAtual);
                historicoWindow.Owner = this;
                historicoWindow.ShowDialog();
            }
            else
            {
                NotificationManager.Show("Selecione um cliente para ver as ordens relacionadas.", NotificationType.Warning);
            }
        }
        #endregion

        #region Ordens de Servico
        private void CarregarResponsaveisFinanceiros()
        {
            var nomesUsuarios = new List<string>();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    // MODIFICAÇÃO AQUI: Buscando apenas usuários com Role diferente de 'Admin'
                    var command = new SQLiteCommand("SELECT Username FROM Users WHERE Role != 'Admin' ORDER BY Username", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            nomesUsuarios.Add(reader.GetString(0));
                        }
                    }
                }

                // Popula os ComboBoxes das abas 'Compras/Gastos' e 'Caixa'
                cmbResponsavelCompraGasto.ItemsSource = nomesUsuarios;

                var filtroNomes = new List<string> { "Todos" };
                filtroNomes.AddRange(nomesUsuarios);
                cmbFiltroTecnico.ItemsSource = filtroNomes;
                cmbFiltroTecnico.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao carregar a lista de responsáveis: {ex.Message}", NotificationType.Error);
            }
        }

        private void CarregarTecnicos()
        {
            var nomesTecnicos = new List<string>();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    // Esta query já estava correta para pegar apenas não-administradores, mantemos ela assim.
                    var command = new SQLiteCommand("SELECT Username FROM Users WHERE Role != 'Admin' ORDER BY Username", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            nomesTecnicos.Add(reader.GetString(0));
                        }
                    }
                }
                cmbTecnicoResponsavel.ItemsSource = nomesTecnicos;
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao carregar a lista de técnicos: {ex.Message}", NotificationType.Error);
            }
        }

        private void CarregarClientesParaOS()
        {
            cmbClienteOS.ItemsSource = null;
            cmbClienteOS.ItemsSource = _clientes;
            cmbClienteOS.DisplayMemberPath = "Nome";
            cmbClienteOS.SelectedValuePath = "Id";
        }
        private void GenerateNumeroOS()
        {
            List<int> usedNumbers = new List<int>();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT NumeroOS FROM OrdensDeServico", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {

                            string numeroOS = reader.GetString(0);
                            if (int.TryParse(numeroOS, out int number) && number >= 1 && number <= 999)
                            {
                                usedNumbers.Add(number);
                            }
                        }
                    }
                }

                int nextNumber = 1;
                while (usedNumbers.Contains(nextNumber) && nextNumber <= 999)
                {
                    nextNumber++;
                }

                if (nextNumber > 999)
                {
                    ShowValidationError("Todos os números de OS de 01 a 999 já foram utilizados.");
                    txtNumeroOS.Text = "";
                    return;
                }

                txtNumeroOS.Text = nextNumber.ToString("D2");
            }
            catch (SQLiteException ex)
            {
                NotificationManager.Show($"Erro ao gerar número da OS: {ex.Message}", NotificationType.Error);
                txtNumeroOS.Text = "";
            }
        }
        private void LimparCamposOS()
        {
            txtNumeroOS.Text = string.Empty;
            cmbClienteOS.SelectedIndex = -1;
            txtNomeClienteOS.Text = string.Empty;
            txtMarcaEquipamento.Text = string.Empty;
            txtModeloEquipamento.Text = string.Empty;
            txtNumeroSerie.Text = string.Empty;
            txtDefeitoReclamado.Text = string.Empty;
            txtDefeitoReal.Text = string.Empty;
            txtServicoExecutado.Text = string.Empty;
            txtValorOS.Text = string.Empty;
            cmbTecnicoResponsavel.SelectedIndex = -1;
            _currentOsId = 0;

            txtAparelho.Text = string.Empty;
            txtCor.Text = string.Empty;
            txtComplemento.Text = string.Empty;
            txtObservacao.Text = string.Empty;

            cmbGarantia.SelectedIndex = -1;
            cmbTempoGarantia.SelectedIndex = -1;
            cmbVoltagem.SelectedIndex = -1;
            cmbDepartamento.SelectedIndex = -1;
            spTempoGarantia.Visibility = Visibility.Collapsed;

            GenerateNumeroOS();
        }
        private void btnSalvarOS_Click(object sender, RoutedEventArgs e)
        {
            if (_currentOsId != 0 && !IsAdmin)
            {
                ShowValidationError("Apenas administradores podem editar Ordens de Serviço.");
                return;
            }

            if (cmbClienteOS.SelectedItem == null && string.IsNullOrWhiteSpace(txtNomeClienteOS.Text))
            {
                ShowValidationError("Selecione um cliente cadastrado OU digite o nome do cliente avulso.");
                return;
            }

            /* // Bloco de validação de campos obrigatórios foi removido conforme solicitado.
            if (string.IsNullOrWhiteSpace(txtAparelho.Text) || string.IsNullOrWhiteSpace(txtDefeitoReclamado.Text))
            {
                ShowValidationError("Os campos 'Aparelho' e 'Defeito Alegado' são obrigatórios.");
                return;
            }
            */

            if (!double.TryParse(txtValorOS.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double valor))
            {
                valor = 0;
            }

            string nomeClienteOS = (cmbClienteOS.SelectedItem != null) ?
                string.Empty : txtNomeClienteOS.Text.Trim();
            int? clienteId = (cmbClienteOS.SelectedItem as Cliente)?.Id;

            // Lógica de garantia simplificada para não incluir o tempo.
            string garantia = cmbGarantia.SelectedItem as string ?? "";

            string tecnicoResponsavel = cmbTecnicoResponsavel.SelectedItem as string ?? string.Empty;
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    SQLiteCommand command;

                    if (_currentOsId == 0)
                    {
                        command = new SQLiteCommand(@"
                    INSERT INTO OrdensDeServico 
                      (NumeroOS, ClienteId, NomeClienteOS, MarcaEquipamento, ModeloEquipamento, NumeroSerie, DefeitoReclamado, DefeitoReal, ServicoExecutado, Valor, TecnicoResponsavel, Status, DataAbertura, Aparelho, Garantia, Voltagem, Cor, Departamento, Complemento, Observacao) 
                    VALUES 
                    (@NumeroOS, @ClienteId, @NomeClienteOS, @MarcaEquipamento, @ModeloEquipamento, @NumeroSerie, @DefeitoReclamado, @DefeitoReal, @ServicoExecutado, @Valor, @TecnicoResponsavel, 'Em análise', @DataAbertura, @Aparelho, @Garantia, @Voltagem, @Cor, @Departamento, @Complemento, @Observacao)",
                                connection);
                        command.Parameters.AddWithValue("@DataAbertura", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    else
                    {
                        command = new SQLiteCommand(@"
                    UPDATE OrdensDeServico SET 
                    NumeroOS = @NumeroOS, ClienteId = @ClienteId, NomeClienteOS = @NomeClienteOS, MarcaEquipamento = @MarcaEquipamento, ModeloEquipamento = @ModeloEquipamento, 
                    NumeroSerie = @NumeroSerie, DefeitoReclamado = @DefeitoReclamado, DefeitoReal = @DefeitoReal, ServicoExecutado = @ServicoExecutado, 
                    Valor = @Valor, TecnicoResponsavel = @TecnicoResponsavel, Aparelho = @Aparelho, Garantia = @Garantia, Voltagem = @Voltagem, 
                    Cor = @Cor, Departamento = @Departamento, Complemento = @Complemento, Observacao = @Observacao
                    WHERE Id = @Id",
                                  connection);
                        command.Parameters.AddWithValue("@Id", _currentOsId);
                    }

                    command.Parameters.AddWithValue("@NumeroOS", txtNumeroOS.Text.Trim());
                    command.Parameters.AddWithValue("@ClienteId", clienteId.HasValue ? (object)clienteId.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@NomeClienteOS", string.IsNullOrWhiteSpace(nomeClienteOS) ? DBNull.Value : (object)nomeClienteOS);
                    command.Parameters.AddWithValue("@MarcaEquipamento", txtMarcaEquipamento.Text.Trim());
                    command.Parameters.AddWithValue("@ModeloEquipamento", txtModeloEquipamento.Text.Trim());
                    command.Parameters.AddWithValue("@NumeroSerie", txtNumeroSerie.Text.Trim());
                    command.Parameters.AddWithValue("@DefeitoReclamado", txtDefeitoReclamado.Text.Trim());
                    command.Parameters.AddWithValue("@DefeitoReal", txtDefeitoReal.Text.Trim());
                    command.Parameters.AddWithValue("@ServicoExecutado", txtServicoExecutado.Text.Trim());
                    command.Parameters.AddWithValue("@Valor", valor);
                    command.Parameters.AddWithValue("@TecnicoResponsavel", tecnicoResponsavel);
                    command.Parameters.AddWithValue("@Aparelho", txtAparelho.Text.Trim());
                    command.Parameters.AddWithValue("@Garantia", garantia);
                    command.Parameters.AddWithValue("@Voltagem", cmbVoltagem.SelectedItem as string ?? string.Empty);
                    command.Parameters.AddWithValue("@Cor", txtCor.Text.Trim());
                    command.Parameters.AddWithValue("@Departamento", cmbDepartamento.SelectedItem as string ?? string.Empty);
                    command.Parameters.AddWithValue("@Complemento", txtComplemento.Text.Trim());
                    command.Parameters.AddWithValue("@Observacao", txtObservacao.Text.Trim());

                    command.ExecuteNonQuery();
                    NotificationManager.Show(_currentOsId == 0 ? "Ordem de Serviço salva com sucesso!" : "Ordem de Serviço atualizada com sucesso!", NotificationType.Success);

                    LimparCamposOS();
                    CarregarOrdens();
                    AtualizarDashboard();
                }
            }
            catch (SQLiteException ex)
            {
                if (ex.Message.Contains("UNIQUE constraint failed: OrdensDeServico.NumeroOS"))
                {
                    ShowValidationError("Já existe uma Ordem de Serviço com este número.");
                }
                else
                {
                    NotificationManager.Show($"Erro de Banco de Dados: {ex.Message}", NotificationType.Error);
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Ocorreu um erro inesperado: {ex.Message}", NotificationType.Error);
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !new Regex("[0-9,.]+").IsMatch(e.Text);
        }
        private void cmbClienteOS_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            txtNomeClienteOS.IsEnabled = (cmbClienteOS.SelectedItem == null);
            if (!txtNomeClienteOS.IsEnabled)
            {
                txtNomeClienteOS.Text = string.Empty;
            }
        }
        private void CarregarOrdens()
        {
            _ordens.Clear();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT * FROM OrdensDeServico ORDER BY DataAbertura DESC", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var os = new OrdemDeServico
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                NumeroOS = reader["NumeroOS"].ToString(),
                                ClienteId = reader["ClienteId"] == DBNull.Value ?
                               (int?)null : Convert.ToInt32(reader["ClienteId"]),
                                NomeClienteOS = reader["NomeClienteOS"].ToString(),
                                MarcaEquipamento = reader["MarcaEquipamento"].ToString(),
                                ModeloEquipamento = reader["ModeloEquipamento"].ToString(),
                                NumeroSerie = reader["NumeroSerie"].ToString(),
                                DefeitoReclamado = reader["DefeitoReclamado"].ToString(),
                                DefeitoReal = reader["DefeitoReal"].ToString(),
                                ServicoExecutado = reader["ServicoExecutado"].ToString(),
                                Valor = reader.IsDBNull(reader.GetOrdinal("Valor")) ?
                               0 : Convert.ToDouble(reader["Valor"]),
                                TecnicoResponsavel = reader["TecnicoResponsavel"].ToString(),
                                Status = reader["Status"].ToString(),
                                DataAbertura = DateTime.Parse(reader["DataAbertura"].ToString()),
                                DataFechamento = reader["DataFechamento"] == DBNull.Value ?
                               (DateTime?)null : DateTime.Parse(reader["DataFechamento"].ToString()),

                                Aparelho = reader["Aparelho"].ToString(),
                                Garantia = reader["Garantia"].ToString(),
                                Voltagem = reader["Voltagem"].ToString(),
                                Cor = reader["Cor"].ToString(),
                                Departamento = reader["Departamento"].ToString(),
                                Complemento = reader["Complemento"].ToString(),
                                Observacao = reader["Observacao"].ToString(),
                            };
                            if (os.ClienteId.HasValue)
                            {
                                var cliente = _clientes.FirstOrDefault(c => c.Id == os.ClienteId.Value);
                                os.NomeClienteCache = cliente?.Nome ?? "Cliente não encontrado";
                            }
                            _ordens.Add(os);
                        }
                    }
                }
            }
            catch (SQLiteException ex)
            {
                NotificationManager.Show($"Erro ao carregar ordens de serviço: {ex.Message}", NotificationType.Error);
            }
            AplicarFiltroOrdens();
        }
        private void AplicarFiltroOrdens()
        {
            string searchTextAberta = txtPesquisaOrdemAberta.Text.ToLower().Trim();
            string searchTextConcluida = txtPesquisaOrdemConcluida.Text.ToLower().Trim();

            dgOrdensAbertas.ItemsSource = _ordens.Where(o => o.Status != "Fechada" &&
                                                              (string.IsNullOrWhiteSpace(searchTextAberta) ||
                                                                   o.NumeroOS.ToLower().Contains(searchTextAberta) ||
                                                                o.NomeClienteDisplay.ToLower().Contains(searchTextAberta) ||

                                                                 o.MarcaEquipamento.ToLower().Contains(searchTextAberta) ||
                                                           (o.TecnicoResponsavel != null && o.TecnicoResponsavel.ToLower().Contains(searchTextAberta)))).ToList();
            dgOrdensConcluidas.ItemsSource = _ordens.Where(o => o.Status == "Fechada" &&
                                                                  (string.IsNullOrWhiteSpace(searchTextConcluida) ||
                                                                       o.NumeroOS.ToLower().Contains(searchTextConcluida) ||
                                                                    o.NomeClienteDisplay.ToLower().Contains(searchTextConcluida) ||
                                                                   o.MarcaEquipamento.ToLower().Contains(searchTextConcluida) ||
                                                                     (o.TecnicoResponsavel != null && o.TecnicoResponsavel.ToLower().Contains(searchTextConcluida)))).ToList();
        }
        private void txtPesquisaOrdem_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltroOrdens();
        }
        private void btnEditarOrdemAberta_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.DataContext is OrdemDeServico selectedOS)) return;
            txtNumeroOS.Text = selectedOS.NumeroOS;
            if (selectedOS.ClienteId.HasValue)
            {
                cmbClienteOS.SelectedValue = selectedOS.ClienteId.Value;
            }
            else
            {
                cmbClienteOS.SelectedIndex = -1;
                txtNomeClienteOS.Text = selectedOS.NomeClienteOS;
            }

            txtMarcaEquipamento.Text = selectedOS.MarcaEquipamento;
            txtModeloEquipamento.Text = selectedOS.ModeloEquipamento;
            txtNumeroSerie.Text = selectedOS.NumeroSerie;
            txtDefeitoReclamado.Text = selectedOS.DefeitoReclamado;
            txtDefeitoReal.Text = selectedOS.DefeitoReal;
            txtServicoExecutado.Text = selectedOS.ServicoExecutado;
            txtValorOS.Text = selectedOS.Valor.ToString(CultureInfo.InvariantCulture);
            cmbTecnicoResponsavel.SelectedItem = selectedOS.TecnicoResponsavel;
            _currentOsId = selectedOS.Id;

            txtAparelho.Text = selectedOS.Aparelho;

            // Lógica de garantia simplificada para não mostrar o tempo de garantia.
            if (!string.IsNullOrEmpty(selectedOS.Garantia))
            {
                if (selectedOS.Garantia.StartsWith("Sim"))
                {
                    cmbGarantia.SelectedItem = "Sim";
                }
                else
                {
                    cmbGarantia.SelectedItem = "Não";
                }
            }
            else
            {
                cmbGarantia.SelectedIndex = -1;
            }
            spTempoGarantia.Visibility = Visibility.Collapsed; // Garante que o campo de tempo esteja sempre oculto.
            cmbTempoGarantia.SelectedIndex = -1;


            cmbVoltagem.SelectedItem = selectedOS.Voltagem;
            txtCor.Text = selectedOS.Cor;
            cmbDepartamento.SelectedItem = selectedOS.Departamento;

            txtComplemento.Text = selectedOS.Complemento;
            txtObservacao.Text = selectedOS.Observacao;

            MainTabControl.SelectedItem = OrdemServicoTabItem;
        }

        private void btnExcluirOrdemAberta_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.DataContext is OrdemDeServico selectedOS)) return;
            var confirmBox = new CustomMessageBox($"Tem certeza que deseja excluir permanentemente a OS '{selectedOS.NumeroOS}'?", "Confirmar Exclusão", MessageBoxButton.OKCancel);
            confirmBox.Owner = this;
            if (confirmBox.ShowDialog() == true && confirmBox.Result == MessageBoxResult.OK)
            {
                try
                {
                    using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                    {

                        connection.Open();
                        var command = new SQLiteCommand("DELETE FROM OrdensDeServico WHERE Id = @Id", connection);
                        command.Parameters.AddWithValue("@Id", selectedOS.Id);
                        command.ExecuteNonQuery();
                        NotificationManager.Show("Ordem de Serviço excluída com sucesso!", NotificationType.Success);

                        if (selectedOS.Id == _currentOsId)
                        {
                            LimparCamposOS();
                        }
                        CarregarOrdens();
                        AtualizarDashboard();
                    }
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Ocorreu um erro ao excluir a Ordem de Serviço: {ex.Message}", NotificationType.Error);
                }
            }
        }
        private void btnFecharOrdemAberta_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.DataContext is OrdemDeServico selectedOS)) return;

            // Lista de status que permitem fechar a OS sem cobrar nada (e sem adicionar gastos).
            var noChargeStatuses = new List<string> { "Orçamento Negado", "Sem Conserto (Devolução)", "Sem Conserto (Descarte)" };
            bool isNoChargeClosure = noChargeStatuses.Contains(selectedOS.Status);

            // Se o status for "Manutenção concluída", abre a nova janela de gastos.
            if (selectedOS.Status == "Manutenção concluída")
            {
                var gastosWindow = new AdicionarGastosOSWindow(selectedOS, _currentUsername);
                gastosWindow.Owner = this;
                bool? result = gastosWindow.ShowDialog();

                if (result == true)
                {
                    // A janela de gastos já fez todo o trabalho. A MainWindow só precisa atualizar.
                    NotificationManager.Show($"OS '{selectedOS.NumeroOS}' fechada com sucesso e gastos registrados!", NotificationType.Success);
                    CarregarOrdens();
                    CarregarCaixa();
                    CarregarComprasGastos();
                    AtualizarDashboard();
                }
                // Se o resultado for 'false' ou 'null', o usuário cancelou, então não fazemos nada.
            }
            // Se for um fechamento sem cobrança, mantém a lógica antiga.
            else if (isNoChargeClosure)
            {
                string confirmationMessage = $"Deseja realmente fechar a OS '{selectedOS.NumeroOS}' com o status '{selectedOS.Status}'?\n\nNenhum valor será lançado no caixa.";
                var confirmBox = new CustomMessageBox(confirmationMessage, "Confirmar Fechamento", MessageBoxButton.OKCancel);
                confirmBox.Owner = this;

                if (confirmBox.ShowDialog() == true && confirmBox.Result == MessageBoxResult.OK)
                {
                    try
                    {
                        using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                        {
                            connection.Open();
                            var commandOS = new SQLiteCommand("UPDATE OrdensDeServico SET Status = 'Fechada', DataFechamento = @DataFechamento WHERE Id = @Id", connection);
                            commandOS.Parameters.AddWithValue("@DataFechamento", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            commandOS.Parameters.AddWithValue("@Id", selectedOS.Id);
                            commandOS.ExecuteNonQuery();

                            NotificationManager.Show($"OS '{selectedOS.NumeroOS}' fechada com sucesso!", NotificationType.Success);
                            CarregarOrdens();
                            CarregarCaixa();
                            AtualizarDashboard();
                        }
                    }
                    catch (Exception ex)
                    {
                        NotificationManager.Show($"Ocorreu um erro ao fechar a OS: {ex.Message}", NotificationType.Error);
                    }
                }
            }
            // Se o status não for nenhum dos permitidos, o botão já estará desabilitado pelo XAML, mas esta é uma segurança extra.
            else
            {
                MessageBox.Show("O status da OS não permite o fechamento direto. Altere para 'Manutenção concluída' ou outro status final.", "Ação Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void cmbStatusOS_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count == 0)
            {
                return;
            }

            if (sender is ComboBox cmb && cmb.DataContext is OrdemDeServico os)
            {
                string novoStatus = os.Status;
                try
                {
                    using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                    {
                        connection.Open();
                        var command = new SQLiteCommand("UPDATE OrdensDeServico SET Status = @Status WHERE Id = @Id", connection);
                        command.Parameters.AddWithValue("@Status", novoStatus);
                        command.Parameters.AddWithValue("@Id", os.Id);
                        command.ExecuteNonQuery();
                    }

                    dgOrdensAbertas.Items.Refresh();
                    NotificationManager.Show($"Status da OS {os.NumeroOS} atualizado para '{novoStatus}'.", NotificationType.Success);
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Erro ao atualizar status: {ex.Message}", NotificationType.Error);
                }
            }
        }

        private void btnVerDetalhesOS_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.DataContext is OrdemDeServico selectedOS)) return;
            Cliente? cliente = null;
            if (selectedOS.ClienteId.HasValue)
            {
                cliente = _clientes.FirstOrDefault(c => c.Id == selectedOS.ClienteId.Value);
            }

            var detailsWindow = new DetalhesOSWindow(selectedOS, cliente, _configuracaoAtual);
            detailsWindow.Owner = this;
            detailsWindow.ShowDialog();
        }

        private void btnEditarOrdemConcluida_Click(object sender, RoutedEventArgs e)
        {
            btnEditarOrdemAberta_Click(sender, e);
        }


        #endregion

        #region Caixa
        public class ResumoResponsavel
        {
            public string Nome { get; set; }
            public double TotalEntradas { get; set; }
            public double TotalSaidas { get; set; }
        }

        private void CarregarCaixa()
        {
            _lancamentosCaixa.Clear();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    // A correção está na linha SELECT abaixo. Garanta que seja "c.AcertoId" e não "c.Acertold"
                    string query = @"
                SELECT 
                    c.Id, c.Data, c.Descricao, c.Tipo, c.Valor, c.OrdemServicoId, c.CompraGastoId, c.AcertoId,
                    os.NumeroOS,
                    COALESCE(cl.Nome, os.NomeClienteOS) AS NomeCliente,
                    COALESCE(os.TecnicoResponsavel, cg.Responsavel) AS ResponsavelTransacao
                FROM Caixa c
                LEFT JOIN OrdensDeServico os ON c.OrdemServicoId = os.Id
                LEFT JOIN Clientes cl ON os.ClienteId = cl.Id
                LEFT JOIN ComprasGastos cg ON c.CompraGastoId = cg.Id
                ORDER BY c.Data DESC";
                    var commandCaixa = new SQLiteCommand(query, connection);

                    using (var reader = commandCaixa.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var lancamento = new LancamentoCaixa
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                Data = DateTime.Parse(reader.GetString(reader.GetOrdinal("Data"))),
                                Descricao = reader.GetString(reader.GetOrdinal("Descricao")),
                                Tipo = reader.GetString(reader.GetOrdinal("Tipo")),
                                Valor = reader.GetDouble(reader.GetOrdinal("Valor")),
                                OrdemServicoId = reader.IsDBNull(reader.GetOrdinal("OrdemServicoId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("OrdemServicoId")),
                                CompraGastoId = reader.IsDBNull(reader.GetOrdinal("CompraGastoId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CompraGastoId")),
                                AcertoId = reader.IsDBNull(reader.GetOrdinal("AcertoId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("AcertoId")),
                                NumeroOSAssociada = reader.IsDBNull(reader.GetOrdinal("NumeroOS")) ? null : reader.GetString(reader.GetOrdinal("NumeroOS")),
                                NomeClienteAssociado = reader.IsDBNull(reader.GetOrdinal("NomeCliente")) ? null : reader.GetString(reader.GetOrdinal("NomeCliente")),
                                ResponsavelTransacao = reader.IsDBNull(reader.GetOrdinal("ResponsavelTransacao")) ? null : reader.GetString(reader.GetOrdinal("ResponsavelTransacao"))
                            };
                            _lancamentosCaixa.Add(lancamento);
                        }
                    }
                }
            }
            catch (SQLiteException ex)
            {
                NotificationManager.Show($"Erro ao carregar lançamentos do caixa: {ex.Message}", NotificationType.Error);
            }

            AplicarFiltroCaixa(null, null, null);
        }

        private void AplicarFiltroCaixa(DateTime? dataInicio, DateTime? dataFim, string tecnico)
        {
            IEnumerable<LancamentoCaixa> lancamentosFiltrados = _lancamentosCaixa;
            if (dataInicio.HasValue)
            {
                lancamentosFiltrados = lancamentosFiltrados.Where(l => l.Data.Date >= dataInicio.Value.Date);
            }
            if (dataFim.HasValue)
            {
                lancamentosFiltrados = lancamentosFiltrados.Where(l => l.Data.Date <= dataFim.Value.Date);
            }
            if (!string.IsNullOrEmpty(tecnico) && tecnico != "Todos")
            {
                lancamentosFiltrados = lancamentosFiltrados.Where(l => l.ResponsavelTransacao == tecnico);
            }

            var listaFinal = lancamentosFiltrados.ToList();
            dgCaixa.ItemsSource = listaFinal;
            double totalGanho = listaFinal.Where(l => l.Tipo == "Entrada").Sum(l => l.Valor);
            double totalGasto = listaFinal.Where(l => l.Tipo == "Saida").Sum(l => l.Valor);
            double saldo = totalGanho - totalGasto;

            txtTotalGanho.Text = totalGanho.ToString("C");
            txtTotalGasto.Text = totalGasto.ToString("C");
            txtSaldoCaixa.Text = saldo.ToString("C");
            txtSaldoCaixa.Foreground = saldo >= 0 ? Brushes.Blue : Brushes.Red;

            // Calcula e exibe o resumo por responsável
            var resumo = listaFinal
                .Where(l => !string.IsNullOrEmpty(l.ResponsavelTransacao))
                .GroupBy(l => l.ResponsavelTransacao)
                .Select(g => new ResumoResponsavel
                {
                    Nome = g.Key,
                    TotalEntradas = g.Where(l => l.Tipo == "Entrada").Sum(l => l.Valor),
                    TotalSaidas = g.Where(l => l.Tipo == "Saida").Sum(l => l.Valor)
                })
                                .OrderBy(r => r.Nome)
                .ToList();
            pnlResumoPorResponsavel.ItemsSource = resumo;
        }

        private void btnFiltrarCaixa_Click(object sender, RoutedEventArgs e)
        {
            if (dpDataInicioCaixa.SelectedDate != null && dpDataFimCaixa.SelectedDate != null && dpDataInicioCaixa.SelectedDate > dpDataFimCaixa.SelectedDate)
            {
                ShowValidationError("A data de início não pode ser maior que a data de fim.");
                return;
            }
            AplicarFiltroCaixa(dpDataInicioCaixa.SelectedDate, dpDataFimCaixa.SelectedDate, cmbFiltroTecnico.SelectedItem as string);
        }

        private void FiltroCaixa_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) // Evita que o evento dispare durante a inicialização
            {
                AplicarFiltroCaixa(dpDataInicioCaixa.SelectedDate, dpDataFimCaixa.SelectedDate, cmbFiltroTecnico.SelectedItem as string);
            }
        }

        private void btnLimparFiltroCaixa_Click(object sender, RoutedEventArgs e)
        {
            dpDataInicioCaixa.SelectedDate = null;
            dpDataFimCaixa.SelectedDate = null;
            cmbFiltroTecnico.SelectedIndex = 0; // "Todos"
            AplicarFiltroCaixa(null, null, null);
        }

        private void btnFiltro7Dias_Click(object sender, RoutedEventArgs e)
        {
            dpDataFimCaixa.SelectedDate = DateTime.Now;
            dpDataInicioCaixa.SelectedDate = DateTime.Now.AddDays(-6);
            btnFiltrarCaixa_Click(sender, e);
        }

        private void btnFiltro30Dias_Click(object sender, RoutedEventArgs e)
        {
            dpDataFimCaixa.SelectedDate = DateTime.Now;
            dpDataInicioCaixa.SelectedDate = DateTime.Now.AddDays(-29);
            btnFiltrarCaixa_Click(sender, e);
        }

        private void btnImprimirRelatorioCaixa_Click(object sender, RoutedEventArgs e)
        {
            if (_configuracaoAtual == null) return;
            var lancamentosParaImprimir = dgCaixa.ItemsSource as List<LancamentoCaixa>;
            if (lancamentosParaImprimir == null || !lancamentosParaImprimir.Any())
            {
                ShowValidationError("Não há dados para imprimir no período selecionado.");
                return;
            }

            DateTime dataInicio = dpDataInicioCaixa.SelectedDate ??
            lancamentosParaImprimir.Min(l => l.Data);
            DateTime dataFim = dpDataFimCaixa.SelectedDate ?? lancamentosParaImprimir.Max(l => l.Data);
            var viewModel = new RelatorioCaixaViewModel
            {
                LancamentosDoPeriodo = lancamentosParaImprimir,
                DataInicio = dataInicio,
                DataFim = dataFim,
                TotalEntradas = lancamentosParaImprimir.Where(l => l.Tipo == "Entrada").Sum(l => l.Valor),
                TotalSaidas = lancamentosParaImprimir.Where(l => l.Tipo == "Saida").Sum(l => l.Valor),
                Configuracao = _configuracaoAtual
            };
            var relatorioWindow = new RelatorioCaixaView(viewModel);

            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                relatorioWindow.Show();
                printDialog.PrintVisual(relatorioWindow.PrintableArea, $"Relatório de Caixa - {viewModel.Configuracao.NomeEmpresa}");
                relatorioWindow.Close();
            }
        }
        #endregion

        #region Compras/Gastos
        private void CarregarOrdensParaAssociar()
        {
            cmbAssociarOSCompraGasto.ItemsSource = null;
            cmbAssociarOSCompraGasto.ItemsSource = _ordens;
        }

        private void cmbTipoCompraGasto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tipo = cmbTipoCompraGasto.SelectedItem as string;
            if (tipo == "Compra" || tipo == "Receita Extra")
            {
                spAssociarOS.Visibility = Visibility.Visible;
            }
            else
            {
                spAssociarOS.Visibility = Visibility.Collapsed;
                cmbAssociarOSCompraGasto.SelectedIndex = -1;
            }
        }

        private void CarregarComprasGastos()
        {
            _comprasGastos.Clear();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT Id, Data, Descricao, Tipo, Valor, Responsavel FROM ComprasGastos ORDER BY Data DESC", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _comprasGastos.Add(new CompraGasto
                            {
                                Id = reader.GetInt32(0),
                                Data = DateTime.Parse(reader.GetString(1)),
                                Descricao = reader.GetString(2),
                                Tipo = reader.GetString(3),
                                Valor = reader.GetDouble(4),
                                Responsavel = reader.IsDBNull(5) ? null : reader.GetString(5)
                            });
                        }
                    }
                }
            }
            catch (SQLiteException ex)
            {
                NotificationManager.Show($"Erro ao carregar compras/gastos: {ex.Message}", NotificationType.Error);
            }

            dgComprasGastos.ItemsSource = null;
            dgComprasGastos.ItemsSource = _comprasGastos;
        }

        private void btnRegistrarCompraGasto_Click(object sender, RoutedEventArgs e)
        {
            var errors = new List<string>();
            if (dpDataCompraGasto.SelectedDate == null) errors.Add("• O campo 'Data' é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtDescricaoCompraGasto.Text)) errors.Add("• O campo 'Descrição' é obrigatório.");
            if (cmbTipoCompraGasto.SelectedItem == null) errors.Add("• O campo 'Tipo' é obrigatório.");
            if (cmbResponsavelCompraGasto.SelectedItem == null) errors.Add("• O campo 'Responsável' é obrigatório.");
            if (string.IsNullOrWhiteSpace(txtValorCompraGasto.Text)) errors.Add("• O campo 'Valor' é obrigatório.");

            if (errors.Any())
            {
                ShowValidationError("Por favor, corrija os seguintes erros:\n\n" + string.Join("\n", errors));
                return;
            }

            if (!double.TryParse(txtValorCompraGasto.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double valor) || valor <= 0)
            {
                ShowValidationError("O valor deve ser um número positivo válido.");
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        DateTime dataSelecionada = dpDataCompraGasto.SelectedDate.Value;
                        DateTime dataEHoraCompletas = dataSelecionada.Date.Add(DateTime.Now.TimeOfDay);
                        string dataFormatadaParaBanco = dataEHoraCompletas.ToString("yyyy-MM-dd HH:mm:ss");

                        var commandGasto = new SQLiteCommand("INSERT INTO ComprasGastos (Data, Descricao, Tipo, Valor, Responsavel) VALUES (@Data, @Descricao, @Tipo, @Valor, @Responsavel); SELECT last_insert_rowid();", connection);
                        commandGasto.Parameters.AddWithValue("@Data", dataFormatadaParaBanco);
                        commandGasto.Parameters.AddWithValue("@Descricao", txtDescricaoCompraGasto.Text.Trim());
                        string tipo = cmbTipoCompraGasto.SelectedItem as string ?? "";
                        commandGasto.Parameters.AddWithValue("@Tipo", tipo);
                        commandGasto.Parameters.AddWithValue("@Valor", valor);
                        commandGasto.Parameters.AddWithValue("@Responsavel", cmbResponsavelCompraGasto.SelectedItem as string);
                        long gastoId = (long)commandGasto.ExecuteScalar();

                        int? osId = null;
                        string osInfo = "";
                        if (spAssociarOS.Visibility == Visibility.Visible && cmbAssociarOSCompraGasto.SelectedItem is OrdemDeServico os)
                        {
                            osId = os.Id;
                            osInfo = $" (OS: {os.NumeroOS} - {os.NomeClienteDisplay})";
                        }

                        string tipoCaixa = (tipo == "Compra" || tipo == "Gasto") ?
                        "Saida" : "Entrada";
                        string descricaoCaixa;

                        if (tipo == "Receita Extra")
                            descricaoCaixa = $"Recebimento (receita extra): {txtDescricaoCompraGasto.Text.Trim()}{osInfo}";
                        else if (tipo == "Compra")
                            descricaoCaixa = $"Pagamento de compra: {txtDescricaoCompraGasto.Text.Trim()}{osInfo}";
                        else
                            descricaoCaixa = $"Pagamento de gasto: {txtDescricaoCompraGasto.Text.Trim()}";
                        var commandCaixa = new SQLiteCommand("INSERT INTO Caixa (Data, Descricao, Tipo, Valor, CompraGastoId, OrdemServicoId) VALUES (@Data, @Descricao, @Tipo, @Valor, @CompraGastoId, @OrdemServicoId)", connection);
                        commandCaixa.Parameters.AddWithValue("@Data", dataFormatadaParaBanco);
                        commandCaixa.Parameters.AddWithValue("@Descricao", descricaoCaixa);
                        commandCaixa.Parameters.AddWithValue("@Tipo", tipoCaixa);
                        commandCaixa.Parameters.AddWithValue("@Valor", valor);
                        commandCaixa.Parameters.AddWithValue("@CompraGastoId", gastoId);
                        commandCaixa.Parameters.AddWithValue("@OrdemServicoId", osId.HasValue ? (object)osId.Value : DBNull.Value);
                        commandCaixa.ExecuteNonQuery();

                        transaction.Commit();
                    }

                    NotificationManager.Show("Registro salvo e caixa atualizado com sucesso!", NotificationType.Success);
                    dpDataCompraGasto.SelectedDate = DateTime.Now;
                    txtDescricaoCompraGasto.Text = string.Empty;
                    cmbTipoCompraGasto.SelectedIndex = -1;
                    cmbResponsavelCompraGasto.SelectedIndex = -1;
                    txtValorCompraGasto.Text = string.Empty;
                    cmbAssociarOSCompraGasto.SelectedIndex = -1;
                    spAssociarOS.Visibility = Visibility.Collapsed;

                    CarregarComprasGastos();
                    CarregarCaixa();
                    AtualizarDashboard();
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Ocorreu um erro inesperado: {ex.Message}", NotificationType.Error);
            }
        }

        private void btnExcluirCompraGasto_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.DataContext is CompraGasto selectedGasto)) return;
            var confirmBox = new CustomMessageBox($"Tem certeza que deseja excluir o registro:\n'{selectedGasto.Descricao}' - {selectedGasto.Valor:C}?", "Confirmar Exclusão", MessageBoxButton.OKCancel);
            confirmBox.Owner = this;
            if (confirmBox.ShowDialog() == true && confirmBox.Result == MessageBoxResult.OK)
            {
                try
                {
                    using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            var commandCaixa = new SQLiteCommand("DELETE FROM Caixa WHERE CompraGastoId = @GastoId", connection);
                            commandCaixa.Parameters.AddWithValue("@GastoId", selectedGasto.Id);
                            commandCaixa.ExecuteNonQuery();

                            var commandGasto = new SQLiteCommand("DELETE FROM ComprasGastos WHERE Id = @Id", connection);
                            commandGasto.Parameters.AddWithValue("@Id", selectedGasto.Id);
                            commandGasto.ExecuteNonQuery();

                            transaction.Commit();
                        }

                        NotificationManager.Show("Registro de compra/gasto excluído com sucesso!", NotificationType.Success);
                        CarregarComprasGastos();
                        CarregarCaixa();
                        AtualizarDashboard();
                    }
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Ocorreu um erro ao excluir o registro: {ex.Message}", NotificationType.Error);
                }
            }
        }
        #endregion

        #region Configurações e Backup Automático

        private void CarregarConfiguracoesBackup()
        {
            try
            {
                bool autoBackupEnabled = Properties.Settings.Default.AutoBackupEnabled;
                chkAutoBackup.IsChecked = autoBackupEnabled;

                string destination = Properties.Settings.Default.AutoBackupDestination;
                if (destination == "Local")
                {
                    rbLocal.IsChecked = true;
                }
                else
                {
                    rbDrive.IsChecked = true;
                }

                if (autoBackupEnabled)
                {
                    _autoBackupTimer.Start();
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao carregar configurações de backup: {ex.Message}", NotificationType.Error);
            }
        }

        private void InicializarTimerBackup()
        {
            _autoBackupTimer = new DispatcherTimer();
            _autoBackupTimer.Interval = TimeSpan.FromMinutes(5);
            _autoBackupTimer.Tick += AutoBackupTimer_Tick;
        }

        private async Task<bool> HandleDriveSelectionAsync()
        {
            bool isDriveReady = await IsGoogleDriveReadyAsync();
            if (!isDriveReady)
            {
                var errorBox = new CustomMessageBox(
                    "A conta do Google Drive não está vinculada ou não foi possível conectar.\n\n" +
                    "Por favor, vincule a conta realizando um backup manual no Google Drive antes de ativar o backup automático.",
                    "Google Drive Não Vinculado",
                    MessageBoxButton.OK
                );
                errorBox.Owner = this;
                errorBox.ShowDialog();
                return false;
            }
            return true;
        }

        private async void chkAutoBackup_Checked(object sender, RoutedEventArgs e)
        {
            if (rbDrive.IsChecked == true)
            {
                if (!await HandleDriveSelectionAsync())
                {

                    // Reverte a ação do usuário e para a execução.
                    await Dispatcher.BeginInvoke(new Action(() => {
                        chkAutoBackup.IsChecked = false;
                        rbLocal.IsChecked = true;
                    }), DispatcherPriority.Input);
                    return;
                }
            }

            // Lógica para ativar o backup se a verificação passar.
            _autoBackupTimer.Start();
            Properties.Settings.Default.AutoBackupEnabled = true;
            Properties.Settings.Default.Save();
            NotificationManager.Show("Backup automático ativado.", NotificationType.Success);
        }

        private void chkAutoBackup_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoBackupTimer.Stop();
            Properties.Settings.Default.AutoBackupEnabled = false;
            Properties.Settings.Default.Save();
            NotificationManager.Show("Backup automático foi desativado.", NotificationType.Warning);
        }

        private async void rbDestination_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded || !(sender is RadioButton rb && rb.IsChecked == true)) return;
            // Se o usuário tentar mudar para o Drive enquanto o backup automático já está ligado,
            // precisamos verificar a conexão imediatamente.
            if (rb == rbDrive && chkAutoBackup.IsChecked == true)
            {
                if (!await HandleDriveSelectionAsync())
                {
                    // Reverte a seleção para Local se a verificação falhar.
                    rbLocal.IsChecked = true;
                    return;
                }
            }

            // Salva a nova preferência de destino.
            string newDestination = rbLocal.IsChecked == true ? "Local" : "Drive";
            if (Properties.Settings.Default.AutoBackupDestination != newDestination)
            {
                Properties.Settings.Default.AutoBackupDestination = newDestination;
                Properties.Settings.Default.Save();
            }
        }

        private async void AutoBackupTimer_Tick(object? sender, EventArgs e)
        {
            if (!_autoBackupTimer.IsEnabled || chkAutoBackup.IsChecked == false)
            {
                _autoBackupTimer.Stop();
                return;
            }

            try
            {
                if (rbLocal.IsChecked == true)
                {
                    await RealizarBackupLocalAsync(true);
                }
                else if (rbDrive.IsChecked == true)
                {
                    await RealizarBackupDriveAsync(true);
                }

                NotificationManager.Show("Backup automático periódico realizado com sucesso!", NotificationType.Success, 4);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no backup automático: {ex.Message}");
                NotificationManager.Show($"Falha no backup automático: {ex.Message}", NotificationType.Error, 10);
            }
        }

        #endregion

        #region Importar/Exportar e Helpers de Backup

        private async Task<bool> IsGoogleDriveReadyAsync()
        {
            try
            {
                UserCredential credential;
                await using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
                {
                    string credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Drive.Api.Oficina");
                    var dataStore = new FileDataStore(credPath, true);

                    var token = await dataStore.GetAsync<TokenResponse>("user");
                    if (token == null)
                    {
                        return false;
                        // Conta nunca foi vinculada.
                    }

                    var flow = new GoogleAuthorizationCodeFlow(
                        new GoogleAuthorizationCodeFlow.Initializer
                        {
                            ClientSecrets = GoogleClientSecrets.FromStream(stream).Secrets,
                            Scopes = Scopes,
                            DataStore = dataStore
                        });
                    credential = new UserCredential(flow, "user", token);

                    bool needsRefresh = credential.Token.IsExpired(flow.Clock);
                    if (needsRefresh)
                    {
                        if (!await credential.RefreshTokenAsync(CancellationToken.None))
                        {
                            return false; // Falha ao renovar o token (revogado ou sem internet).
                        }
                    }
                }

                var service = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });
                var request = service.About.Get();
                request.Fields = "user";
                await request.ExecuteAsync();

                return true;
            }
            catch (Exception)
            {
                // Qualquer exceção significa que não está pronto.
                return false;
            }
        }

        private async void btnExportarDados_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Arquivo de Banco de Dados (*.db)|*.db",
                Title = "Salvar Backup do Banco de Dados",
                FileName = $"Backup_OficinaDB_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                await RealizarBackupLocalAsync(false, saveFileDialog.FileName);
            }
        }

        private async void btnSalvarGoogleDrive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await RealizarBackupDriveAsync(false);
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao salvar no Google Drive: {ex.Message}", NotificationType.Error);
            }
        }

        private async Task RealizarBackupLocalAsync(bool isAutoBackup, string? caminhoCompleto = null)
        {
            string backupPath;
            if (!isAutoBackup)
            {
                backupPath = caminhoCompleto!;
            }
            else
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string backupDir = Path.Combine(baseDir, "AutoBackup");
                Directory.CreateDirectory(backupDir);
                backupPath = Path.Combine(backupDir, $"AutoBackup_OficinaDB_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db");
            }

            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                await Task.Run(() => File.Copy(DbPath, backupPath, true));

                if (!isAutoBackup)
                {
                    NotificationManager.Show("Backup local salvo com sucesso!", NotificationType.Success);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Falha ao salvar backup local: {ex.Message}", ex);
            }
        }

        private async Task RealizarBackupDriveAsync(bool isAutoBackup)
        {
            if (!isAutoBackup)
            {
                NotificationManager.Show("Iniciando processo de backup no Google Drive...", NotificationType.Warning);
            }

            try
            {
                if (_googleDriveService == null)
                {
                    CancellationToken cancellationToken = isAutoBackup ?
                    new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token : CancellationToken.None;
                    _googleDriveService = await AuthenticateGoogleDriveAsync(cancellationToken);
                }

                string nomeEmpresa = _configuracaoAtual?.NomeEmpresa ??
                "Sistema Oficina";
                string parentFolderName = $"Backup DB - {nomeEmpresa}";
                string parentFolderId = await GetOrCreateFolderAsync(_googleDriveService, parentFolderName);

                string subFolderName = isAutoBackup ?
                "Auto Backup" : "Backup Manual";
                string finalFolderId = await GetOrCreateFolderAsync(_googleDriveService, subFolderName, parentFolderId);
                string backupFileName = $"{(isAutoBackup ? "AutoBackup" : "Backup")}_OficinaDB_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
                await UploadToGoogleDriveAsync(_googleDriveService, backupFileName, finalFolderId);

                if (!isAutoBackup)
                {
                    NotificationManager.Show($"Backup salvo em '{parentFolderName}/{subFolderName}' no Google Drive!", NotificationType.Success);
                }
            }
            catch (TokenResponseException ex)
            {
                _googleDriveService = null;
                throw new Exception("Falha de autenticação com o Google Drive. Tente o backup manual para autenticar novamente.", ex);
            }
            catch (OperationCanceledException)
            {
                _googleDriveService = null;
                throw new Exception("A autenticação com o Google Drive foi cancelada ou demorou demais.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("Falha de rede. Verifique sua conexão com a internet.", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Falha no backup do Google Drive: {ex.Message}", ex);
            }
        }

        private void btnImportarDados_Click(object sender, RoutedEventArgs e)
        {
            var confirmBox = new CustomMessageBox(
                "ATENÇÃO!\n\nVocê está prestes a substituir todos os dados atuais. " +
                "Esta ação não pode ser desfeita.\n\nO aplicativo será fechado após a importação. " +
                "Você precisará abri-lo novamente.\n\nDeseja continuar?",
                "Confirmar Importação de Backup",
                MessageBoxButton.OKCancel
            );
            confirmBox.Owner = this;

            if (confirmBox.ShowDialog() != true || confirmBox.Result != MessageBoxResult.OK)
            {
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivo de Banco de Dados (*.db)|*.db",
                Title = "Selecionar Backup para Importar"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    File.Copy(openFileDialog.FileName, DbPath, true);

                    var successBox = new CustomMessageBox(
                        "Banco de dados importado com sucesso!\n\nO aplicativo será reiniciado para carregar os novos dados.",
                        "Importação Concluída",
                        MessageBoxButton.OK
                           );
                    successBox.Owner = this;
                    successBox.ShowDialog();

                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    NotificationManager.Show($"Falha ao importar o banco de dados: {ex.Message}", NotificationType.Error);
                }
            }
        }

        #region Código do Google Drive (Helpers)

        private async Task<string> GetOrCreateFolderAsync(DriveService service, string folderName, string? parentId = null)
        {
            var listRequest = service.Files.List();
            string query = $"mimeType='application/vnd.google-apps.folder' and trashed=false and name='{folderName.Replace("'", "\\'")}'";
            if (parentId != null)
            {
                query += $" and '{parentId}' in parents";
            }
            else
            {
                query += " and 'root' in parents";
            }

            listRequest.Q = query;
            listRequest.Fields = "files(id, parents)";
            var result = await listRequest.ExecuteAsync();

            if (result.Files != null && result.Files.Any())
            {
                return result.Files.First().Id;
            }

            var folderMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            if (parentId != null)
            {
                folderMetadata.Parents = new List<string> { parentId };
            }

            var createRequest = service.Files.Create(folderMetadata);
            createRequest.Fields = "id";
            var folder = await createRequest.ExecuteAsync();

            return folder.Id;
        }

        private async Task<DriveService> AuthenticateGoogleDriveAsync(CancellationToken cancellationToken)
        {
            UserCredential credential;
            await using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Drive.Api.Oficina");
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    cancellationToken,
                    new FileDataStore(credPath, true)
                );
            }

            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
        }

        private async Task UploadToGoogleDriveAsync(DriveService service, string fileName, string folderId)
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new List<string> { folderId }
            };

            GC.Collect();
            GC.WaitForPendingFinalizers();

            FilesResource.CreateMediaUpload request;
            await using (var stream = new FileStream(DbPath, FileMode.Open, FileAccess.Read))
            {
                request = service.Files.Create(fileMetadata, stream, "application/vnd.sqlite3");
                request.Fields = "id";
                await request.UploadAsync();
            }
        }
        #endregion

        #endregion

        private void DataGrid_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dg)
            {
                dg.SelectedItem = null;
            }
        }

        // #################### INÍCIO DA NOVA SEÇÃO DE MÉTODOS ####################
        #region Acerto de Contas

        private void CarregarUsuariosParaAcerto()
        {
            _acertoUsuarios.Clear();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    // ALTERAÇÃO: Adicionada a condição "AND IncluirNoAcerto = 1" para buscar apenas usuários marcados
                    var command = new SQLiteCommand("SELECT Username FROM Users WHERE Role != 'Admin' AND IncluirNoAcerto = 1 ORDER BY Username", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _acertoUsuarios.Add(new AcertoUsuarioViewModel
                            {
                                Username = reader.GetString(0),
                                Percentage = 0,
                                ProfitShare = 0
                            });
                        }
                    }
                }
                dgGastosAcerto.ItemsSource = _gastosParaAcerto;
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao carregar usuários para o acerto: {ex.Message}", NotificationType.Error);
            }
        }

        private void btnAcertoPeriodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int dias))
            {
                CalcularPeriodoAcerto(dias);
            }
        }

        private void CalcularPeriodoAcerto(int dias)
        {
            // Limpa a seleção de gastos de um cálculo anterior
            _gastosParaAcerto.Clear();
            chkSelecionarTodosGastos.IsChecked = false;

            DateTime dataFim = DateTime.Now;
            DateTime dataInicio = dataFim.AddDays(-dias).Date;

            // Busca todos os lançamentos do período para calcular os totais
            var lancamentosPeriodo = _lancamentosCaixa
                .Where(l => l.Data.Date >= dataInicio && l.Data.Date <= dataFim.Date)
                .ToList();

            double totalEntradas = lancamentosPeriodo.Where(l => l.Tipo == "Entrada").Sum(l => l.Valor);
            double totalSaidas = lancamentosPeriodo.Where(l => l.Tipo == "Saida").Sum(l => l.Valor);
            _lucroPeriodoAcerto = totalEntradas - totalSaidas;

            txtAcertoTotalEntradas.Text = totalEntradas.ToString("C");
            txtAcertoTotalSaidas.Text = totalSaidas.ToString("C");
            txtAcertoLucroPeriodo.Text = _lucroPeriodoAcerto.ToString("C");
            txtAcertoLucroPeriodo.Foreground = _lucroPeriodoAcerto >= 0 ? Brushes.Green : Brushes.Red;

            // Busca apenas os gastos (saídas) que AINDA NÃO FORAM ACERTADOS
            var gastosNaoAcertados = _lancamentosCaixa
                .Where(l => l.Data.Date >= dataInicio && l.Data.Date <= dataFim.Date && l.Tipo == "Saida" && !l.AcertoId.HasValue)
                .ToList();

            // Adiciona os gastos encontrados na lista de seleção
            foreach (var gasto in gastosNaoAcertados)
            {
                _gastosParaAcerto.Add(new GastoSelecionavelViewModel(gasto));
            }

            // Reseta a tabela de acerto dos usuários
            foreach (var usuario in _acertoUsuarios)
            {
                usuario.ExpensesToReimburse = 0;
                usuario.ProfitShare = 0;
            }

            NotificationManager.Show($"Período de {dias} dias calculado. Selecione os gastos a serem reembolsados e clique em 'Atualizar Reembolsos'.", NotificationType.Success);
        }

        private void btnAtualizarReembolsos_Click(object sender, RoutedEventArgs e)
        {
            // Primeiro, zera todos os reembolsos para recalcular
            foreach (var usuario in _acertoUsuarios)
            {
                usuario.ExpensesToReimburse = 0;
            }

            // Soma apenas os gastos que foram selecionados pelo usuário
            foreach (var gastoVM in _gastosParaAcerto.Where(g => g.IsSelected))
            {
                // Encontra o usuário responsável pelo gasto
                var usuarioParaReembolsar = _acertoUsuarios.FirstOrDefault(u => u.Username == gastoVM.Lancamento.ResponsavelTransacao);
                if (usuarioParaReembolsar != null)
                {
                    // Adiciona o valor do gasto ao total de reembolso dele
                    usuarioParaReembolsar.ExpensesToReimburse += gastoVM.Lancamento.Valor;
                }
            }

            NotificationManager.Show("Valores de reembolso atualizados na tabela. Agora defina as porcentagens.", NotificationType.Success);
        }


        private void btnCalcularAcertoFinal_Click(object sender, RoutedEventArgs e)
        {
            double totalPercentage = _acertoUsuarios.Sum(u => u.Percentage);
            if (Math.Abs(totalPercentage - 100.0) > 0.01)
            {
                NotificationManager.Show("A soma das porcentagens dos usuários deve ser exatamente 100%.", NotificationType.Error);
                return;
            }

            if (string.IsNullOrEmpty(txtAcertoLucroPeriodo.Text) || txtAcertoLucroPeriodo.Text == "R$ 0,00")
            {
                NotificationManager.Show("Primeiro, calcule o período clicando em um dos botões (7, 15 ou 30 dias).", NotificationType.Warning);
                return;
            }

            // Calcula a parte no lucro para cada usuário
            foreach (var usuario in _acertoUsuarios)
            {
                usuario.ProfitShare = _lucroPeriodoAcerto * (usuario.Percentage / 100.0);
            }

            // Confirmação final antes de salvar no banco
            var confirmBox = new CustomMessageBox(
                "Você está prestes a fechar este acerto. Os gastos selecionados serão marcados como 'pagos' e não poderão ser incluídos em acertos futuros.\n\nDeseja continuar?",
                "Confirmar Fechamento do Acerto",
                MessageBoxButton.OKCancel
            );
            confirmBox.Owner = this;

            if (confirmBox.ShowDialog() != true || confirmBox.Result != MessageBoxResult.OK)
            {
                return;
            }

            // Lógica para salvar o acerto no banco de dados
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 1. Insere o registro do novo acerto na tabela Acertos
                        double totalReembolsado = _acertoUsuarios.Sum(u => u.ExpensesToReimburse);
                        var cmdAcerto = new SQLiteCommand("INSERT INTO Acertos (Data, LucroTotal, TotalReembolsado) VALUES (@Data, @Lucro, @Reembolso); SELECT last_insert_rowid();", connection);
                        cmdAcerto.Parameters.AddWithValue("@Data", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmdAcerto.Parameters.AddWithValue("@Lucro", _lucroPeriodoAcerto);
                        cmdAcerto.Parameters.AddWithValue("@Reembolso", totalReembolsado);
                        long novoAcertoId = (long)cmdAcerto.ExecuteScalar();

                        // 2. Atualiza cada gasto selecionado, marcando com o ID do novo acerto
                        foreach (var gastoVM in _gastosParaAcerto.Where(g => g.IsSelected))
                        {
                            var cmdUpdateCaixa = new SQLiteCommand("UPDATE Caixa SET AcertoId = @AcertoId WHERE Id = @CaixaId", connection);
                            cmdUpdateCaixa.Parameters.AddWithValue("@AcertoId", novoAcertoId);
                            cmdUpdateCaixa.Parameters.AddWithValue("@CaixaId", gastoVM.Lancamento.Id);
                            cmdUpdateCaixa.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }

                    NotificationManager.Show("Acerto finalizado e salvo com sucesso! O sistema foi atualizado.", NotificationType.Success);

                    // Limpa a UI para o próximo acerto
                    LimparTelaDeAcerto();
                    CarregarCaixa(); // Recarrega os dados do caixa para refletir as mudanças
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Ocorreu um erro ao finalizar o acerto: {ex.Message}", NotificationType.Error);
            }
        }

        private void LimparTelaDeAcerto()
        {
            _gastosParaAcerto.Clear();
            chkSelecionarTodosGastos.IsChecked = false;
            txtAcertoTotalEntradas.Text = "R$ 0,00";
            txtAcertoTotalSaidas.Text = "R$ 0,00";
            txtAcertoLucroPeriodo.Text = "R$ 0,00";

            foreach (var usuario in _acertoUsuarios)
            {
                usuario.Percentage = 0;
                usuario.ExpensesToReimburse = 0;
                usuario.ProfitShare = 0;
            }
        }

        // Eventos para o CheckBox "Selecionar Todos"
        private void chkSelecionarTodosGastos_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var item in _gastosParaAcerto)
            {
                item.IsSelected = true;
            }
        }

        private void chkSelecionarTodosGastos_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var item in _gastosParaAcerto)
            {
                item.IsSelected = false;
            }
        }

        #endregion
        // #################### FIM DA NOVA SEÇÃO DE MÉTODOS ####################

        // #################### SUBSTITUA TODA A REGIÃO DE AGENDAMENTO POR ESTA ####################
        #region Agendamento

        private void PopularHorasMinutos()
        {
            // Popula horas de 00 a 23
            cmbHoraAgendamento.ItemsSource = Enumerable.Range(0, 24).Select(h => h.ToString("00"));
            // Popula minutos com intervalos de 15
            cmbMinutoAgendamento.ItemsSource = new List<string> { "00", "15", "30", "45" };
        }

        private void InicializarTimerAgendamento()
        {
            _agendamentoTimer = new DispatcherTimer
            {
                // ATUALIZAÇÃO: Verifica a cada 1 minuto para maior precisão do alarme
                Interval = TimeSpan.FromMinutes(1)
            };
            _agendamentoTimer.Tick += AgendamentoTimer_Tick;
            _agendamentoTimer.Start();

            // Executa uma verificação inicial 5 segundos após iniciar o sistema
            var initialCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            initialCheckTimer.Tick += (s, e) =>
            {
                AgendamentoTimer_Tick(null, EventArgs.Empty);
                initialCheckTimer.Stop();
            };
            initialCheckTimer.Start();
        }

        private void AgendamentoTimer_Tick(object sender, EventArgs e)
        {
            DateTime agora = DateTime.Now;

            // ATUALIZAÇÃO: Lógica de verificação baseada em minutos
            var agendamentosProximos = _agendamentos
                .Where(a => a.Status == "Agendado" && // O agendamento deve estar ativo
                            !_notifiedAgendamentoIds.Contains(a.Id) && // Não pode ter sido notificado antes
                            a.DataAgendamento > agora && // A data/hora deve ser no futuro
                            (a.DataAgendamento - agora).TotalMinutes <= 30) // E deve estar nos próximos 30 minutos
                .ToList();

            if (!agendamentosProximos.Any()) return;

            foreach (var agendamento in agendamentosProximos)
            {
                string mensagem = $"ALERTA DE AGENDAMENTO PRÓXIMO!\n\n" +
                                  $"Cliente: {agendamento.NomeClienteDisplay}\n" +
                                  $"Coletar: {agendamento.Equipamento}\n" +
                                  $"Horário Marcado: {agendamento.DataAgendamento:dd/MM/yyyy 'às' HH:mm}";

                SystemSounds.Exclamation.Play();

                var alertBox = new CustomMessageBox(mensagem, "Alerta de Agendamento", MessageBoxButton.OK);
                alertBox.Owner = this;
                alertBox.ShowDialog();

                _notifiedAgendamentoIds.Add(agendamento.Id); // Marca como notificado
            }
        }

        // Adicione este novo método em qualquer lugar dentro da classe MainWindow
        private void AtualizarIndicadorAgendamentos()
        {
            var agendamentosOrdenados = _agendamentos.OrderBy(a => a.DataAgendamento).ToList();
            int contagem = agendamentosOrdenados.Count;

            // Atualiza a DataGrid da aba de agendamentos
            dgAgendamentos.ItemsSource = null;
            dgAgendamentos.ItemsSource = agendamentosOrdenados;

            // Atualiza o indicador do topo da janela
            var textBlockContagem = btnAgendamentosPendentes.Template.FindName("txtContagemAgendamentos", btnAgendamentosPendentes) as TextBlock;
            if (contagem > 0)
            {
                if (textBlockContagem != null)
                {
                    textBlockContagem.Text = contagem == 1 ? "1 Agendamento" : $"{contagem} Agendamentos";
                }
                btnAgendamentosPendentes.Visibility = Visibility.Visible;
                lvAgendamentosPopup.ItemsSource = agendamentosOrdenados;
            }
            else
            {
                btnAgendamentosPendentes.Visibility = Visibility.Collapsed;
                lvAgendamentosPopup.ItemsSource = null;
                btnAgendamentosPendentes.IsChecked = false;
            }
        }

        // ADICIONE este novo método para o botão do Dashboard
        private void btnDetalhesDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AgendamentoViewModel viewModel)
            {
                // O ViewModel segura o objeto Agendamento original, que passamos para o método de exibição
                ExibirDetalhesAgendamento(viewModel.Agendamento);
            }
        }

        // ADICIONE este novo método auxiliar para evitar código duplicado
        private void ExibirDetalhesAgendamento(Agendamento agendamento)
        {
            if (agendamento == null) return;

            Cliente? cliente = null;
            if (agendamento.ClienteId.HasValue)
            {
                cliente = _clientes.FirstOrDefault(c => c.Id == agendamento.ClienteId.Value);
            }

            var detailsWindow = new DetalhesAgendamentoWindow(agendamento, cliente);
            detailsWindow.Owner = this;
            detailsWindow.ShowDialog();
        }

        private void btnVerDetalhesAgendamento_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Agendamento selectedAgendamento)
            {
                ExibirDetalhesAgendamento(selectedAgendamento);
            }
        }

        private void txtCep_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            // Remove o manipulador para evitar chamadas recursivas
            tb.TextChanged -= txtCep_TextChanged;

            string text = new string(tb.Text.Where(char.IsDigit).ToArray());
            string formattedText = text;

            if (text.Length > 5)
            {
                formattedText = $"{text.Substring(0, 5)}-{text.Substring(5)}";
            }

            tb.Text = formattedText;
            tb.CaretIndex = tb.Text.Length;

            // Readiciona o manipulador
            tb.TextChanged += txtCep_TextChanged;
        }

        private void CarregarAgendamentos()
        {
            _agendamentos.Clear();
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT Id, ClienteId, NomeClienteAvulso, Equipamento, DataAgendamento, Status, Endereco, Numero, Bairro, Cidade, UF, CEP FROM Agendamentos WHERE Status = 'Agendado' ORDER BY DataAgendamento ASC", connection);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var agendamento = new Agendamento
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                ClienteId = reader.IsDBNull(reader.GetOrdinal("ClienteId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("ClienteId")),
                                NomeClienteAvulso = reader.IsDBNull(reader.GetOrdinal("NomeClienteAvulso")) ? null : reader.GetString(reader.GetOrdinal("NomeClienteAvulso")),
                                Equipamento = reader.GetString(reader.GetOrdinal("Equipamento")),
                                DataAgendamento = DateTime.Parse(reader.GetString(reader.GetOrdinal("DataAgendamento"))),
                                Status = reader.GetString(reader.GetOrdinal("Status")),
                                Endereco = reader.IsDBNull(reader.GetOrdinal("Endereco")) ? null : reader.GetString(reader.GetOrdinal("Endereco")),
                                Numero = reader.IsDBNull(reader.GetOrdinal("Numero")) ? null : reader.GetString(reader.GetOrdinal("Numero")),
                                Bairro = reader.IsDBNull(reader.GetOrdinal("Bairro")) ? null : reader.GetString(reader.GetOrdinal("Bairro")),
                                Cidade = reader.IsDBNull(reader.GetOrdinal("Cidade")) ? null : reader.GetString(reader.GetOrdinal("Cidade")),
                                UF = reader.IsDBNull(reader.GetOrdinal("UF")) ? null : reader.GetString(reader.GetOrdinal("UF")),
                                CEP = reader.IsDBNull(reader.GetOrdinal("CEP")) ? null : reader.GetString(reader.GetOrdinal("CEP"))
                            };

                            if (agendamento.ClienteId.HasValue)
                            {
                                var cliente = _clientes.FirstOrDefault(c => c.Id == agendamento.ClienteId.Value);
                                agendamento.NomeClienteCache = cliente?.Nome ?? "Cliente não encontrado";
                            }
                            _agendamentos.Add(agendamento);
                        }
                    }
                }
                dgAgendamentos.ItemsSource = null;
                dgAgendamentos.ItemsSource = _agendamentos;
            }
            catch (SQLiteException ex)
            {
                NotificationManager.Show($"Erro ao carregar agendamentos: {ex.Message}", NotificationType.Error);
            }
            AtualizarIndicadorAgendamentos();

            // Adicione/Confirme esta chamada no final
            CarregarAgendamentosDashboard();
        }

        private void CarregarClientesParaAgendamento()
        {
            cmbClienteAgendamento.ItemsSource = null;
            cmbClienteAgendamento.ItemsSource = _clientes;
            cmbClienteAgendamento.DisplayMemberPath = "Nome";
            cmbClienteAgendamento.SelectedValuePath = "Id";
        }

        private void cmbClienteAgendamento_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isAvulso = (cmbClienteAgendamento.SelectedItem == null);
            txtNomeClienteAgendamento.IsEnabled = isAvulso;

            // Garante que os campos de endereço estejam sempre editáveis
            txtEnderecoAgendamento.IsReadOnly = false;
            txtNumeroAgendamento.IsReadOnly = false;
            txtBairroAgendamento.IsReadOnly = false;
            txtCidadeAgendamento.IsReadOnly = false;
            txtUfAgendamento.IsReadOnly = false;
            txtCepAgendamento.IsReadOnly = false;

            // Limpa todos os campos de endereço antes de um novo preenchimento
            txtEnderecoAgendamento.Clear();
            txtNumeroAgendamento.Clear();
            txtBairroAgendamento.Clear();
            txtCidadeAgendamento.Clear();
            txtUfAgendamento.Clear();
            txtCepAgendamento.Clear();

            if (isAvulso)
            {
                txtNomeClienteAgendamento.Clear();
            }
            else if (cmbClienteAgendamento.SelectedItem is Cliente selectedCliente)
            {
                txtNomeClienteAgendamento.Clear();

                // Preenche automaticamente com os dados do cliente que existem no cadastro.
                // Os outros campos (Número, UF, CEP) permanecem em branco e editáveis.
                txtEnderecoAgendamento.Text = selectedCliente.Endereco;
                txtBairroAgendamento.Text = selectedCliente.Bairro;
                txtCidadeAgendamento.Text = selectedCliente.Cidade;
            }
        }

        private void cmbEquipamentoAgendamento_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbEquipamentoAgendamento.SelectedItem as string == "Outro")
            {
                txtOutroEquipamentoAgendamento.Visibility = Visibility.Visible;
            }
            else
            {
                txtOutroEquipamentoAgendamento.Visibility = Visibility.Collapsed;
                txtOutroEquipamentoAgendamento.Clear();
            }
        }

        private void btnSalvarAgendamento_Click(object sender, RoutedEventArgs e)
        {
            if (cmbClienteAgendamento.SelectedItem == null && string.IsNullOrWhiteSpace(txtNomeClienteAgendamento.Text))
            { ShowValidationError("Selecione um cliente cadastrado OU digite o nome de um cliente avulso."); return; }

            string equipamento = cmbEquipamentoAgendamento.SelectedItem as string;
            if (string.IsNullOrEmpty(equipamento))
            { ShowValidationError("Selecione um tipo de equipamento."); return; }

            if (equipamento == "Outro" && string.IsNullOrWhiteSpace(txtOutroEquipamentoAgendamento.Text))
            { ShowValidationError("Se o tipo for 'Outro', descreva o equipamento."); return; }

            if (dpDataAgendamento.SelectedDate == null || cmbHoraAgendamento.SelectedItem == null || cmbMinutoAgendamento.SelectedItem == null)
            { ShowValidationError("Selecione a data, hora e minuto para o agendamento."); return; }

            DateTime data = dpDataAgendamento.SelectedDate.Value;
            int hora = int.Parse(cmbHoraAgendamento.SelectedItem.ToString());
            int minuto = int.Parse(cmbMinutoAgendamento.SelectedItem.ToString());
            DateTime dataCompleta = new DateTime(data.Year, data.Month, data.Day, hora, minuto, 0);

            var agendamento = new Agendamento
            {
                ClienteId = (cmbClienteAgendamento.SelectedItem as Cliente)?.Id,
                NomeClienteAvulso = txtNomeClienteAgendamento.Text.Trim(),
                Equipamento = (equipamento == "Outro") ? txtOutroEquipamentoAgendamento.Text.Trim() : equipamento,
                DataAgendamento = dataCompleta,
                Status = "Agendado",
                Endereco = txtEnderecoAgendamento.Text.Trim(),
                Numero = txtNumeroAgendamento.Text.Trim(),
                Bairro = txtBairroAgendamento.Text.Trim(),
                Cidade = txtCidadeAgendamento.Text.Trim(),
                UF = txtUfAgendamento.Text.Trim().ToUpper(),
                CEP = txtCepAgendamento.Text.Trim()
            };

            if (agendamento.ClienteId.HasValue)
            {
                agendamento.NomeClienteCache = (cmbClienteAgendamento.SelectedItem as Cliente)?.Nome;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand(@"INSERT INTO Agendamentos (ClienteId, NomeClienteAvulso, Equipamento, DataAgendamento, Status, Endereco, Numero, Bairro, Cidade, UF, CEP) 
                                              VALUES (@ClienteId, @NomeClienteAvulso, @Equipamento, @DataAgendamento, @Status, @Endereco, @Numero, @Bairro, @Cidade, @UF, @CEP);
                                              SELECT last_insert_rowid();", connection);

                    command.Parameters.AddWithValue("@ClienteId", agendamento.ClienteId.HasValue ? (object)agendamento.ClienteId.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@NomeClienteAvulso", string.IsNullOrWhiteSpace(agendamento.NomeClienteAvulso) ? DBNull.Value : (object)agendamento.NomeClienteAvulso);
                    command.Parameters.AddWithValue("@Equipamento", agendamento.Equipamento);
                    command.Parameters.AddWithValue("@DataAgendamento", agendamento.DataAgendamento.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@Status", agendamento.Status);
                    command.Parameters.AddWithValue("@Endereco", string.IsNullOrWhiteSpace(agendamento.Endereco) ? DBNull.Value : (object)agendamento.Endereco);
                    command.Parameters.AddWithValue("@Numero", string.IsNullOrWhiteSpace(agendamento.Numero) ? DBNull.Value : (object)agendamento.Numero);
                    command.Parameters.AddWithValue("@Bairro", string.IsNullOrWhiteSpace(agendamento.Bairro) ? DBNull.Value : (object)agendamento.Bairro);
                    command.Parameters.AddWithValue("@Cidade", string.IsNullOrWhiteSpace(agendamento.Cidade) ? DBNull.Value : (object)agendamento.Cidade);
                    command.Parameters.AddWithValue("@UF", string.IsNullOrWhiteSpace(agendamento.UF) ? DBNull.Value : (object)agendamento.UF);
                    command.Parameters.AddWithValue("@CEP", string.IsNullOrWhiteSpace(agendamento.CEP) ? DBNull.Value : (object)agendamento.CEP);

                    long newId = (long)command.ExecuteScalar();
                    agendamento.Id = (int)newId;

                    _agendamentos.Add(agendamento);
                    AtualizarIndicadorAgendamentos();

                    // #################### LINHA DA CORREÇÃO ####################
                    CarregarAgendamentosDashboard(); // Atualiza a lista do dashboard
                                                     // #########################################################

                    NotificationManager.Show("Agendamento salvo com sucesso!", NotificationType.Success);
                    LimparCamposAgendamento();
                }
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Ocorreu um erro ao salvar o agendamento: {ex.Message}", NotificationType.Error);
            }
        }

        private void LimparCamposAgendamento()
        {
            cmbClienteAgendamento.SelectedIndex = -1;
            txtNomeClienteAgendamento.Clear();
            cmbEquipamentoAgendamento.SelectedIndex = -1;
            txtOutroEquipamentoAgendamento.Clear();
            txtOutroEquipamentoAgendamento.Visibility = Visibility.Collapsed;
            dpDataAgendamento.SelectedDate = DateTime.Now;
            cmbHoraAgendamento.SelectedIndex = -1;
            cmbMinutoAgendamento.SelectedIndex = -1;
            txtEnderecoAgendamento.Clear();
            txtNumeroAgendamento.Clear();
            txtBairroAgendamento.Clear();
            txtCidadeAgendamento.Clear();
            txtUfAgendamento.Clear();
            txtCepAgendamento.Clear();
        }

        private void AtualizarStatusAgendamento(Agendamento agendamento, string novoStatus)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("UPDATE Agendamentos SET Status = @Status WHERE Id = @Id", connection);
                    command.Parameters.AddWithValue("@Status", novoStatus);
                    command.Parameters.AddWithValue("@Id", agendamento.Id);
                    command.ExecuteNonQuery();
                }

                NotificationManager.Show($"Agendamento '{agendamento.Id}' atualizado para '{novoStatus}'.", NotificationType.Success);
                // Apenas recarrega a lista de agendamentos pendentes
                CarregarAgendamentos();
            }
            catch (Exception ex)
            {
                NotificationManager.Show($"Erro ao atualizar status do agendamento: {ex.Message}", NotificationType.Error);
            }
        }

        private void btnConcluirAgendamento_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Agendamento selectedAgendamento)
            {
                var confirmBox = new CustomMessageBox($"Deseja marcar como 'Concluído' o agendamento para o cliente '{selectedAgendamento.NomeClienteDisplay}'?", "Confirmar Conclusão", MessageBoxButton.OKCancel);
                confirmBox.Owner = this;
                if (confirmBox.ShowDialog() == true && confirmBox.Result == MessageBoxResult.OK)
                {
                    AtualizarStatusAgendamento(selectedAgendamento, "Concluído");
                }
            }
        }

        private void btnExcluirAgendamento_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Agendamento selectedAgendamento)
            {
                var confirmBox = new CustomMessageBox($"Tem certeza que deseja excluir permanentemente o agendamento para '{selectedAgendamento.NomeClienteDisplay}'?", "Confirmar Exclusão", MessageBoxButton.OKCancel);
                confirmBox.Owner = this;
                if (confirmBox.ShowDialog() == true && confirmBox.Result == MessageBoxResult.OK)
                {
                    AtualizarStatusAgendamento(selectedAgendamento, "Excluído");
                }
            }
        }

        #endregion
        // #################### FIM DA REGIÃO DE AGENDAMENTO ####################
    }


}