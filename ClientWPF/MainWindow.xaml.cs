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
        public MainWindow()
        {
            InitializeComponent();

            
        }

        public static void ConnectServer()
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IpAddres, Port);
                Socket socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);
                socket.Connect(endPoint);
                if(socket.Connected)
                {

                }
            }
            catch(Exception ex)
            {

            }
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
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
                socket.Connect(endPoint);
                Send($"connect {sIpAddres} {sPort}");
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            Send("cd ..");
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Send($"get {lvFiles.SelectedItem}");
        }

        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                string fileName = ofd.FileName;
                FileInfo fileInfo = new FileInfo(fileName);
                FileInfoFTP newFileInfo = new FileInfoFTP(File.ReadAllBytes(fileName), fileInfo.Name);
                var jsonFile = JsonConvert.SerializeObject(newFileInfo);
                Send($"set {jsonFile}");

            }
        }

        private void MouseDoubleClick_toDirectory(object sender, MouseButtonEventArgs e)
        {
            Send($"cd {lvFiles.SelectedItem.ToString().TrimEnd('/')}");
        }

        public void Send(string message)
        {
            try
            {
                if (socket.Connected)
                {
                    ViewModelSend viewModelSend = new ViewModelSend(message, Id);
                    socket.Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(viewModelSend)));
                    byte[] bytes = new byte[10485760];
                    int bytesRec = socket.Receive(bytes);
                    string messageServer = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                    ViewModelMessage viewModelMessage = JsonConvert.DeserializeObject<ViewModelMessage>(messageServer);

                    if (viewModelMessage.Command == "autorization")
                    {
                        Id = int.Parse(viewModelMessage.Data);
                        Send("cd");
                    }
                    else if (viewModelMessage.Command == "message")
                        MessageBox.Show(viewModelMessage.Data);
                    else if (viewModelMessage.Command == "cd")
                    {
                        lvFiles.ItemsSource = JsonConvert.DeserializeObject<List<string>>(viewModelMessage.Data);
                    }
                    else if (viewModelMessage.Command == "file")
                    {
                        SaveFileDialog sfd = new SaveFileDialog();
                        if (sfd.ShowDialog() == true)
                        {
                            string nameFile = sfd.FileName;
                            byte[] bytesFile = JsonConvert.DeserializeObject<byte[]>(viewModelMessage.Data);
                            File.WriteAllBytes(nameFile, bytesFile);
                            MessageBox.Show("Файл загружен");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Подключение не удалось(");
                }
            }
            catch (Exception ex) { 
                MessageBox.Show($"Что-то случилось: {ex.Message}"); 
            }
        }
    }
}
