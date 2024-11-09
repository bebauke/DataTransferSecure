using System;
using System.Collections.Generic;
using System.Drawing;
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

            // Default ports
            SetTCPServerPort(9000);
            SetUDPPort(8000);
            SetUDPServerPort(8000);
        }

        // Nachricht senden
        public async Task SendMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                await communicator.SendMessageAsync(message);
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
        internal void OnConnectionLost(object sender, EventArgs e)
        {
            communicator.Disconnect();
            mainForm.UpdateStatus("Verbindung verloren...");
            mainForm.SetChatBoxColor(Color.LightGray);
        }

        // Verbindung wiederherstellen
        public async Task Connect()
        {
            bool result = await communicator.Reconnect(nTry: 3, statusCallback: status =>
            {
                mainForm.UpdateStatus(status);
                if (status.StartsWith("Warnung:"))
                {
                    mainForm.AppendMessage(status, color: Color.Red);
                }
                else if (status.StartsWith("Info:"))
                {
                    mainForm.AppendMessage(status, color: Color.Blue);
                }
#if DEBUG 
                else
                {
                    mainForm.AppendMessage(status, color: Color.MediumSlateBlue);
                }
#endif
            });

            string _status = "no connection";
            if (result)
            {
                _status = communicator.GetTCPClientInfo()[0];
                mainForm.SetChatBoxColor(communicator.UseEncryption ? Color.LightGreen : Color.MistyRose);
            }
            mainForm.UpdateStatus(communicator.IsConnected() ? "Verbunden: " + _status : "Verbindung fehlgeschlagen...");
        }

        // Event-Handler für empfangene Nachrichten
        internal void OnMessageReceived(object sender, string message)
        {
            mainForm.AppendMessage($"Empfangen: {message}");
        }

        // Öffnet das Einstellungsfenster
        public void OpenSettings()
        {
            using (var settingsForm = new SettingsForm())
            {
                // Übertrage aktuelle Werte
                settingsForm.LocalUdpPort = communicator.udpPort;
                settingsForm.DefaultUdpServerPort = communicator.udpServerPort;
                settingsForm.TcpServerPort = communicator.tcpServerPort;
                settingsForm.UseCertificates = communicator.UseCertificate;
                settingsForm.UseEncryption = communicator.UseEncryption;
                settingsForm.UseChecksum = communicator.UseChecksum;
                settingsForm.CertificatePath = communicator.CertificatePath;
                settingsForm.CertificatePassword = communicator.CertificatePassword;

                if (settingsForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Übernimm die neuen Werte
                    SetUDPPort(settingsForm.LocalUdpPort);
                    SetUDPServerPort(settingsForm.DefaultUdpServerPort);
                    SetTCPServerPort(settingsForm.TcpServerPort);

                    // Update encryption technology settings
                    communicator.UseEncryption = settingsForm.UseEncryption;
                    communicator.UseCertificate = settingsForm.UseCertificates;
                    communicator.UseChecksum = settingsForm.UseChecksum;

                    // Update certificate path
                    communicator.CertificatePath = settingsForm.CertificatePath;
                    communicator.CertificatePassword = settingsForm.CertificatePassword;
                }
            }
        }

        // Setze Ports
        public void SetUDPPort(int port) => communicator.udpPort = port;
        public void SetUDPServerPort(int port) => communicator.udpServerPort = port;
        public void SetTCPServerPort(int port) => communicator.tcpServerPort = port;

        public int GetUDPPort() => communicator.udpPort;
        public int GetUDPServerPort() => communicator.udpServerPort;
        public int GetTCPServerPort() => communicator.tcpServerPort;
    }
}
