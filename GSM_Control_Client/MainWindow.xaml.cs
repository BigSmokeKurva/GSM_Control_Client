using Leaf.xNet;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
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
using HttpMethod = System.Net.Http.HttpMethod;

namespace GSM_Control_Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Style _messageStyle;
        string ip;
        int port;
        string api;
        public Dictionary<string, Dictionary<string, dynamic>> ports = new();
        public MainWindow()
        {
            InitializeComponent();
            ReadConfig();
            // Проверка подклбчения
            try { WebRequest.Create($"http://{ip}:{port}/{api}").GetResponse(); }
            catch
            {
                MessageBox.Show($"Server connection error! ({ip}:{port})");
                Environment.Exit(0);
            }
            Title += $" - connected {ip}:{port}";
            _messageStyle = (Style)FindResource("MessageStyle");
            new Thread(ThreadPortsChecker).Start();
            new Thread(ThreadBoardUpdate).Start();

        }
        void ReadConfig()
        {
            using var sr = new StreamReader("config.json");
            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(sr.ReadToEnd());
            ip = json["ip"];
            port = json["port"];
            api = json["api"];
        }
        void ThreadBoardUpdate()
        {
            while (true)
            {
                Thread.Sleep(200);

                Dispatcher.Invoke(() =>
                {
                    if (PortsListBox.SelectedValue is null) return;
                    if (BoardBorder.Visibility != Visibility.Visible) BoardBorder.Visibility = Visibility.Visible;
                    var selectedPort = PortsListBox.SelectedValue.ToString().Replace("System.Windows.Controls.ListBoxItem: ", string.Empty);
                    if(selectedPort == "Dashboard")
                    {
                        if(DashboardGrid.Visibility != Visibility.Visible)
                        {
                            DashboardGrid.Visibility = Visibility.Visible;
                            PortBoardGrid.Visibility = Visibility.Hidden;
                        }
                        List<string> dashboardPorts = new();
                        foreach (var portName in ports.Keys) dashboardPorts.Add($"{portName}: Availability: {ports[portName]["availability"]}; MessagesCount: {ports[portName]["messages"].Count}; Operator: {ports[portName]["operator"]}; PhoneNumber: {ports[portName]["phoneNumber"]}.");
                        DashboardList.ItemsSource = dashboardPorts;
                    }
                    else
                    {
                        if (PortBoardGrid.Visibility != Visibility.Visible)
                        {
                            DashboardGrid.Visibility = Visibility.Hidden;
                            PortBoardGrid.Visibility = Visibility.Visible;
                        }
                        var messages = new List<TextBox>();
                        foreach (string message in ports[selectedPort]["messages"].ToArray()) { messages.Add(new TextBox() { Text = message, Style = _messageStyle }); }
                        messages.Reverse();
                        if (PortNameTitleTextBlock.Text != selectedPort || ports[selectedPort]["phoneNumber"] != PhoneNumberTextBox.Text)
                        {
                            PortNameTitleTextBlock.Text = selectedPort;
                            GSMModelTextBox.Text = ports[selectedPort]["gsmInfo"];
                            AvailabilityTextBox.Text = ports[selectedPort]["availability"].ToString();
                            PhoneNumberTextBox.Text = ports[selectedPort]["phoneNumber"];
                            OperatorTextBox.Text = ports[selectedPort]["operator"];
                            MessagesListBox.ItemsSource = messages;
                        }
                        else if (MessagesListBox.Items.Count != messages.Count) MessagesListBox.ItemsSource = messages;
                    }
                });
            }

        }
        void ThreadPortsChecker()
        {
            string res;
            dynamic json;
            // Firt start
            using (var webResponse = WebRequest.Create($"http://{ip}:{port}/{api}/GetPorts").GetResponse())
            {
                res = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
                json = Newtonsoft.Json.JsonConvert.DeserializeObject(res);
                foreach (JProperty item in json)
                {
                    ports[item.Name] = new()
                    {
                        { "comName", (string)json[item.Name]["comName"] },
                        { "imsi", (string)json[item.Name]["imsi"] },
                        { "operator", (string)json[item.Name]["operator"] },
                        { "gsmInfo", (string)json[item.Name]["gsmInfo"] },
                        { "phoneNumber", (string)json[item.Name]["phoneNumber"] },
                        { "availability", (bool)json[item.Name]["availability"] },
                        { "messages", new List<string>() }
                    };
                    Dispatcher.Invoke(() =>
                    {
                        PortsListBox.Items.Add(item.Name);
                    });
                }
            }
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);
                    using var webResponse = WebRequest.Create($"http://{ip}:{port}/{api}/GetPorts").GetResponse();
                    res = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
                    json = Newtonsoft.Json.JsonConvert.DeserializeObject(res);
                    foreach (Newtonsoft.Json.Linq.JProperty item in json)
                    {
                        ports[item.Name]["comName"] = (string)json[item.Name]["comName"];
                        ports[item.Name]["imsi"] = (string)json[item.Name]["imsi"];
                        ports[item.Name]["operator"] = (string)json[item.Name]["operator"];
                        ports[item.Name]["gsmInfo"] = (string)json[item.Name]["gsmInfo"];
                        ports[item.Name]["phoneNumber"] = (string)json[item.Name]["phoneNumber"];
                        ports[item.Name]["availability"] = (bool)json[item.Name]["availability"];
                        using var webResponse1 = WebRequest.Create($"http://{ip}:{port}/{api}/GetMsgs/Port/{item.Name.Split(" ")[1]}").GetResponse();
                        res = new StreamReader(webResponse1.GetResponseStream()).ReadToEnd();
                        ports[item.Name]["messages"].Clear();
                        foreach (string str in res.Split("\n"))
                        {
                            if (str.Length == 0 || str == "No messages") continue;
                            ports[item.Name]["messages"].Add(str.Replace("|", " | "));
                        }
                    }
                }
                catch { }
            }
        }

        private void ClearButton(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedPort = PortsListBox.SelectedValue.ToString().Replace("System.Windows.Controls.ListBoxItem: ", string.Empty);
                ports[selectedPort]["messages"].Clear();
                WebRequest.Create($"http://{ip}:{port}/{api}/ClearMsgs/Port/{selectedPort.Split(" ")[1]}").GetResponse();
            }
            catch { }
        }

    }
}
