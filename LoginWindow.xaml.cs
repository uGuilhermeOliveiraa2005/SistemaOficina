using Microsoft.Win32;
using SistemaOficina.Helpers;
using System;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SistemaOficina
{
    public partial class LoginWindow : Window
    {
        private readonly string DbPath = Path.Combine(Directory.GetCurrentDirectory(), "OficinaDB.db");
        private const string SementeSecreta = "Abacaxi-Verde-33@!";

        public LoginWindow()
        {
            InitializeComponent();
            DatabaseHelper.InitializeDatabase();
            this.Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CheckRegistrationStatus();
        }

        // #################### INÍCIO DA ALTERAÇÃO ####################
        private void CheckRegistrationStatus()
        {
            bool hasUsers = false;
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT COUNT(1) FROM Users", connection);
                    long userCount = (long)command.ExecuteScalar();
                    if (userCount > 0)
                    {
                        hasUsers = true;
                    }
                }
            }
            catch (Exception ex)
            {
                txtLoginError.Text = $"Erro ao verificar usuários: {ex.Message}";
            }

            if (hasUsers)
            {
                // Se já existem usuários, esconde todas as opções secundárias.
                btnShowRegisterView.Visibility = Visibility.Collapsed;
                btnImportDatabase.Visibility = Visibility.Collapsed;
                textBlockSeparator.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Se não há usuários, é a primeira vez. Mostra as opções.
                btnShowRegisterView.Visibility = Visibility.Visible;
                btnImportDatabase.Visibility = Visibility.Visible;
                textBlockSeparator.Visibility = Visibility.Visible;
            }

            // Garante que a tela de Login seja sempre a inicial, e a de registro fique oculta.
            LoginGrid.Visibility = Visibility.Visible;
            RegisterGrid.Visibility = Visibility.Collapsed;
        }
        // #################### FIM DA ALTERAÇÃO ####################

        private void ShowRegisterView_Click(object sender, RoutedEventArgs e)
        {
            LoginGrid.Visibility = Visibility.Collapsed;
            RegisterGrid.Visibility = Visibility.Visible;
        }

        private void ShowLoginView_Click(object sender, RoutedEventArgs e)
        {
            RegisterGrid.Visibility = Visibility.Collapsed;
            LoginGrid.Visibility = Visibility.Visible;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = txtLoginUsername.Text;
            string password = pwdLoginPassword.Password;
            txtLoginError.Text = "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                txtLoginError.Text = "Usuário e senha são obrigatórios.";
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT PasswordHash, Role FROM Users WHERE Username = @username", connection);
                    command.Parameters.AddWithValue("@username", username);

                    string? storedHash = null;
                    string? role = null;

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            storedHash = reader.GetString(0);
                            role = reader.GetString(1);
                        }
                    }

                    if (storedHash != null && role != null && PasswordHelper.VerifyPassword(password, storedHash))
                    {
                        if (chkKeepLoggedIn.IsChecked == true)
                        {
                            Properties.Settings.Default.KeepLoggedIn = true;
                            Properties.Settings.Default.SavedUsername = username;
                        }
                        else
                        {
                            Properties.Settings.Default.KeepLoggedIn = false;
                            Properties.Settings.Default.SavedUsername = string.Empty;
                        }
                        Properties.Settings.Default.Save();

                        MainWindow mainWindow = new MainWindow(username, role);
                        mainWindow.Show();
                        this.Close();
                    }
                    else
                    {
                        txtLoginError.Text = "Usuário ou senha inválidos.";
                    }
                }
            }
            catch (Exception ex)
            {
                txtLoginError.Text = $"Erro ao fazer login: {ex.Message}";
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = txtRegisterUsername.Text;
            string password = pwdRegisterPassword.Password;
            string confirmPassword = pwdRegisterConfirmPassword.Password;
            string activationKey = pwdActivationKey.Password;
            txtRegisterError.Text = "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword) || string.IsNullOrWhiteSpace(activationKey))
            {
                txtRegisterError.Text = "Todos os campos, incluindo a chave, são obrigatórios.";
                return;
            }

            if (!ValidarChave(username, activationKey))
            {
                txtRegisterError.Text = "Chave de Ativação inválida para este nome de usuário.";
                return;
            }

            if (password != confirmPassword)
            {
                txtRegisterError.Text = "As senhas não coincidem.";
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
                {
                    connection.Open();

                    var checkUserCmd = new SQLiteCommand("SELECT COUNT(1) FROM Users WHERE Username = @username", connection);
                    checkUserCmd.Parameters.AddWithValue("@username", username);
                    long userExists = (long)checkUserCmd.ExecuteScalar();

                    if (userExists > 0)
                    {
                        txtRegisterError.Text = "Este nome de usuário já está em uso.";
                        return;
                    }

                    string passwordHash = PasswordHelper.HashPassword(password);
                    var insertCmd = new SQLiteCommand("INSERT INTO Users (Username, PasswordHash, Role) VALUES (@username, @passwordHash, 'Admin')", connection);
                    insertCmd.Parameters.AddWithValue("@username", username);
                    insertCmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                    insertCmd.ExecuteNonQuery();

                    MessageBox.Show("Conta criada e ativada com sucesso! Por favor, faça o login.", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    ShowLoginView_Click(this, new RoutedEventArgs());
                    CheckRegistrationStatus();
                }
            }
            catch (Exception ex)
            {
                txtRegisterError.Text = $"Erro ao criar conta: {ex.Message}";
            }
        }

        private void ImportDatabase_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Selecionar Backup do Banco de Dados para Importar",
                Filter = "Arquivo de Banco de Dados (*.db)|*.db",
                DefaultExt = ".db",
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var confirmBox = new CustomMessageBox(
                    "ATENÇÃO!\n\nIsto irá substituir o banco de dados atual, se existir. " +
                    "O aplicativo será reiniciado após a importação.\n\nDeseja continuar?",
                    "Confirmar Importação de Banco de Dados",
                    MessageBoxButton.OKCancel
                );
                confirmBox.Owner = this;

                if (confirmBox.ShowDialog() != true || confirmBox.Result != MessageBoxResult.OK)
                {
                    return;
                }

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

                    System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Falha ao importar o banco de dados: {ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool ValidarChave(string nomeUsuario, string chaveFornecida)
        {
            string dadosParaHash = $"{nomeUsuario.ToUpper()}-{SementeSecreta}";

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dadosParaHash));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }

                string hashCompleto = builder.ToString().ToUpper();
                string chaveCalculada = hashCompleto.Substring(0, 16);

                string chaveCalculadaFormatada = $"{chaveCalculada.Substring(0, 4)}-{chaveCalculada.Substring(4, 4)}-{chaveCalculada.Substring(8, 4)}-{chaveCalculada.Substring(12, 4)}";

                return chaveFornecida.Replace(" ", "").ToUpper() == chaveCalculadaFormatada;
            }
        }
    }
}