﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

namespace RadioGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private UdpClient udpClient;

        private UdpClient activeRadioUdpClient;

        private volatile bool end;

        private RadioUpdate lastUpdate;

        private DateTime lastUpdateTime = new DateTime(0L);

   

        const double MHZ = 1000000;

        public MainWindow()
        {

            InitializeComponent();

            //allows click and drag anywhere on the window
            this.containerPanel.MouseLeftButtonDown += WrapPanel_MouseLeftButtonDown;
          
            radio1.radioId = 0;
           
            radio2.radioId = 1;
          
            radio3.radioId = 2;

            SetupActiveRadio();
            SetupRadioStatus();
           

        }

        private void SetupRadioStatus()
        {
            //setup UDP
            this.udpClient = new UdpClient();
            this.udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            this.udpClient.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            IPAddress multicastaddress = IPAddress.Parse("239.255.50.10");
            udpClient.JoinMulticastGroup(multicastaddress);

            IPEndPoint localEp = new IPEndPoint(IPAddress.Any, 35024);
            this.udpClient.Client.Bind(localEp);

            Task.Run(() =>
            {
                using (udpClient)
                {

                    while (!end)
                    {
                        try
                        {
                            //IPEndPoint object will allow us to read datagrams sent from any source.
                            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 35024);
                            udpClient.Client.ReceiveTimeout = 10000;
                            var receivedResults = udpClient.Receive(ref remoteEndPoint);

                            lastUpdate = JsonConvert.DeserializeObject<RadioUpdate>(Encoding.UTF8.GetString(receivedResults));

                            lastUpdateTime = DateTime.Now;

                        }
                        catch (Exception e)
                        {
                            Console.Out.WriteLine(e.ToString());
                        }


                    }

                    this.udpClient.Close();
                }
            });

            Task.Run(() =>
            {


                while (!end)
                {
                    Thread.Sleep(100);

                    //check 
                    if (lastUpdate != null && lastUpdate.name != null)
                    {
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {

                            //check if current
                            long elapsedTicks = DateTime.Now.Ticks - lastUpdateTime.Ticks;
                            TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);

                            if (lastUpdate.allowNonPlayers)
                            {
                                this.muteStatusNonUsers.Fill = new SolidColorBrush(Colors.Red);
                                this.muteStatusNonUsers.ToolTip = "Mute Non Players OFF";
                            }
                            else
                            {
                                this.muteStatusNonUsers.Fill = new SolidColorBrush(Colors.Green);
                                this.muteStatusNonUsers.ToolTip = "Mute Non Players ON";
                            }

                            if (lastUpdate.caMode)
                            {
                                this.caModeStatus.Fill = new SolidColorBrush(Colors.Green);
                                this.caModeStatus.ToolTip = "Radio ON - CA / JTAC / Spectator Mode";

                            }
                            else
                            {
                                this.caModeStatus.Fill = new SolidColorBrush(Colors.Red);
                                this.caModeStatus.ToolTip = "Radio OFF - CA / JTAC / Spectator Mode";
                            }


                            radio1.update(lastUpdate, elapsedSpan);
                            radio2.update(lastUpdate, elapsedSpan);
                            radio3.update(lastUpdate, elapsedSpan);

                        }));
                    }
                }
            });

        }

        private void SetupActiveRadio()
        {
            //setup UDP
            this.activeRadioUdpClient = new UdpClient();
            this.activeRadioUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            this.activeRadioUdpClient.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            IPAddress multicastaddress = IPAddress.Parse("239.255.50.10");
            this.activeRadioUdpClient.JoinMulticastGroup(multicastaddress);

            IPEndPoint localEp = new IPEndPoint(IPAddress.Any, 35025);
            this.activeRadioUdpClient.Client.Bind(localEp);

            Task.Run(() =>
            {
                using (activeRadioUdpClient)
                {

                    while (!end)
                    {
                        try
                        {
                            //IPEndPoint object will allow us to read datagrams sent from any source.
                            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 35025);
                            activeRadioUdpClient.Client.ReceiveTimeout = 10000;
                            var receivedResults = activeRadioUdpClient.Receive(ref remoteEndPoint);

                            RadioTransmit lastRadioTransmit = JsonConvert.DeserializeObject<RadioTransmit>(Encoding.UTF8.GetString(receivedResults));
                            switch (lastRadioTransmit.radio)
                            {
                                case 0:
                                    radio1.setLastRadioTransmit(lastRadioTransmit);
                                    break;
                                case 1:
                                    radio2.setLastRadioTransmit(lastRadioTransmit);
                                    break;
                                case 2:
                                    radio3.setLastRadioTransmit(lastRadioTransmit);
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                           // Console.Out.WriteLine(e.ToString());
                        }


                    }

                    this.activeRadioUdpClient.Close();
                }
            });

            Task.Run(() =>
            {


                while (!end)
                {
                    Thread.Sleep(100);

                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        radio1.repaintRadioTransmit();
                        radio2.repaintRadioTransmit();
                        radio3.repaintRadioTransmit();

                    }));
                }
                
            });

        }

        private void WrapPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            this.end = true;

        }

        ~MainWindow()
        {
            //Shut down threads
            this.end = true;
        }

        private void Button_Minimise(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Button_Close(object sender, RoutedEventArgs e)
        {
            this.end = true;
            Close();
        }

        private void windowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.Opacity = e.NewValue;
        }

        private void SendUDPCommand(RadioCommand.CmdType type)
        {
            RadioCommand update = new RadioCommand();
            update.freq =1;
            update.volume = 1;
            update.radio = 0;
            update.cmdType = type;

            byte[] bytes = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(update) + "\n");
            //multicast
            Send("239.255.50.10", 5060, bytes);
            //unicast
            Send("127.0.0.1", 5061, bytes);
            

        }
        private void Send(String ipStr, int port, byte[] bytes)
        {
            try
            {

                UdpClient client = new UdpClient();
                IPEndPoint ip = new IPEndPoint(IPAddress.Parse(ipStr), port);

                client.Send(bytes, bytes.Length, ip);
                client.Close();
            }
            catch (Exception e) { }

        }

        private void Button_ToggleMute(object sender, RoutedEventArgs e)
        {
            SendUDPCommand(RadioCommand.CmdType.TOGGLE_MUTE_NON_RADIO);
        }


        private void Button_Toggle_CA_Mode(object sender, RoutedEventArgs e)
        {
            SendUDPCommand(RadioCommand.CmdType.TOGGLE_FORCE_RADIO_ON);
        }
    }

}
