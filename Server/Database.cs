using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace Server
{
    public static class Database
    {
        private static string dbPath = "ftp.db";
        private static string connectionString = $"Data Source={dbPath};Version=3;";

        // 1. Инициализация базы: создаем таблицы, если их нет
        public static void Initialize()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                // Таблица пользователей
                string createUsers = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Login TEXT NOT NULL UNIQUE,
                        Password TEXT NOT NULL,
                        RootPath TEXT NOT NULL
                    );";

                // Таблица логов команд
                string createCommands = @"
                    CREATE TABLE IF NOT EXISTS Commands (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER,
                        CommandText TEXT,
                        Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                    );";

                using (var cmd = new SQLiteCommand(createUsers, connection)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand(createCommands, connection)) cmd.ExecuteNonQuery();
            }
        }

        // 2. Добавление пользователя (если его еще нет)
        public static void AddUser(string login, string password, string rootPath)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string sql = "INSERT OR IGNORE INTO Users (Login, Password, RootPath) VALUES (@l, @p, @r)";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@l", login);
                    cmd.Parameters.AddWithValue("@p", password);
                    cmd.Parameters.AddWithValue("@r", rootPath);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 3. Проверка авторизации (возвращает Id или -1)
        public static int AuthorizeUser(string login, string password)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string sql = "SELECT Id FROM Users WHERE Login = @l AND Password = @p";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@l", login);
                    cmd.Parameters.AddWithValue("@p", password);
                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : -1;
                }
            }
        }

        // 4. Получение корневой папки пользователя
        public static string GetRootPath(int userId)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string sql = "SELECT RootPath FROM Users WHERE Id = @id";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }

        // 5. Логирование команды в БД
        public static void LogCommand(int userId, string commandText)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string sql = "INSERT INTO Commands (UserId, CommandText) VALUES (@u, @c)";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@u", userId);
                    cmd.Parameters.AddWithValue("@c", commandText);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}