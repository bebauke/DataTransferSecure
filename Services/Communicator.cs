using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DataTransferSecure.Services
{
    public class Communicator
    {

        internal bool encrypted = true;

        internal int udpPort;
        internal int tcpServerPort;
        internal int udpServerPort;

        public event EventHandler<string> MessageReceived; // Ereignis für empfangene Nachrichten
        public event EventHandler ConnectionLost;

        internal string Role { get; set; } = "Unbekannt";
        internal TcpClient tcpClient;
        private TcpListener tcpListener;
        private UdpClient udpClient;
        internal bool iAmServer { get; set; }
        // Private Eigenschaft zum Speichern des gemeinsamen Schlüssels
        private byte[] sharedSecretKey;
        CancellationTokenSource cts;


        // Initialisiert die Kommunikation
        // Initialisiert die Kommunikation und stellt sofort auf eine verschlüsselte Verbindung um
        public async Task Init(int udpPort = 8000, int tcpServerPort = 9000, int timeoutMilliseconds = 5000, Action<string> statusCallback = null, bool encrypted = true, int udpServerPort = 8000)
        {
            this.encrypted = encrypted;

            this.udpPort = udpPort;
            this.tcpServerPort = tcpServerPort;
            this.udpServerPort = udpServerPort;
            var timeoutTask = Task.Delay(timeoutMilliseconds); // Timeout-Task
            string _status = "Unbekannt";

            // Port Verfügbar?
            if (!Utilities.PortManager.IsPortAvailable(udpPort))
            {
                throw new InvalidOperationException($"UDP-Port {udpPort} wird bereits verwendet.");
            }

            if(!Utilities.PortManager.IsPortAvailable(tcpServerPort))
            {
                throw new InvalidOperationException($"TCP-Port {tcpServerPort} wird bereits verwendet.");
            }

            // Schritt 1: Handshake ausführen und Rolle festlegen
            Task<string> hsTask = PerformUDPHandshake(udpPort, this.udpServerPort, statusCallback: status =>
            {
                _status = status;
                statusCallback?.Invoke(status + " " + DateTime.Now.ToString("mm:ss.fff"));
            });

            Task task = await Task.WhenAny(hsTask, timeoutTask);
            if (task == timeoutTask)
            {
                throw new TimeoutException($"Handshake hat zu lange gedauert: {_status}.");
            }
            string resultIpAddress = hsTask.Result;

            // Schritt 2: TCP-Verbindung aufbauen
            var initTask = SetupTcpConnection(tcpServerPort, resultIpAddress, statusCallback);
            if (await Task.WhenAny(initTask, timeoutTask) == timeoutTask)
            {
                throw new TimeoutException("TCP-Verbindung konnte nicht rechtzeitig hergestellt werden.");
            }

            if (encrypted)
            {
                // Schritt 3: Diffie-Hellman-Schlüsselaustausch durchführen
                statusCallback?.Invoke("Stelle auf eine verschlüsselte Verbindung um...");
                byte[] sharedSecret = await PerformDiffieHellmanKeyExchange(timeoutMilliseconds, statusCallback);

                // `sharedSecret` verwenden, um eine Verschlüsselung der Kommunikation zu implementieren
                SetSharedSecretKey(sharedSecret);

                statusCallback?.Invoke("Verschlüsselte Verbindung hergestellt.");
            }
            else
            {
                statusCallback?.Invoke("Verbindung hergestellt (unsicher)");
            }

            // Schritt 4: Starte das Lauschen auf Nachrichten
            StartListening(encrypted);
        }

        public void StopListening()
        {
            cts.Cancel();
        }

        public void StartListening(bool encrypted = true, Action<string> statusCallback = null)
        {
            if (cts != null)
            {

                cts.Cancel();
                statusCallback?.Invoke("Abbruch des Empfangs");
            }
            cts = new CancellationTokenSource();
            if (encrypted)
            {
                ListenForMessages(cts, statusCallback);
                statusCallback?.Invoke("Verschlüsselte Nachrichten werden empfangen");
            }
            else
            {

                ListenForMessages_unencrypted(cts, statusCallback);
                statusCallback?.Invoke("Unverschlüsselte Nachrichten werden empfangen");
            }
        }




        internal async Task SetupTcpConnection(int portServer, string ipAdressServer, Action<string> statusCallback = null)
        {
            Task timeoutTask = Task.Delay(5000); // Timeout-Task

            if (iAmServer)
            {
                tcpListener = new TcpListener(IPAddress.Any, portServer);
                tcpListener.Start();
                Role = "Server";
                statusCallback?.Invoke($"Server: Warte auf Verbindung (p{portServer})");

                // Warten auf eingehende Verbindung und Zuweisung an tcpClient
                var acceptTask = tcpListener.AcceptTcpClientAsync();
                if (await Task.WhenAny(acceptTask, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException("Timeout beim Warten auf Verbindung.");
                }

                tcpClient = acceptTask.Result; // Zuweisung der akzeptierten Verbindung

                statusCallback?.Invoke($"Verbindung hergestellt");
            }
            else
            {
                tcpClient = new TcpClient();
                statusCallback?.Invoke($"Client: Verbinde mit {ipAdressServer}:{portServer}");

                // Warten auf die Verbindung mit dem Server
                var connectTask = tcpClient.ConnectAsync(ipAdressServer, portServer);
                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException("Timeout beim Verbinden.");
                }

                statusCallback?.Invoke($"Verbunden");
                Role = "Client";
            }
        }


        internal List<string> GetTCPClientInfo()
        {
            List<string> info = new List<string>();
            if (tcpClient != null)
            {
                info.Add(tcpClient.Client.LocalEndPoint.ToString());
                info.Add(tcpClient.Client.RemoteEndPoint.ToString());
            }

            return info;
        }


        // Führe den UDP-Handshake durch, um Rollen zuzuweisen ! Warnung wenn beide Teilnehmer nicht auf dem Standartport sind,
        // muss einer der Teilnemer den fremden Port angeben.
        internal async Task<string> PerformUDPHandshake(int udpPort, int serverPort = 8000, Action<string> statusCallback = null)
        {
            // Verwenden Sie den UdpClient mit dem gegebenen Port zum Empfangen
            udpClient = new UdpClient(udpPort);
            udpClient.EnableBroadcast = true;
            IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, serverPort);
            string localIPAddress = Utilities.NetworkUtils.GetLocalIPAddress();
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(localIPAddress), udpPort);
            string returnIPAddress = "";
            byte[] handshakeMessage = Encoding.UTF8.GetBytes("HSK:Client");

            try
            {
                // Sende eine Handshake-Nachricht
                statusCallback?.Invoke($"Sende Handshake-Nachricht (p{serverPort})");
                await udpClient.SendAsync(handshakeMessage, handshakeMessage.Length, broadcastEndPoint);

                while (true)
                {
                    // Warte auf eine Antwort
                    statusCallback?.Invoke($"Warte auf Antwort (p{udpPort})");
                    var receiveTask = udpClient.ReceiveAsync();
                    if (await Task.WhenAny(receiveTask, Task.Delay(5000)) == receiveTask)
                    {
                        UdpReceiveResult result = receiveTask.Result;

                        // Prüfen, ob die empfangene Nachricht von der eigenen Adresse stammt
                        if (result.RemoteEndPoint.Address.Equals(localEndPoint.Address) &&
                            result.RemoteEndPoint.Port == localEndPoint.Port)
                        {
                            statusCallback?.Invoke($"Eigene: {result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port} / {localEndPoint.Address}:{localEndPoint.Port}");
                            continue; // Ignoriere diese Nachricht und warte erneut
                        }


                        string response = Encoding.UTF8.GetString(result.Buffer);
                        statusCallback?.Invoke($"Antwort empfangen: {response}: {result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port}/{localEndPoint.Address}:{localEndPoint.Port}");

                        iAmServer = response == "HSK:Client";
                        Role = iAmServer ? "Server" : "Client";
                        statusCallback?.Invoke($"Ich bin der {Role}");

                        if (iAmServer)
                        {
                            // Gib die Rückmeldung, dass der Handshake erfolgreich war (HSK:Client)
                            handshakeMessage = Encoding.UTF8.GetBytes("HSK:Server");
                            statusCallback?.Invoke($"Sende Antwort (p{result.RemoteEndPoint.Port})");
                            await udpClient.SendAsync(handshakeMessage, handshakeMessage.Length, result.RemoteEndPoint);
                            statusCallback?.Invoke($"Antwort gesendet");
                        }
                        else
                        {
                            // Gib die Rückmeldung, dass der Handshake erfolgreich war (HSK:Server)
                        }
                        returnIPAddress = result.RemoteEndPoint.Address.ToString();
                        break; // Beende die Schleife, wenn eine gültige Nachricht empfangen wurde
                    }
                    else // Timeout
                    {
                        statusCallback?.Invoke($"Timeout bei Empfang der Antwort");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Fehler beim Senden/Empfangen: {ex.Message}");
            }
            statusCallback?.Invoke($"Handshake beendet");

            // free resources
            udpClient.Close();

            return returnIPAddress;
        }

        public void Disconnect()
        {
            try
            {
                // Ensure the network stream is closed
                tcpClient?.GetStream()?.Close();
                tcpClient?.Close(); // Close the TCP client
                tcpListener?.Stop(); // Stop the TCP listener if running
                udpClient?.Close(); // Close the UDP client
                Role = "Getrennt"; // Update the role to "Disconnected"
                Console.WriteLine("Verbindung getrennt und Ressourcen freigegeben.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Trennen der Verbindung: {ex.Message}");
            }
        }

        public async Task<bool> Reconnect(int udpPort = 0, int tcpServerPort = 0, int udpServerPort = 0, Action<string> statusCallback = null, bool encrypted = true, int nTry = 1)
        {
            if (udpPort == 0)
                udpPort = this.udpPort;
            if (tcpServerPort == 0)
                tcpServerPort = this.tcpServerPort;
            if (udpServerPort == 0)
                udpServerPort = this.udpServerPort;

            // Ensure the current connection is disconnected
            Disconnect();
            await Task.Delay(500); // Short delay to ensure resources are released

            while (nTry > 0)
            {
                try
                {
                    // Reinitialize the connection
                    await Init(udpPort, tcpServerPort, statusCallback: statusCallback, encrypted: encrypted, udpServerPort: udpServerPort);
                    return true;
                }
                catch (Exception ex)
                {
                    statusCallback?.Invoke($"Fehler beim Verbinden: {ex.Message} (Versuche übrig: {nTry})");
                    nTry--;
                }
                Disconnect();
                await Task.Delay(1000); // Delay before retrying
            }

            return false;
        }



        private async void ListenForMessages(CancellationTokenSource token, Action<string> statusCallback = null)
        {
            try
            {
                NetworkStream stream = tcpClient.GetStream();
                byte[] buffer = new byte[1024];

                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            byte[] binaryData = buffer.Take(bytesRead).ToArray();
                            string message = DecryptMessage(binaryData, GetSharedSecretKey());
                            MessageReceived?.Invoke(this, message);
                        }
                        else
                        {
                            // If bytesRead is 0, the other side has disconnected
                            statusCallback?.Invoke("Der andere Teilnehmer hat die Verbindung geschlossen.");
                            ConnectionLost?.Invoke(this, EventArgs.Empty);
                            Disconnect(); // Clean up this side's connection
                            break;
                        }
                    }
                    catch (IOException)
                    {
                        statusCallback?.Invoke("Fehler beim Lesen der Nachricht. Verbindung könnte unterbrochen sein.");
                        Disconnect();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Fehler beim Empfangen von Nachrichten: {ex.Message}");
            }
            statusCallback?.Invoke("Verschlüsselte Nachrichten empfangen abgebrochen");
        }

        private async void ListenForMessages_unencrypted(CancellationTokenSource token, Action<string> statusCallback = null)
        {
            try
            {
                NetworkStream stream = tcpClient.GetStream();
                byte[] buffer = new byte[1024];

                while (true)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            MessageReceived?.Invoke(this, message);
                        }
                        else
                        {
                            // If bytesRead is 0, the other side has disconnected
                            statusCallback?.Invoke("Der andere Teilnehmer hat die Verbindung geschlossen.");
                            ConnectionLost?.Invoke(this, EventArgs.Empty);
                            Disconnect();
                            break;
                        }
                    }
                    catch (IOException)
                    {
                        statusCallback?.Invoke("Fehler beim Lesen der Nachricht. Verbindung könnte unterbrochen sein.");
                        Disconnect();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Fehler beim Empfangen von Nachrichten: {ex.Message}");
            }
            statusCallback?.Invoke("Unverschlüsselte Nachrichten empfangen abgebrochen");
        }


        public async Task SendMessageAsync_unencrypted(string message, Action<string> statusCallback = null)
        {
            if (tcpClient == null || !tcpClient.Connected)
                throw new InvalidOperationException("Keine Verbindung vorhanden.");

            NetworkStream stream = tcpClient.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
            statusCallback?.Invoke("Nachricht gesendet: " + data);
        }

        // Sendet eine Nachricht über TCP
        public async Task SendMessageAsync(string message, Action<string> statusCallback = null)
        {
            if (tcpClient == null || !tcpClient.Connected)
                throw new InvalidOperationException("Keine Verbindung vorhanden.");

            NetworkStream stream = tcpClient.GetStream();
            byte[] data = EncryptMessage(message, GetSharedSecretKey());
            await stream.WriteAsync(data, 0, data.Length);
            statusCallback?.Invoke("Nachricht verschlüsselt gesendet: " + data);
        }

        // Überprüft, ob die Verbindung aktiv ist
        public bool IsConnected()
        {
            return tcpClient != null && tcpClient.Connected;
        }


        // Diffie-Hellman-Schlüsselaustausch
        public async Task<byte[]> PerformDiffieHellmanKeyExchange(int timeoutMilliseconds = 5000, Action<string> statusCallback = null)
        {
            using (var diffieHellman = new ECDiffieHellmanCng())
            {
                diffieHellman.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
                diffieHellman.HashAlgorithm = CngAlgorithm.Sha256;

                byte[] publicKey = diffieHellman.PublicKey.ToByteArray();
                byte[] sharedSecret = null;

                Task timeoutTask = Task.Delay(timeoutMilliseconds); // Timeout-Task
                statusCallback?.Invoke("Starte Diffie-Hellman-Schlüsselaustausch...");

                if (tcpClient == null)
                {
                    statusCallback?.Invoke("tcpClient ist null. Die TCP-Verbindung wurde nicht korrekt hergestellt.");
                    throw new InvalidOperationException("tcpClient ist null.");
                }

                var stream = tcpClient.GetStream();

                if (iAmServer)
                {
                    statusCallback?.Invoke("Warte auf den Public Key des Clients...");

                    var buffer = new byte[1024];
                    var receiveTask = stream.ReadAsync(buffer, 0, buffer.Length);
                    if (await Task.WhenAny(receiveTask, timeoutTask) == timeoutTask)
                    {
                        throw new TimeoutException("Timeout beim Warten auf den Public Key des Clients.");
                    }

                    byte[] clientPublicKey = buffer.Take(receiveTask.Result).ToArray();
                    statusCallback?.Invoke("Public Key des Clients empfangen. Berechne gemeinsames Geheimnis...");
                    sharedSecret = diffieHellman.DeriveKeyMaterial(CngKey.Import(clientPublicKey, CngKeyBlobFormat.EccPublicBlob));

                    // Sende den Public Key des Servers zurück an den Client
                    statusCallback?.Invoke("Sende Public Key an den Client...");
                    await stream.WriteAsync(publicKey, 0, publicKey.Length);
                    statusCallback?.Invoke("Public Key an den Client gesendet.");
                }
                else
                {
                    statusCallback?.Invoke("Sende Public Key an den Server...");
                    await stream.WriteAsync(publicKey, 0, publicKey.Length);

                    statusCallback?.Invoke("Warte auf den Public Key des Servers...");

                    var buffer = new byte[1024];
                    var receiveTask = stream.ReadAsync(buffer, 0, buffer.Length);
                    if (await Task.WhenAny(receiveTask, timeoutTask) == timeoutTask)
                    {
                        throw new TimeoutException("Timeout: Stelle sicher dass alle Teilnehmer verschlüsseln!");
                    }

                    byte[] serverPublicKey = buffer.Take(receiveTask.Result).ToArray();
                    statusCallback?.Invoke("Public Key des Servers empfangen. Berechne gemeinsames Geheimnis...");
                    sharedSecret = diffieHellman.DeriveKeyMaterial(CngKey.Import(serverPublicKey, CngKeyBlobFormat.EccPublicBlob));
                }

                statusCallback?.Invoke("Diffie-Hellman-Schlüsselaustausch abgeschlossen.");
                return sharedSecret;
            }
        }

        // Verschlüsselt eine Nachricht mit AES
        internal byte[] EncryptMessage(string message, byte[] key)
        {
            using (var aes = new AesManaged())
            {
                aes.Key = key;
                aes.GenerateIV();
                var encryptor = aes.CreateEncryptor();
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] encryptedMessage = encryptor.TransformFinalBlock(messageBytes, 0, messageBytes.Length);

                // Kombiniere IV und verschlüsselte Nachricht
                return aes.IV.Concat(encryptedMessage).ToArray();
            }
        }

        // Entschlüsselt eine Nachricht mit AES
        internal string DecryptMessage(byte[] encryptedMessage, byte[] key)
        {
            using (var aes = new AesManaged())
            {
                aes.Key = key;

                // Extrahiere IV und verschlüsselte Nachricht
                byte[] iv = encryptedMessage.Take(16).ToArray();
                byte[] cipherText = encryptedMessage.Skip(16).ToArray();

                aes.IV = iv;
                var decryptor = aes.CreateDecryptor();
                byte[] decryptedMessage = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                return Encoding.UTF8.GetString(decryptedMessage);
            }
        }

        // Methode, um den Schlüssel zu setzen
        private void SetSharedSecretKey(byte[] key)
        {
            sharedSecretKey = key;
        }

        // Methode, um den Schlüssel zu erhalten (z.B. für die Verwendung beim Verschlüsseln/Entschlüsseln)
        private byte[] GetSharedSecretKey()
        {
            return sharedSecretKey;
        }

    }
}
