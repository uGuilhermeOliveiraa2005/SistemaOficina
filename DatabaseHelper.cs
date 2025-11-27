using System;
using System.IO;
using System.Data.SQLite;

namespace SistemaOficina
{
    public static class DatabaseHelper
    {
        public static string DbPath = Path.Combine(Directory.GetCurrentDirectory(), "OficinaDB.db");
        public static void InitializeDatabase()
        {
            if (!File.Exists(DbPath))
            {
                SQLiteConnection.CreateFile(DbPath);
            }

            using (var connection = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
            {
                connection.Open();
                string createTableClientes = @"
                    CREATE TABLE IF NOT EXISTS Clientes (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, Nome TEXT NOT NULL, Endereco TEXT, Cidade TEXT, 
                        Bairro TEXT, Telefone1 TEXT, Telefone2 TEXT, CpfCnpj TEXT
                    );";
                string createTableOrdensDeServico = @"
                    CREATE TABLE IF NOT EXISTS OrdensDeServico (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, NumeroOS TEXT UNIQUE NOT NULL, ClienteId INTEGER, NomeClienteOS TEXT, MarcaEquipamento TEXT,
                        ModeloEquipamento TEXT, NumeroSerie TEXT, DefeitoReclamado TEXT, DefeitosVisiveis TEXT, DefeitoReal TEXT,
                        ServicoExecutado TEXT, Valor REAL, TecnicoResponsavel TEXT, Status TEXT NOT NULL DEFAULT 'Aberta', DataAbertura TEXT NOT NULL,
                        DataFechamento TEXT, Aparelho TEXT, Garantia TEXT, Voltagem TEXT, Cor TEXT, Departamento TEXT, Complemento TEXT, Observacao TEXT,
                        FOREIGN KEY (ClienteId) REFERENCES Clientes(Id)
                    );";
                string createTableCaixa = @"
                    CREATE TABLE IF NOT EXISTS Caixa (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, Data TEXT NOT NULL, Descricao TEXT NOT NULL, Tipo TEXT NOT NULL, 
                        Valor REAL NOT NULL, OrdemServicoId INTEGER, CompraGastoId INTEGER, AcertoId INTEGER, 
                        FOREIGN KEY (OrdemServicoId) REFERENCES OrdensDeServico(Id) ON DELETE SET NULL,
                        FOREIGN KEY (CompraGastoId) REFERENCES ComprasGastos(Id) ON DELETE CASCADE,
                        FOREIGN KEY (AcertoId) REFERENCES Acertos(Id) ON DELETE SET NULL
                    );";
                string createTableComprasGastos = @"
                    CREATE TABLE IF NOT EXISTS ComprasGastos (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, Data TEXT NOT NULL, Descricao TEXT NOT NULL, 
                        Tipo TEXT NOT NULL, Valor REAL NOT NULL, Responsavel TEXT
                    );";
                string createTableUsers = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT UNIQUE NOT NULL, 
                        PasswordHash TEXT NOT NULL, Role TEXT NOT NULL DEFAULT 'Admin',
                        IncluirNoAcerto INTEGER NOT NULL DEFAULT 1
                    );";
                string createTableConfiguracao = @"
                    CREATE TABLE IF NOT EXISTS ConfiguracaoEmpresa (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, NomeEmpresa TEXT, Logo BLOB, Endereco TEXT, Telefone TEXT, 
                        Proprietario TEXT, FuncionarioPrincipal TEXT, PrimeiroAcessoConcluido INTEGER NOT NULL DEFAULT 0, CpfCnpj TEXT
                    );";
                string createTableAcertos = @"
                    CREATE TABLE IF NOT EXISTS Acertos (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Data TEXT NOT NULL,
                        LucroTotal REAL NOT NULL,
                        TotalReembolsado REAL NOT NULL
                    );";

                // #################### INÍCIO DA ALTERAÇÃO ####################
                // Garante que a tabela Agendamentos seja criada com todas as colunas, incluindo as novas
                string createTableAgendamentos = @"
                    CREATE TABLE IF NOT EXISTS Agendamentos (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ClienteId INTEGER,
                        NomeClienteAvulso TEXT,
                        Equipamento TEXT NOT NULL,
                        DataAgendamento TEXT NOT NULL,
                        Status TEXT NOT NULL,
                        Endereco TEXT,
                        Numero TEXT,
                        Bairro TEXT,
                        Cidade TEXT,
                        UF TEXT,
                        CEP TEXT,
                        FOREIGN KEY (ClienteId) REFERENCES Clientes(Id)
                    );";
                // #################### FIM DA ALTERAÇÃO ####################

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = createTableClientes; command.ExecuteNonQuery();
                    command.CommandText = createTableOrdensDeServico; command.ExecuteNonQuery();
                    command.CommandText = createTableCaixa; command.ExecuteNonQuery();
                    command.CommandText = createTableComprasGastos; command.ExecuteNonQuery();
                    command.CommandText = createTableUsers; command.ExecuteNonQuery();
                    command.CommandText = createTableConfiguracao; command.ExecuteNonQuery();
                    command.CommandText = createTableAcertos; command.ExecuteNonQuery();
                    command.CommandText = createTableAgendamentos; command.ExecuteNonQuery();

                    command.CommandText = "INSERT OR IGNORE INTO ConfiguracaoEmpresa (Id, PrimeiroAcessoConcluido) VALUES (1, 0);";
                    command.ExecuteNonQuery();
                }

                // Esta seção garante que bancos de dados antigos sejam atualizados sem erros
                AdicionarColunaSeNaoExistir(connection, "Users", "Role", "TEXT NOT NULL DEFAULT 'Admin'");
                AdicionarColunaSeNaoExistir(connection, "OrdensDeServico", "Aparelho", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "OrdensDeServico", "Garantia", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "OrdensDeServico", "Voltagem", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "OrdensDeServico", "Cor", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "OrdensDeServico", "Departamento", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "OrdensDeServico", "Complemento", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "OrdensDeServico", "Observacao", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "Clientes", "CpfCnpj", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "ConfiguracaoEmpresa", "CpfCnpj", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "ComprasGastos", "Responsavel", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "Caixa", "AcertoId", "INTEGER");
                AdicionarColunaSeNaoExistir(connection, "Agendamentos", "Endereco", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "Agendamentos", "Numero", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "Agendamentos", "Bairro", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "Agendamentos", "Cidade", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "Agendamentos", "UF", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "Agendamentos", "CEP", "TEXT");
                AdicionarColunaSeNaoExistir(connection, "Users", "IncluirNoAcerto", "INTEGER NOT NULL DEFAULT 1");
            }
        }

        // #################### INÍCIO DA ALTERAÇÃO ####################
        // Método substituído por uma versão mais segura que verifica a existência da coluna antes de tentar adicioná-la
        private static void AdicionarColunaSeNaoExistir(SQLiteConnection connection, string nomeTabela, string nomeColuna, string tipoColuna)
        {
            var command = new SQLiteCommand($"PRAGMA table_info({nomeTabela})", connection);
            bool columnExists = false;
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    // O nome da coluna está no segundo campo (índice 1) do resultado do PRAGMA
                    if (reader.GetString(1).Equals(nomeColuna, StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            // Se a coluna não existe, o comando ALTER TABLE é executado
            if (!columnExists)
            {
                var alterCmd = new SQLiteCommand($"ALTER TABLE {nomeTabela} ADD COLUMN {nomeColuna} {tipoColuna}", connection);
                alterCmd.ExecuteNonQuery();
            }
        }
        // #################### FIM DA ALTERAÇÃO ####################
    }
}