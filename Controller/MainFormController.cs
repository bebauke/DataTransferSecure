using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using DataTransferSecure.Services;
using DataTransferSecure.Views;

namespace DataTransferSecure.Controller
{
    public class MainFormController
    {
        internal readonly Communicator communicator;
        internal readonly MainForm mainForm;

        public MainFormController(MainForm form)
        {
            this.mainForm = form;
            communicator = new Communicator();
            communicator.MessageReceived += OnMessageReceived;
            communicator.ConnectionLost += OnConnectionLost;

            SetTCPServerPort(9000);
            SetUDPPort(8000);
            SetUDPServerPort(8001);
        }

        // Nachricht senden
        public async Task SendMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                if (communicator.encrypted)
                    await communicator.SendMessageAsync(message);
                else
                    await communicator.SendMessageAsync_unencrypted(message);
                mainForm.AppendMessage($"Ich: {message}");
            }
            catch (Exception ex)
            {
                mainForm.ShowError($"Fehler beim Senden: {ex.Message}");
            }
        }

        // Verbindung trennen
        public void Disconnect()
        {
            communicator.Disconnect();
            mainForm.UpdateStatus("Getrennt");
            mainForm.SetChatBoxColor(Color.LightGray);
        }

        // OnConnectionLost-Event-Handler
        private void OnConnectionLost(object sender, EventArgs e)
        {
            communicator.Disconnect();
            mainForm.UpdateStatus("Verbindung verloren...");
            mainForm.SetChatBoxColor(Color.LightGray);
        }

        // Verbindung wiederherstellen
        public async Task Connect(bool encryptet = true)
        {
            bool result = await communicator.Reconnect(nTry: 3, encrypted: encryptet, statusCallback: status =>
            {
                mainForm.UpdateStatus(status);
#if DEBUG
                mainForm.AppendMessage("Status: " + status, color: Color.MediumSlateBlue);
#endif
            });

            string _status = "no connection";
            if (result)
            {
                _status = communicator.GetTCPClientInfo()[0];
                mainForm.SetChatBoxColor(encryptet ? Color.LightGreen : Color.MistyRose);
            }
            mainForm.UpdateStatus(communicator.IsConnected() ? "Verbunden: " + _status : "Verbindung fehlgeschlagen...");
        }

        // Event-Handler für empfangene Nachrichten
        internal void OnMessageReceived(object sender, string message)
        {
            mainForm.AppendMessage($"Empfangen: {message}");
        }

        // Setze Ports

        public void SetUDPPort(int port)
        {
            communicator.udpPort = port;
        }
        public void SetUDPPort(string port)
        {
            if (int.TryParse(port, out int udpPort))
            {
                SetUDPPort(udpPort);
            }
            else
            {
                mainForm.ShowError("Ungültiger Port.");
            }
        }

        public void SetUDPServerPort(int port)
        {
            communicator.udpServerPort = port;
        }
        public void SetUDPServerPort(string port)
        {
            if (int.TryParse(port, out int udpServerPort))
            {
                SetUDPServerPort(udpServerPort);
            }
            else
            {
                mainForm.ShowError("Ungültiger Port.");
            }
        }


        public void SetTCPServerPort(int port)
        {
            communicator.tcpServerPort = port;
        }
        public void SetTCPServerPort(string port)
        {
            if (int.TryParse(port, out int tcpPort))
            {
                SetTCPServerPort(tcpPort);
            }
            else
            {
                mainForm.ShowError("Ungültiger Port.");
            }
        }



        public void updateIPAddresses(string local_ip, string remote_ip)
        {
            List<string> info = communicator.GetTCPClientInfo();
            mainForm.UpdateIPs(info[0], info[1]);
        }


        public int GetUDPPort()
        {
            return communicator.udpPort;
        }

        public int GetUDPServerPort()
        {
            return communicator.udpServerPort;
        }

        public int GetTCPServerPort()
        {
            return communicator.tcpServerPort;
        }
    }
}
