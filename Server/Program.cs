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
        public static List<User> Users = new List<User>();
        public static IPAddress IpAdress;
        public static int Port;

        public static bool AutorizationUser(string login, string password)
        {
            User user = Users.Find(x => x.login == login && x.password == password);
            return user != null;
        }

        // Исправленный метод получения директории
        public static List<string> GetDirectory(string src)
        {
            List<string> FoldersFiles = new List<string>();
            if (Directory.Exists(src))
            {
                // Получаем папки
                string[] dirs = Directory.GetDirectories(src);
                foreach (string dir in dirs)
                {
                    // Берем только имя папки, а не полный путь
                    FoldersFiles.Add(Path.GetFileName(dir) + "/");
                }
                // Получаем файлы
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
            Socket sListner = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
                );
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
                    Console.Write("Сообщение от пользователя: " + Data + "\n");
                    string Reply = "";
                    ViewModelSend ViewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);

                    if (ViewModelSend != null)
                    {
                        ViewModelMessage viewModelMessage = null;
                        string[] DataCommand = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.RemoveEmptyEntries);

                        // Проверка на пустую команду
                        if (DataCommand.Length == 0) continue;

                        if (DataCommand[0] == "connect")
                        {
                            // ИСПРАВЛЕНИЕ: Проверка на наличие логина и пароля
                            if (DataCommand.Length >= 3)
                            {
                                if (AutorizationUser(DataCommand[1], DataCommand[2]))
                                {
                                    int IdUser = Users.FindIndex(x => x.login == DataCommand[1] && x.password == DataCommand[2]);
                                    viewModelMessage = new ViewModelMessage("autorization", IdUser.ToString());
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
                            if (ViewModelSend.Id != -1)
                            {
                                List<string> FoldersFiles = new List<string>();
                                string currentSrc = Users[ViewModelSend.Id].temp_src;

                                if (DataCommand.Length == 1)
                                {
                                    // Команда cd без аргументов - возврат в корень
                                    Users[ViewModelSend.Id].temp_src = Users[ViewModelSend.Id].src;
                                    FoldersFiles = GetDirectory(Users[ViewModelSend.Id].src);
                                }
                                else
                                {
                                    // Собираем имя папки (может содержать пробелы)
                                    string cdFolder = string.Join(" ", DataCommand.Skip(1));

                                    // ИСПРАВЛЕНИЕ: Используем Path.Combine для правильного склеивания путей
                                    string newPath = Path.Combine(currentSrc, cdFolder);

                                    // Проверяем, существует ли такая папка
                                    if (Directory.Exists(newPath))
                                    {
                                        Users[ViewModelSend.Id].temp_src = newPath;
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
                            if (ViewModelSend.Id != -1)
                            {
                                if (DataCommand.Length >= 2)
                                {
                                    string getFile = string.Join(" ", DataCommand.Skip(1));
                                    // ИСПРАВЛЕНИЕ: Правильное объединение пути
                                    string fullPath = Path.Combine(Users[ViewModelSend.Id].temp_src, getFile);

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
                            // ИСПРАВЛЕНИЕ: Обработка команды set (загрузка файла)
                            // Раньше это было в else, из-за чего падал парсер JSON
                            if (ViewModelSend.Id != -1)
                            {
                                // Здесь Message должен содержать JSON с FileInfoFTP
                                try
                                {
                                    FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(ViewModelSend.Message);
                                    if (SendFileInfo != null)
                                    {
                                        string savePath = Path.Combine(Users[ViewModelSend.Id].temp_src, SendFileInfo.Name);
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
                            // Неизвестная команда
                            viewModelMessage = new ViewModelMessage("message", "Неизвестная команда");
                        }

                        // Отправка ответа (вынесено за пределы if/else, чтобы отправлялся всегда)
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
                    // Не забываем сбрасывать цвет обратно, чтобы консоль не была полностью красной
                    Console.ForegroundColor = ConsoleColor.Green;
                }
            }
        }

        static void Main(string[] args)
        {
            // ВНИМАНИЕ: Убедитесь, что путь A:\Авиатехникум существует на вашем ПК,
            // иначе тесты не пройдут. Для теста лучше использовать реальную папку, например C:\Test
            Users.Add(new User("aoshcepkov", "Asdfg123", @"C:\Users\kotyl\Desktop\MDK0202Prac4")); // Изменил путь для примера, проверьте свой

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