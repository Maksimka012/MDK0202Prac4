using Common;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ClientWPF
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static IPAddress IpAddres;
        public static int Port;
        public static int Id = 1;
        Socket socket;
        ViewModelSend viewModelSend;
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string sIpAddres = tbIp.Text;
                string sPort = tbPort.Text;

                if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAddres, out IpAddres))
                {
                    MessageBox.Show("Данные успешно введены. Подключаюсь к серверу.");
                    IPEndPoint endPoint = new IPEndPoint(IpAddres, Port);
                    socket = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp);
                    await socket.ConnectAsync(endPoint);
                    if (socket.Connected)
                    {
                        string login = tbLogin.Text;
                        string pass = tbPass.Password;
                        ViewModelMessage viewModelMessage = await Task.Run(() => Send($"connect {login} {pass}"));

                        if (viewModelMessage.Command == "autorization")
                        {
                            Id = int.Parse(viewModelMessage.Data);
                            ViewModelMessage view = await Task.Run(() => Send("cd"));

                            if (view.Command == "cd")
                            {
                                var list = JsonConvert.DeserializeObject<List<string>>(view.Data);
                                Dispatcher.Invoke(() => { lvFiles.ItemsSource = list; });
                            }
                            else if (view.Command == "message")
                            {
                                MessageBox.Show("Ошибка: " + view.Data);
                            }
                        }                        
                    }
                    else
                    {
                        MessageBox.Show("Подключение не удалось(");
                    }
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("Что-то случилось: " + exp.Message);
            }            
        }

        private async void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            ViewModelMessage view = await Task.Run(() => Send("cd .."));
            if (view.Command == "cd")
                lvFiles.ItemsSource = JsonConvert.DeserializeObject<List<string>>(view.Data);
            else if (view.Command == "message")
                MessageBox.Show(view.Data);
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (lvFiles.SelectedItem == null) return;
            string file = lvFiles.SelectedItem.ToString();
            if (file.EndsWith("/")) 
            { 
                MessageBox.Show("Это папка!"); 
                return; 
            }
            ViewModelMessage view = await Task.Run(() => Send($"get {file}"));

            if (view.Command == "file")
            {
                string[] DataMessage = viewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                string getFile = "";
                for (int i = 1; i < DataMessage.Length; i++)
                {
                    if (getFile == "")
                        getFile = DataMessage[i];
                    else
                        getFile += " " + DataMessage[i];
                }

                byte[] bytesFile = JsonConvert.DeserializeObject<byte[]>(view.Data);
                File.WriteAllBytes(getFile, bytesFile);
            }
            else if (view.Command == "message")
                MessageBox.Show(view.Data);
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                string fileName = ofd.FileName;
                FileInfo fileInfo = new FileInfo(fileName);
                FileInfoFTP newFileInfo = new FileInfoFTP(File.ReadAllBytes(fileName), fileInfo.Name);
                var jsonFile = JsonConvert.SerializeObject(newFileInfo);
                ViewModelMessage view = await Task.Run(() => Send($"set {jsonFile}"));
                if (view != null) MessageBox.Show(view.Data);
            }
        }

        private async void MouseDoubleClick_toDirectory(object sender, MouseButtonEventArgs e)
        {
            if (lvFiles.SelectedItem == null) return;
            string selected = lvFiles.SelectedItem.ToString();
            if (selected.EndsWith("/"))
            {
                string folder = selected.TrimEnd('/');
                ViewModelMessage view = await Task.Run(() => Send($"cd {folder}"));
                if(view.Command == "cd")
                    lvFiles.ItemsSource = JsonConvert.DeserializeObject<List<string>>(view.Data);
                else if (view.Command == "message")
                    MessageBox.Show(view.Data);
            }
        }

        public ViewModelMessage Send(string message)
        {
            try
            {
                viewModelSend = new ViewModelSend(message, Id);
                socket.Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend)));
                byte[] bytes = new byte[10485760];
                int bytesRec = socket.Receive(bytes);
                string messageServer = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                System.Diagnostics.Debug.WriteLine("ПРИШЛО ОТ СЕРВЕРА: " + messageServer); 
                ViewModelMessage viewModelMessage = JsonConvert.DeserializeObject<ViewModelMessage>(messageServer);
                return viewModelMessage;
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Что-то случилось в Send: {ex.Message}"); 
                return null;
            }
        }
    }
}
