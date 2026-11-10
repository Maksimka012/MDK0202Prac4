using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Common;
using Newtonsoft.Json;

namespace Server
{
    internal class Program
    {
        public static IPAddress IpAdress;
        public static int Port;

        // Храним текущий путь для каждого авторизованного пользователя по его Id
        public static Dictionary<int, string> UserSessions = new Dictionary<int, string>();

        // Получение списка файлов и папок (без изменений)
        public static List<string> GetDirectory(string src)
        {
            List<string> FoldersFiles = new List<string>();
            if (Directory.Exists(src))
            {
                string[] dirs = Directory.GetDirectories(src);
                foreach (string dir in dirs)
                {
                    FoldersFiles.Add(Path.GetFileName(dir) + "/");
                }
                string[] files = Directory.GetFiles(src);
                foreach (string file in files)
                {
                    FoldersFiles.Add(Path.GetFileName(file));
                }
            }
            return FoldersFiles;
        }

        public static void StartServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IpAdress, Port);
            Socket sListner = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sListner.Bind(endPoint);
            sListner.Listen(10);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Сервер запущен.");

            while (true)
            {
                try
                {
                    Socket Handler = sListner.Accept();
                    string Data = null;
                    byte[] Bytes = new byte[10485760];
                    int BytesRec = Handler.Receive(Bytes);
                    Data += Encoding.UTF8.GetString(Bytes, 0, BytesRec);
                    Console.Write($"\n[{DateTime.Now:HH:mm:ss}] Сообщение от пользователя: " + Data + "\n");

                    string Reply = "";
                    ViewModelSend ViewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);

                    if (ViewModelSend != null)
                    {
                        ViewModelMessage viewModelMessage = null;
                        string[] DataCommand = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.RemoveEmptyEntries);

                        if (DataCommand.Length == 0) continue;

                        // Логируем команду в БД (если пользователь авторизован)
                        if (ViewModelSend.Id != -1)
                        {
                            Database.LogCommand(ViewModelSend.Id, ViewModelSend.Message);
                        }

                        if (DataCommand[0] == "connect")
                        {
                            if (DataCommand.Length >= 3)
                            {
                                int userId = Database.AuthorizeUser(DataCommand[1], DataCommand[2]);
                                if (userId != -1)
                                {
                                    // Запоминаем, что этот пользователь авторизован и его корневую папку
                                    UserSessions[userId] = Database.GetRootPath(userId);

                                    viewModelMessage = new ViewModelMessage("autorization", userId.ToString());
                                    Console.WriteLine($"[INFO] Пользователь {DataCommand[1]} вошел. ID: {userId}");
                                }
                                else
                                {
                                    viewModelMessage = new ViewModelMessage("message", "Не правильный логин или пароль пользователя");
                                }
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Использование: connect [login] [password]");
                            }
                        }
                        else if (DataCommand[0] == "cd")
                        {
                            if (ViewModelSend.Id != -1 && UserSessions.ContainsKey(ViewModelSend.Id))
                            {
                                string currentSrc = UserSessions[ViewModelSend.Id];
                                List<string> FoldersFiles = new List<string>();

                                if (DataCommand.Length == 1)
                                {
                                    // Возврат в корень
                                    string root = Database.GetRootPath(ViewModelSend.Id);
                                    UserSessions[ViewModelSend.Id] = root;
                                    FoldersFiles = GetDirectory(root);
                                }
                                else
                                {
                                    string cdFolder = string.Join(" ", DataCommand.Skip(1));
                                    string newPath = Path.Combine(currentSrc, cdFolder);

                                    if (Directory.Exists(newPath))
                                    {
                                        UserSessions[ViewModelSend.Id] = newPath;
                                        FoldersFiles = GetDirectory(newPath);
                                    }
                                    else
                                    {
                                        viewModelMessage = new ViewModelMessage("message", "Директория не существует");
                                    }
                                }

                                if (viewModelMessage == null)
                                {
                                    if (FoldersFiles.Count == 0)
                                        viewModelMessage = new ViewModelMessage("message", "Директория пуста");
                                    else
                                        viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                                }
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                        }
                        else if (DataCommand[0] == "get")
                        {
                            if (ViewModelSend.Id != -1 && UserSessions.ContainsKey(ViewModelSend.Id))
                            {
                                if (DataCommand.Length >= 2)
                                {
                                    string getFile = string.Join(" ", DataCommand.Skip(1));
                                    string fullPath = Path.Combine(UserSessions[ViewModelSend.Id], getFile);

                                    if (File.Exists(fullPath))
                                    {
                                        byte[] byteFile = File.ReadAllBytes(fullPath);
                                        viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                                    }
                                    else
                                    {
                                        viewModelMessage = new ViewModelMessage("message", "Файл не найден: " + fullPath);
                                    }
                                }
                                else
                                {
                                    viewModelMessage = new ViewModelMessage("message", "Использование: get [NameFile]");
                                }
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            }
                        }
                        else if (DataCommand[0] == "set")
                        {
                            if (ViewModelSend.Id != -1 && UserSessions.ContainsKey(ViewModelSend.Id))
                            {
                                try
                                {
                                    // Клиент должен прислать JSON с данными файла
                                    FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(ViewModelSend.Message);
                                    if (SendFileInfo != null)
                                    {
                                        string savePath = Path.Combine(UserSessions[ViewModelSend.Id], SendFileInfo.Name);
                                        File.WriteAllBytes(savePath, SendFileInfo.Data);
                                        viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                                    }
                                    else
                                    {
                                        viewModelMessage = new ViewModelMessage("message", "Ошибка формата данных файла");
                                    }
                                }
                                catch
                                {
                                    viewModelMessage = new ViewModelMessage("message", "Ошибка загрузки файла (неверный формат JSON)");
                                }
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            }
                        }
                        else
                        {
                            viewModelMessage = new ViewModelMessage("message", "Неизвестная команда");
                        }

                        if (viewModelMessage != null)
                        {
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.ToString());
                    Console.ForegroundColor = ConsoleColor.Green;
                }
            }
        }

        static void Main(string[] args)
        {
            // 1. Инициализируем БД (создаст файл ftp.db, если его нет)
            Database.Initialize();

            // 2. Добавляем пользователя в БД. Если он уже есть — ничего не произойдет.
            string rootPath = @"C:\Users\kotyl\Desktop\MDK0202Prac4";

            // Проверка на существование папки, чтобы не было ошибок
            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine($"ВНИМАНИЕ: Папка {rootPath} не существует.");
            }

            Database.AddUser("aoshcepkov", "Asdfg123", rootPath);

            Console.Write("Введите IP адрес сервера: ");
            string sIpAddress = Console.ReadLine();
            Console.Write($"Введите порт: ");
            string sPort = Console.ReadLine();

            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAddress, out IpAdress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                StartServer();
            }
            Console.Read();
        }
    }
}