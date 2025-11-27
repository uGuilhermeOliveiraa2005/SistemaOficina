using System;
using System.Data.SQLite;
using System.Windows;

namespace SistemaOficina
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ##### CORREÇÃO IMPORTANTE #####
            // Esta linha é a mais importante e deve ser a primeira a ser executada.
            // Ela garante que o banco de dados e todas as suas tabelas/colunas
            // existam ANTES de qualquer outra parte do programa tentar usá-los.
            DatabaseHelper.InitializeDatabase();

            bool keepLoggedIn = SistemaOficina.Properties.Settings.Default.KeepLoggedIn;
            string savedUsername = SistemaOficina.Properties.Settings.Default.SavedUsername;

            if (keepLoggedIn && !string.IsNullOrEmpty(savedUsername))
            {
                // Se o usuário pediu para manter conectado, busca a Role dele no banco de dados
                string role = GetUserRole(savedUsername);
                if (role != null)
                {
                    MainWindow mainWindow = new MainWindow(savedUsername, role);
                    mainWindow.Show();
                }
                else
                {
                    // Caso o usuário salvo não exista mais, abre a tela de login
                    LoginWindow loginWindow = new LoginWindow();
                    loginWindow.Show();
                }
            }
            else
            {
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();
            }
        }

        private string GetUserRole(string username)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={DatabaseHelper.DbPath};Version=3;"))
                {
                    connection.Open();
                    var command = new SQLiteCommand("SELECT Role FROM Users WHERE Username = @username", connection);
                    command.Parameters.AddWithValue("@username", username);
                    var result = command.ExecuteScalar();
                    return result?.ToString();
                }
            }
            catch (Exception)
            {
                // Em caso de erro ao acessar o banco (ex: coluna não existe durante a transição), retorna nulo
                return null;
            }
        }
    }
}