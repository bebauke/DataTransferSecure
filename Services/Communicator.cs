using DataTransferSecure.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DataTransferSecure.Services
{
    public class Communicator
    {

        internal bool UseEncryption = true;
        internal bool UseCertificate = true;
        internal bool UseChecksum = true;


        internal int udpPort;
        internal int tcpServerPort;
        internal int udpServerPort;

        public event EventHandler<string> MessageReceived; // Ereignis für empfangene Nachrichten
        public event EventHandler ConnectionLost;

        internal string Role { get; set; } = "Unbekannt";
        internal bool iAmServer { get; set; }
        internal TcpClient tcpClient;
        internal TcpListener tcpListener;
        internal UdpClient udpClient;
        // internal Eigenschaft zum Speichern des gemeinsamen Schlüssels
        internal byte[] sharedSecretKey;
        internal X509Certificate2 certificate;
        internal X509Certificate2 remoteCertificate;
        CancellationTokenSource cts;


        // Initialisiert die Kommunikation
        // Initialisiert die Kommunikation und stellt sofort auf eine verschlüsselte Verbindung um
        public async Task Init(int udpPort = 8000, int tcpServerPort = 9000, int timeoutMilliseconds = 5000, Action<string> statusCallback = null, bool useEncryption = true, bool useCertificate = true, bool useChecksum = true, int udpServerPort = 8000)
        {
            this.UseEncryption = useEncryption;
            this.UseCertificate = useCertificate;
            this.UseChecksum = useChecksum;
            if (useCertificate)
                certificate = new X509Certificate2("D:/certificates/certificate.pfx", "vistaprint!");

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

            if (!Utilities.PortManager.IsPortAvailable(tcpServerPort))
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

            if (UseEncryption)
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
            if (UseCertificate)
            {
                try
                {
                    await AuthenticateCertificatesAsync(statusCallback: statusCallback);

                }
                catch (Exception ex)
                {
                    statusCallback?.Invoke($"Fehler beim Authentifizieren des Remote-Zertifikats: {ex.Message}");
                    throw;
                }
            }
            // Schritt 4: Starte das Lauschen auf Nachrichten
            StartListening();
        }

        // Authentifizieren des Remote-Zertifikats
        internal async Task<bool> AuthenticateCertificatesAsync(int timeout = 5000, Action<string> statusCallback = null)
        {
            Task timeoutTask = Task.Delay(timeout); // Timeout-Task

            if (tcpClient == null) throw new InvalidOperationException("TCP-Client ist nicht initialisiert.");

            try
            {
                Task<byte[]> getBytes = ReciveCertificateBytesAsync(statusCallback);
                Task sendBytes = SendCertificateBytesAsync(certificate.RawData);

                Task task = Task.WhenAll(getBytes, sendBytes);
                if (await Task.WhenAny(task, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException("Timeout beim Empfangen des Remote-Zertifikats.");
                }

                var remoteCertBytes = getBytes.Result;
                remoteCertificate = new X509Certificate2(remoteCertBytes);
                if (!remoteCertificate.Verify())
                {
                    if (CertUtils.IsSelfSignedCertificate(remoteCertificate))
                    {
                        statusCallback?.Invoke("Warnung: Das Remote-Zertifikat ist selbst signiert.");
                    }
                    else
                    {
                        statusCallback?.Invoke("Das Remote-Zertifikat ist nicht gültig.");
                        return false;
                    }

                    using (var sha256 = SHA256.Create())
                    {
                        byte[] hash = sha256.ComputeHash(remoteCertificate.RawData);
                        statusCallback?.Invoke($"Zertifikats-Hash (SHA-256): {BitConverter.ToString(hash).Replace("-", "")}");
                    }

                    statusCallback?.Invoke($"Info: Zertifikatinhaber: {CertUtils.GetSubject(certificate, "CN")} {CertUtils.GetSubject(certificate, "I")} ({CertUtils.GetSubject(certificate, "O")})");
                }
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"Fehler beim Überprüfen des Remote-Zertifikats: {ex.Message}");
                throw;
            }
            return true;
        }


        internal async Task<byte[]> ReciveCertificateBytesAsync(Action<string> statusCallback = null)
        {
            // Beispielcode, um Zertifikatdaten asynchron vom Remote-Endpunkt zu empfangen
            NetworkStream stream = tcpClient.GetStream();
            byte[] buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            statusCallback?.Invoke("Zertifikatsdaten empfangen.");
            return buffer.Take(bytesRead).ToArray();
        }

        internal async Task SendCertificateBytesAsync(byte[] certBytes, Action<string> statusCallback = null)
        {
            // Beispielcode, um Zertifikatdaten asynchron an den Remote-Endpunkt zu senden
            NetworkStream stream = tcpClient.GetStream();
            await stream.WriteAsync(certBytes, 0, certBytes.Length);
            statusCallback?.Invoke("Zertifikatsdaten gesendet.");
        }



        public void StopListening()
        {
            cts.Cancel();
        }

        public void StartListening(Action<string> statusCallback = null)
        {
            if (cts != null)
            {

                cts.Cancel();
                statusCallback?.Invoke("Abbruch des Empfangs");
            }
            cts = new CancellationTokenSource();

            ListenForMessages(cts, statusCallback);
            var encr = UseEncryption ? "Verschlüsselte" : "Unverschlüsselte";
            statusCallback?.Invoke($"{encr} Nachrichten werden empfangen");

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

            var e = UseEncryption;
            var f = UseCertificate;
            var g = UseChecksum;
            string _message = $"HSK:Client{DataTypesUtils.btos(e)}{DataTypesUtils.btos(f)}{DataTypesUtils.btos(g)}";
            byte[] handshakeMessage = Encoding.UTF8.GetBytes(_message);

            try
            {
                // Sende eine Handshake-Nachricht
                statusCallback?.Invoke($"Sende Handshake-Nachricht (p{serverPort}): {_message}");
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

                        iAmServer = response.StartsWith("HSK:Client");
                        Role = iAmServer ? "Server" : "Client";
                        statusCallback?.Invoke($"Ich bin der {Role}");

                        var (_e, _f, _g) = (response[10]=='1', response[11]=='1', response[12]=='1');
                        statusCallback?.Invoke($"Eigenschaften: Encrypted {_e} Certificates {_f} Checksum {_g}");
                        var (n_e, n_f, n_g) = (e && _e, f && _f, g && _g);

                        this.UseEncryption = n_e;
                        this.UseCertificate = n_f;
                        this.UseChecksum = n_g;

                        if (iAmServer)
                        {

                            // Gib die Rückmeldung, dass der Handshake erfolgreich war (HSK:Client)
                            handshakeMessage = Encoding.UTF8.GetBytes($"HSK:Server{DataTypesUtils.btos(n_e)}{DataTypesUtils.btos(n_f)}{DataTypesUtils.btos(n_g)}");
                            statusCallback?.Invoke($"Sende Antwort (p{result.RemoteEndPoint.Port})");
                            await udpClient.SendAsync(handshakeMessage, handshakeMessage.Length, result.RemoteEndPoint);
                            statusCallback?.Invoke($"Antwort gesendet");
                        }
                        else
                        {
                            // Gib die Rückmeldung, dass der Handshake erfolgreich war (HSK:Server)
                        }
                        statusCallback?.Invoke($"Info:Encrypted {UseEncryption}, Certificates {UseCertificate}, Checksum {UseChecksum}");

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
            statusCallback?.Invoke($"Handshake abgschlossen.");

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

        public async Task<bool> Reconnect(int udpPort = 0, int tcpServerPort = 0, int udpServerPort = 0, Action<string> statusCallback = null, int nTry = 1)
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
                    await Init(udpPort, tcpServerPort, statusCallback: statusCallback, useEncryption: this.UseEncryption, useChecksum: this.UseChecksum, useCertificate: this.UseCertificate, udpServerPort: udpServerPort);
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

        internal async void ListenForMessages(CancellationTokenSource token, Action<string> statusCallback = null)
        {
            try
            {
                NetworkStream stream = tcpClient.GetStream();
                byte[] buffer = new byte[4096]; // Größeres Buffer für mögliche Signatur- und Checksum-Daten

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
                            byte[] receivedData = buffer.Take(bytesRead).ToArray();
                            byte[] signature = null;

                            // Zertifikats- und Signaturprüfung, falls aktiviert
                            if (UseCertificate)
                            {
                                signature = receivedData.Take(256).ToArray();
                                receivedData = receivedData.Skip(256).ToArray();
                            }

                            // Prüfsumme abtrennen, falls aktiviert
                            if (UseChecksum)
                            {
                                byte[] receivedChecksum = receivedData.Skip(receivedData.Length - 32).ToArray();
                                receivedData = receivedData.Take(receivedData.Length - 32).ToArray();

                                if (!VerifyChecksum(receivedData, receivedChecksum))
                                {
                                    statusCallback?.Invoke("Prüfsummenprüfung fehlgeschlagen. Nachricht ist möglicherweise beschädigt.");
                                    continue;
                                }
                            }

                            string message;
                            if (UseEncryption)
                            {
                                // Nachricht entschlüsseln
                                message = DecryptMessage(receivedData, GetSharedSecretKey());
                            }
                            else
                            {
                                // Nachricht als unverschlüsselt behandeln
                                message = Encoding.UTF8.GetString(receivedData);
                            }

                            // Prüfe die Signatur nach der Entschlüsselung
                            if (UseCertificate && !VerifyMessage(message, signature))
                            {
                                statusCallback?.Invoke("Warnung: Signaturprüfung fehlgeschlagen.");
                                continue;
                            }

                            MessageReceived?.Invoke(this, message);
                        }
                        else
                        {
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
            statusCallback?.Invoke(UseEncryption ? "Verschlüsselte Nachrichten empfangen abgebrochen" : "Unverschlüsselte Nachrichten empfangen abgebrochen");
        }



        public async Task SendMessageAsync(string message, Action<string> statusCallback = null)
        {
            if (tcpClient == null || !tcpClient.Connected)
                throw new InvalidOperationException("Keine Verbindung vorhanden.");

            NetworkStream stream = tcpClient.GetStream();
            byte[] data;
            byte[] signature = null;

            if (UseEncryption)
            {
                // Nachricht verschlüsseln
                data = EncryptMessage(message, GetSharedSecretKey());
            }
            else
            {
                // Nachricht als unverschlüsselt behandeln
                data = Encoding.UTF8.GetBytes(message);
            }
            // Berechne und füge eine Prüfsumme hinzu, falls aktiviert
            if (UseChecksum)
            {
                byte[] checksumData = ComputeChecksum(data);
                data = data.Concat(checksumData).ToArray();
            }
            // Signiere die Nachricht, falls aktiviert
            if (UseCertificate)
            {
                signature = SignMessage(message);
                data = signature.Concat(data).ToArray();
            }

            await stream.WriteAsync(data, 0, data.Length);
            statusCallback?.Invoke(UseEncryption ? "Verschlüsselte Nachricht gesendet." : "Unverschlüsselte Nachricht gesendet.");
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


        // Überprüfen der Integrität und Authentizität der Nachricht
        public bool VerifyMessage(string message, byte[] signature)
        {
            using (var rsa = remoteCertificate.GetRSAPublicKey())
            {
                byte[] dataBytes = Encoding.UTF8.GetBytes(message);
                return rsa.VerifyData(dataBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        // Signiert eine Nachricht für die Authentizität
        public byte[] SignMessage(string message)
        {
            using (var rsa = certificate.GetRSAPrivateKey())
            {
                byte[] dataBytes = Encoding.UTF8.GetBytes(message);
                return rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        // Berechnet eine einfache Prüfsumme
        internal byte[] ComputeChecksum(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }

        // Überprüft die Prüfsumme
        internal bool VerifyChecksum(byte[] data, byte[] checksum )
        {
            byte[] calculatedChecksum = ComputeChecksum(data);

            return checksum.SequenceEqual(calculatedChecksum);
        }


        // Methode, um den Schlüssel zu setzen
        internal void SetSharedSecretKey(byte[] key)
        {
            sharedSecretKey = key;
        }

        // Methode, um den Schlüssel zu erhalten (z.B. für die Verwendung beim Verschlüsseln/Entschlüsseln)
        internal byte[] GetSharedSecretKey()
        {
            return sharedSecretKey;
        }

    }
}
