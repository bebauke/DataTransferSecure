using System;
using System.Threading.Tasks;
using Xunit; // If using xUnit

using DataTransferSecure.Services;
using DataTransferSecure.Utilities;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataTransferSecure.Tests
{
    public class CommunicatorIntegrationTests
    {
        private Communicator serverCommunicator;
        private Communicator clientCommunicator;

        public CommunicatorIntegrationTests()
        {
            serverCommunicator = new Communicator();
            clientCommunicator = new Communicator();
        }

        [Fact]
        async Task T01_UDP_Handshake_ShouldDefineRoles()
        {
            // Arrange: Erstelle zwei Communicator-Instanzen
            Communicator communicator_a = new Communicator();
            Communicator communicator_b = new Communicator();

            List<string> statusMessages = new List<string>();

            // Starte den Server und Client mit einem kleinen Zeitunterschied
            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            try
            {
                Lazy<Task> serverTask;
                Lazy<Task> clientTask;

                // Port A wird als "Default-Port" für den Server verwendet. -> einer der Teilnehmer muss den Port des anderen kennen.
                // Theoretisch wird der Default-Port der Anwendung festgelegt. Da beide Teilnehmer auf der gleichen IPAdresse sind,
                // müssen hier von einander verschiedene Ports verwendet werden. Somit bilden diese Tests aber auch nicht den fall ab, dass
                // die Teilnehmer auf unterschiedlichen IP-Adressen  aber dem gleichen Port lauschen....
                serverTask = new Lazy<Task>(() => communicator_a.PerformUDPHandshake(port_a, port_a, status => { statusMessages.Add($"Status S:{status + " " + DateTime.Now.ToString("mm:ss.fff")}"); }));

                clientTask = new Lazy<Task>(() => communicator_b.PerformUDPHandshake(port_b, port_a, status => { statusMessages.Add($"Status C:{status + " " + DateTime.Now.ToString("mm:ss.fff")}"); }));
                // Act: Führe die Initialisierung aus
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Überprüfe die Rollenverteilung
            Assert.Equal("Server", communicator_a.Role);
            Assert.Equal("Client", communicator_b.Role);

            communicator_a.Disconnect();
            communicator_b.Disconnect();

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);

        }

        [Fact]
        async Task T02_TCP_Connection_ShouldBeEstablished()
        {
            // Arrange: Erstelle zwei Communicator-Instanzen
            Communicator communicator_a = new Communicator() { Role = "Server", iAmServer = true };
            Communicator communicator_b = new Communicator() { Role = "Client", iAmServer = false };

            List<string> statusMessages = new List<string>();

            int port_server = PortManager.GetAvailablePort();

            // Starte den Server und Client mit einem kleinen Zeitunterschied
            try
            {
                Lazy<Task> serverTask;
                Lazy<Task> clientTask;
                if (Utilities.NetworkUtils.IsPortAvailable(9000) && Utilities.NetworkUtils.IsPortAvailable(9100))
                {
                    serverTask = new Lazy<Task>(() => communicator_a.SetupTcpConnection(port_server, Utilities.NetworkUtils.GetLocalIPAddress(), status => { statusMessages.Add($"Status S:{status + " " + DateTime.Now.ToString("mm:ss.fff")}"); }));
                    clientTask = new Lazy<Task>(() => communicator_b.SetupTcpConnection(port_server, Utilities.NetworkUtils.GetLocalIPAddress(), status => { statusMessages.Add($"Status C:{status + " " + DateTime.Now.ToString("mm:ss.fff")}"); }));
                }
                else
                {
                    throw new Exception("Ports are not available");
                }

                // Act: Führe die Initialisierung aus
                await Task.WhenAll(serverTask.Value, clientTask.Value);

                // Assert: Überprüfe, ob die Verbindung erfolgreich hergestellt wurde
                Assert.True(communicator_a.IsConnected());
                Assert.True(communicator_b.IsConnected());

                Assert.True(communicator_a.tcpClient.Connected);
                Assert.True(communicator_b.tcpClient.Connected);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            communicator_a.Disconnect();
            communicator_b.Disconnect();

            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T03_DiffieHellman_KeyExchange_ShouldGenerateSameKeys()
        {
            // Arrange: Erstelle zwei Communicator-Instanzen
            Communicator communicator_a = new Communicator() { Role = "Server", iAmServer = true };
            Communicator communicator_b = new Communicator() { Role = "Client", iAmServer = false };

            List<string> statusMessages = new List<string>();

            int port_server = PortManager.GetAvailablePort();

            try
            {
                // Setup der TCP-Verbindung
                Task serverTask = communicator_a.SetupTcpConnection(port_server, Utilities.NetworkUtils.GetLocalIPAddress(),
                    status => statusMessages.Add($"Status S:{status}"));

                Task clientTask = communicator_b.SetupTcpConnection(port_server, Utilities.NetworkUtils.GetLocalIPAddress(),
                    status => statusMessages.Add($"Status C:{status}"));

                await Task.WhenAll(serverTask, clientTask);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            try
            {
                // Diffie-Hellman-Schlüsselaustausch
                int timeout = 5000;
                Task<byte[]> serverKeyTask = communicator_a.PerformDiffieHellmanKeyExchange(timeout, status => statusMessages.Add($"Status S:{status}"));
                Task<byte[]> clientKeyTask = communicator_b.PerformDiffieHellmanKeyExchange(timeout, status => statusMessages.Add($"Status C:{status}"));

                // Warte auf die Ergebnisse des Schlüsselaustauschs
                await Task.WhenAll(serverKeyTask, clientKeyTask);

                // Assert: Überprüfe, ob die abgeleiteten Schlüssel identisch sind
                byte[] serverKey = serverKeyTask.Result;
                byte[] clientKey = clientKeyTask.Result;
                Assert.Equal(serverKey, clientKey);

                // Überprüfe, ob die Schlüssel gültig sind (z.B. nicht leer)
                Assert.NotNull(serverKey);
                Assert.NotNull(clientKey);
                Assert.True(serverKey.Length > 0);
                Assert.True(clientKey.Length > 0);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            communicator_a.Disconnect();
            communicator_b.Disconnect();

            PortManager.ReleasePort(port_server);

        }


        [Fact]
        public async Task T04_SetUpClientAndServer_ShouldConnectAndExchangeMessages()
        {
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => { statusMessages.Add($"Server: {status}"); }));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => { statusMessages.Add($"Client: {status}"); }));

            try
            {
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            bool serverConnected = serverCommunicator.IsConnected();
            bool clientConnected = clientCommunicator.IsConnected();

            // Assert
            Assert.True(serverConnected, "Server did not establish a connection.");
            Assert.True(clientConnected, "Client did not establish a connection.");

            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);

        }

        [Fact]
        public async Task T05a_MessageExchange_ShouldSendAndReceiveCorrectMessages()
        {
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            // Initialize server and client for unencrypted communication
            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(udpPort: port_a, udpServerPort: port_a, tcpServerPort: port_server, statusCallback: status => { statusMessages.Add($"Server: {status}"); }, useEncryption: false, useCertificate:false, useChecksum:false));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(udpPort: port_b, udpServerPort: port_a, tcpServerPort: port_server, statusCallback: status => { statusMessages.Add($"Client: {status}"); }, useEncryption: false, useCertificate: false, useChecksum: false));

            try
            {
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Arrange
            string messageToSend = "Hello from Client!";
            string receivedMessage = string.Empty;

            serverCommunicator.MessageReceived += (sender, message) =>
            {
                receivedMessage = message;
            };

            // Act
            await clientCommunicator.SendMessageAsync(messageToSend, status => { statusMessages.Add($"Client: {status}"); });
            await Task.Delay(500); // Small delay to allow the message to be transmitted

            // Assert
            try
            {
                Assert.Equal(messageToSend, receivedMessage);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }


        [Fact]
        public async Task T05b_MessageExchangeEncrypted_ShouldSendAndReciveEncrypted()
        {
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(port_a, udpServerPort: port_a, tcpServerPort: port_server, useEncryption: true, useCertificate: false, useChecksum: false, statusCallback: status => { statusMessages.Add($"Server: {status}"); }));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(port_b, udpServerPort: port_a, tcpServerPort: port_server, useEncryption: true, useCertificate: false, useChecksum: false, statusCallback: status => { statusMessages.Add($"Client: {status}"); }));

            try
            {
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Arrange
            string messageToSend = "Hello from Client!";
            string receivedMessage = string.Empty;

            // Set up a listener on the server to receive the message
            serverCommunicator.MessageReceived += (sender, message) =>
            {
                receivedMessage = message;
            };

            // Act
            await clientCommunicator.SendMessageAsync(messageToSend);
            await Task.Delay(500); // Small delay to allow the message to be transmitted

            // Assert
            Assert.Equal(messageToSend, receivedMessage);

            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T05c_MessageExchangeCertificate_ShouldSendAndReciveEncrypted()
        {
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(port_a, udpServerPort: port_a, tcpServerPort: port_server, useEncryption: true, useCertificate: true, useChecksum: false, statusCallback: status => { statusMessages.Add($"Server: {status}"); }));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(port_b, udpServerPort: port_a, tcpServerPort: port_server, useEncryption: true, useCertificate: true, useChecksum: false, statusCallback: status => { statusMessages.Add($"Client: {status}"); }));

            try
            {
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Arrange
            string messageToSend = "Hello from Client!";
            string receivedMessage = string.Empty;

            // Set up a listener on the server to receive the message
            serverCommunicator.MessageReceived += (sender, message) =>
            {
                receivedMessage = message;
            };

            // Act
            await clientCommunicator.SendMessageAsync(messageToSend);
            await Task.Delay(500); // Small delay to allow the message to be transmitted

            // Assert
            Assert.Equal(messageToSend, receivedMessage);

            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T05d_MessageExchangeChecksum_ShouldSendAndReciveEncrypted()
        {
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(port_a, udpServerPort: port_a, tcpServerPort: port_server, useEncryption: false, useCertificate: false, useChecksum: true, statusCallback: status => { statusMessages.Add($"Server: {status}"); }));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(port_b, udpServerPort: port_a, tcpServerPort: port_server, useEncryption: false, useCertificate: false, useChecksum: true, statusCallback: status => { statusMessages.Add($"Client: {status}"); }));

            try
            {
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Arrange
            string messageToSend = "Hello from Client!";
            string receivedMessage = string.Empty;

            // Set up a listener on the server to receive the message
            serverCommunicator.MessageReceived += (sender, message) =>
            {
                receivedMessage = message;
            };

            // Act
            await clientCommunicator.SendMessageAsync(messageToSend);
            await Task.Delay(500); // Small delay to allow the message to be transmitted

            try
            {
            // Assert
            Assert.Equal(messageToSend, receivedMessage);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T06a_ServerDisconnection_ShouldCloseConnectionGracefully()
        {
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => { statusMessages.Add($"Server: {status}"); }));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => { statusMessages.Add($"Client: {status}"); }));

            try
            {
                // Initialize server and client
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Act: Disconnect the server
            serverCommunicator.Disconnect();

            // Allow a short delay for the disconnection to take effect
            await Task.Delay(200);

            // Check the connection states
            bool isClientConnected = clientCommunicator.IsConnected();
            bool isServerConnected = serverCommunicator.IsConnected();

            // Assert
            Assert.False(isServerConnected, "Server did not disconnect successfully.");
            Assert.False(isClientConnected, "Client did not detect the disconnection.");

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T06b_ClientDisconnection_ShouldCloseConnectionGracefully()
        {
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => { statusMessages.Add($"Server: {status}"); }));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => { statusMessages.Add($"Client: {status}"); }));

            try
            {
                // Initialize server and client
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Act: Disconnect the client
            clientCommunicator.Disconnect();

            // Allow a short delay for the disconnection to take effect
            await Task.Delay(200);

            // Check the connection states
            bool isClientConnected = clientCommunicator.IsConnected();
            bool isServerConnected = serverCommunicator.IsConnected();

            // Assert
            Assert.False(isClientConnected, "Client did not disconnect successfully.");
            Assert.False(isServerConnected, "Server did not detect the disconnection.");

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T06c_BothDisconnection_ShouldCloseConnectionGracefully()
        {
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => { statusMessages.Add($"Server: {status}"); }));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => { statusMessages.Add($"Client: {status}"); }));

            try
            {
                // Initialize server and client
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Act: Disconnect both the server and the client
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();

            // Allow a short delay for the disconnection to take effect
            await Task.Delay(200);

            // Check the connection states
            bool isClientConnected = clientCommunicator.IsConnected();
            bool isServerConnected = serverCommunicator.IsConnected();

            // Assert
            Assert.False(isServerConnected, "Server did not disconnect successfully.");
            Assert.False(isClientConnected, "Client did not disconnect successfully.");

            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T07_Reconnection_ShouldWorkAfterDisconnection()
        {
            // Arrange: Get available ports
            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            try
            {
                // Initialize and start both server and client
                await Task.WhenAll(
                    serverCommunicator.Init(port_a, port_server, udpServerPort: port_a),
                    clientCommunicator.Init(port_b, port_server, udpServerPort: port_a)
                );

                // Verify initial connection
                Assert.True(serverCommunicator.IsConnected(), "Server should be connected initially.");
                Assert.True(clientCommunicator.IsConnected(), "Client should be connected initially.");

                // Act: Disconnect the client and allow time for the disconnection to propagate
                clientCommunicator.Disconnect();
                await Task.Delay(500); // Allow disconnection to propagate

                // Verify that client is disconnected and server should detect the disconnection
                Assert.False(clientCommunicator.IsConnected(), "Client should be disconnected.");
                Assert.False(serverCommunicator.IsConnected(), "Server should detect client disconnection.");

                // Act: Reconnect both server and client
                try
                {
                    await Task.WhenAll(
                        serverCommunicator.Reconnect(port_a, port_server, udpServerPort: port_a),
                        clientCommunicator.Reconnect(port_b, port_server, udpServerPort: port_a)
                    );
                }
                catch (Exception e)
                {
                    throw new Exception("Reconnection failed: " + e.Message);
                }

                // Verify reconnection for both server and client
                bool isServerConnected = serverCommunicator.IsConnected();
                bool isClientConnected = clientCommunicator.IsConnected();
                Assert.True(isServerConnected, "Server did not reconnect successfully.");
                Assert.True(isClientConnected, "Client did not reconnect successfully.");

                // Clean up: Disconnect both server and client
                serverCommunicator.Disconnect();
                clientCommunicator.Disconnect();

                // Verify final disconnection
                Assert.False(clientCommunicator.IsConnected(), "Client should be disconnected after final cleanup.");
                Assert.False(serverCommunicator.IsConnected(), "Server should be disconnected after final cleanup.");
            }
            finally
            {
                // Release ports
                PortManager.ReleasePort(port_a);
                PortManager.ReleasePort(port_b);
                PortManager.ReleasePort(port_server);
            }
        }

        [Fact]
        public async Task T08_CertificateAuthentication_ShouldVerifyRemoteCertificate()
        {
            // Arrange: Get available ports
            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();
            List<string> statusMessages = new List<string>();

            // Act: Initialize server and client
            try
            {
                await Task.WhenAll(
                    serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Server: {status}")),
                    clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Client: {status}"))
                );
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Check if Certificates are Selfsigned
            Assert.False(CertUtils.IsSelfSignedCertificate(serverCommunicator.certificate), "Server certificate is self-signed.");
            Assert.False(CertUtils.IsSelfSignedCertificate(clientCommunicator.certificate), "Client certificate is self-signed.");

            // Assert: Check if both communicators verified the remote certificate
            Assert.NotNull(serverCommunicator.remoteCertificate);
            Assert.NotNull(clientCommunicator.remoteCertificate);

            // Further verify that the certificates are valid (you can use additional checks if needed)
            Assert.True(serverCommunicator.remoteCertificate.Verify(), "Server certificate could not be verified.");
            Assert.True(clientCommunicator.remoteCertificate.Verify(), "Client certificate could not be verified.");

            // Clean up
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();
            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T09_MessageSigning_ShouldVerifySignedMessages()
        {
            // Arrange: Get available ports
            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();
            List<string> statusMessages = new List<string>();

            // Act: Initialize server and client
            try
            {
                await Task.WhenAll(
                    serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Server: {status}")),
                    clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Client: {status}"))
                );
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Arrange: Sign a message and verify it on the server side
            string messageToSend = "This is a signed message.";
            byte[] signature = clientCommunicator.SignMessage(messageToSend);

            // Assert: Verify the message signature on the server side
            bool isSignatureValid = serverCommunicator.VerifyMessage(messageToSend, signature);
            Assert.True(isSignatureValid, "The message signature could not be verified.");

            // Clean up
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();
            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T10_EncryptedMessageExchangeWithCertificates_ShouldSendAndReceiveCorrectMessages()
        {
            // Arrange: Get available ports
            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();
            List<string> statusMessages = new List<string>();

            // Act: Initialize server and client with encryption
            try
            {
                await Task.WhenAll(
                    serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Server: {status}"), useEncryption: true),
                    clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Client: {status}"), useEncryption: true)
                );
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Arrange: Prepare a message to be sent and set up a listener for the message on the server
            string messageToSend = "Hello from Client (encrypted with certificate)!";
            string receivedMessage = string.Empty;
            serverCommunicator.MessageReceived += (sender, message) => receivedMessage = message;

            // Act: Send an encrypted message
            await clientCommunicator.SendMessageAsync(messageToSend);
            await Task.Delay(500); // Small delay to allow the message to be transmitted

            // Assert: Check if the message was received correctly
            Assert.Equal(messageToSend, receivedMessage);

            // Clean up
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();
            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        //[Fact]
        //public async Task T11_ManInTheMiddleAttack_ShouldDetectTamperedMessages()
        //{
        //    // Arrange: Erstelle eine Verbindung zwischen Server und Client
        //    List<string> statusMessages = new List<string>();
        //    int port_a = PortManager.GetAvailablePort();
        //    int port_b = PortManager.GetAvailablePort();
        //    int port_server = PortManager.GetAvailablePort();

        //    await Task.WhenAll(
        //        serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Server: {status}")),
        //        clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Client: {status}"))
        //    );

        //    // Erstelle eine Man-in-the-Middle Instanz
        //    var mitm = new ManInTheMiddle(clientCommunicator, serverCommunicator);

        //    bool messageRejected = false;
        //    serverCommunicator.MessageReceived += (sender, message) =>
        //    {
        //        messageRejected = message == null || message.Contains("Prüfsummenprüfung fehlgeschlagen");
        //    };

        //    // Manipuliere eine Nachricht und sende sie
        //    string originalMessage = "This is a secure message.";
        //    await mitm.InterceptAndModifyMessage(originalMessage, originalData =>
        //    {
        //        // Modifiziere die Nachricht, um die Prüfsumme zu invalidieren
        //        return originalData.Concat(new byte[] { 0xFF }).ToArray();
        //    });

        //    await Task.Delay(500); // Verzögerung, um die Nachricht zu empfangen

        //    // Assert: Die Nachricht sollte abgelehnt werden
        //    Assert.True(messageRejected, "Die manipulierte Nachricht wurde nicht abgelehnt.");

        //    // Clean up
        //    serverCommunicator.Disconnect();
        //    clientCommunicator.Disconnect();
        //    PortManager.ReleasePort(port_a);
        //    PortManager.ReleasePort(port_b);
        //    PortManager.ReleasePort(port_server);
        //}


        [Fact]
        public async Task T12_ConcurrentMessageSending_ShouldHandleMultipleMessages()
        {
            // Arrange: Initialisiere Server und Client
            List<string> statusMessages = new List<string>();
            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            await Task.WhenAll(
                serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Server: {status}")),
                clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Client: {status}"))
            );

            string[] messagesToSend = { "Message 1", "Message 2", "Message 3", "Message 4", "Message 5" };
            List<string> receivedMessages = new List<string>();

            serverCommunicator.MessageReceived += (sender, message) => receivedMessages.Add(message);

            // Act: Sende alle Nachrichten gleichzeitig
            List<Task> sendTasks = messagesToSend.Select(msg => clientCommunicator.SendMessageAsync(msg)).ToList();
            await Task.WhenAll(sendTasks);
            await Task.Delay(1000); // Verzögerung, um alle Nachrichten zu empfangen

            // Assert: Überprüfe, ob alle Nachrichten empfangen wurden
            Assert.Equal(messagesToSend.Length, receivedMessages.Count);
            foreach (var message in messagesToSend)
            {
                Assert.Contains(message, receivedMessages);
            }

            // Clean up
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();
            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        //[Fact]
        //public async Task T13_LargeDataTransfer_ShouldHandleLargeMessages()
        //{
        //    // Arrange: Initialisiere Server und Client
        //    List<string> statusMessages = new List<string>();
        //    int port_a = PortManager.GetAvailablePort();
        //    int port_b = PortManager.GetAvailablePort();
        //    int port_server = PortManager.GetAvailablePort();

        //    await Task.WhenAll(
        //        serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Server: {status}")),
        //        clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, statusCallback: status => statusMessages.Add($"Client: {status}"))
        //    );

        //    string largeMessage = new string('A', 10000); // 1 MB Nachricht 1000000
        //    string receivedMessage = string.Empty;

        //    serverCommunicator.MessageReceived += (sender, message) => receivedMessage = message;

        //    // Act: Sende die große Nachricht
        //    await clientCommunicator.SendMessageAsync(largeMessage);
        //    await Task.Delay(2000); // Verzögerung, um die große Nachricht zu empfangen

        //    // Assert: Die Nachricht sollte korrekt empfangen werden
        //    Assert.Equal(largeMessage, receivedMessage);

        //    // Clean up
        //    serverCommunicator.Disconnect();
        //    clientCommunicator.Disconnect();
        //    PortManager.ReleasePort(port_a);
        //    PortManager.ReleasePort(port_b);
        //    PortManager.ReleasePort(port_server);
        //}


        [Fact]
        public async Task T14_MessageIntegrity_ShouldDetectTampering()
        {
            // Arrange: Initialisiere Server und Client
            List<string> statusMessages = new List<string>();
            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            await Task.WhenAll(
                serverCommunicator.Init(port_a, port_server, udpServerPort: port_a, useEncryption: true, useCertificate: false, useChecksum: false, statusCallback: status => statusMessages.Add($"Server: {status}")),
                clientCommunicator.Init(port_b, port_server, udpServerPort: port_a, useEncryption: true, useCertificate: false, useChecksum: false, statusCallback: status => statusMessages.Add($"Client: {status}"))
            );

            string originalMessage = "This is a secure message.";
            string tamperedMessage = "This is a tampered message.";
            byte[] tamperedData = Encoding.UTF8.GetBytes(tamperedMessage);

            // Füge eine gültige Prüfsumme hinzu, um die Manipulation zu verbergen
            byte[] checksum = clientCommunicator.ComputeChecksum(tamperedData);
            byte[] tamperedDataWithChecksum = tamperedData.Concat(checksum).ToArray();

            bool tamperingDetected = false;
            serverCommunicator.MessageReceived += (sender, message) =>
            {
                if (message != originalMessage)
                    tamperingDetected = true;
            };

            // Act: Sende die manipulierte Nachricht
            await clientCommunicator.SendMessageAsync(Encoding.UTF8.GetString(tamperedDataWithChecksum));
            await Task.Delay(500); // Verzögerung, um die Nachricht zu empfangen

            // Assert: Die Manipulation sollte erkannt werden
            Assert.True(tamperingDetected, "Die Manipulation wurde nicht erkannt.");

            // Clean up
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();
            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T15a_TechnologyAgreement_ShouldAgreeOnEncryptionTechnology()
        {
            // Arrange: Initialisiere Server und Client mit unterschiedlicher Technologieanforderung
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            // Server möchte Verschlüsselung und Zertifikate, aber keine Prüfsummen
            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(
                port_a, port_server, udpServerPort: port_a,
                useEncryption: true, useCertificate: true, useChecksum: false,
                statusCallback: status => statusMessages.Add($"Server: {status}")
            ));

            // Client möchte Verschlüsselung und Prüfsummen, aber keine Zertifikate
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(
                port_b, port_server, udpServerPort: port_a,
                useEncryption: true, useCertificate: false, useChecksum: true,
                statusCallback: status => statusMessages.Add($"Client: {status}")
            ));

            try
            {
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                // only last 8 entryes of strings
                // throw new Exception("\n" + string.Join("\n", statusMessages.Skip(Math.Max(0, statusMessages.Count() - 15))) + "\n" + e.Message);
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Assert: Überprüfe die Einigung auf Technologien
            // Beide sollten sich auf "useEncryption = true" einigen, aber "useCertificate" und "useChecksum" werden abgelehnt
            Assert.True(serverCommunicator.UseEncryption && clientCommunicator.UseEncryption, "Verschlüsselung sollte aktiviert sein.");
            Assert.False(serverCommunicator.UseChecksum || clientCommunicator.UseChecksum, "Prüfsummen sollten nicht aktiviert sein.");
            Assert.False(serverCommunicator.UseCertificate || clientCommunicator.UseCertificate, "Zertifikate sollten nicht aktiviert sein.");

            // Clean up
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();
            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T15b_TechnologyAgreement_ShouldAgreeOnCertificateAuthentication()
        {
            // Arrange: Initialisiere Server und Client
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            // Server möchte Zertifikate, keine Verschlüsselung und keine Prüfsummen
            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(
                port_a, port_server, udpServerPort: port_a,
                useEncryption: false, useCertificate: true, useChecksum: false,
                statusCallback: status => statusMessages.Add($"Server: {status}")
            ));

            // Client möchte ebenfalls Zertifikate, aber ohne Verschlüsselung und ohne Prüfsummen
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(
                port_b, port_server, udpServerPort: port_a,
                useEncryption: false, useCertificate: true, useChecksum: false,
                statusCallback: status => statusMessages.Add($"Client: {status}")
            ));

            try
            {
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Assert: Beide sollten sich auf die Nutzung von Zertifikaten einigen, ohne Verschlüsselung oder Prüfsummen
            Assert.True(serverCommunicator.UseCertificate && clientCommunicator.UseCertificate, "Zertifikate sollten aktiviert sein.");
            Assert.False(serverCommunicator.UseEncryption || clientCommunicator.UseEncryption, "Verschlüsselung sollte nicht aktiviert sein.");
            Assert.False(serverCommunicator.UseChecksum || clientCommunicator.UseChecksum, "Prüfsummen sollten nicht aktiviert sein.");

            // Clean up
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();
            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }

        [Fact]
        public async Task T15c_TechnologyAgreement_ShouldAgreeOnChecksumVerification()
        {
            // Arrange: Initialisiere Server und Client
            List<string> statusMessages = new List<string>();

            int port_a = PortManager.GetAvailablePort();
            int port_b = PortManager.GetAvailablePort();
            int port_server = PortManager.GetAvailablePort();

            // Server möchte Prüfsummen und keine Verschlüsselung oder Zertifikate
            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(
                port_a, port_server, udpServerPort: port_a,
                useEncryption: false, useCertificate: false, useChecksum: true,
                statusCallback: status => statusMessages.Add($"Server: {status}")
            ));

            // Client möchte ebenfalls Prüfsummen, aber ohne Verschlüsselung und ohne Zertifikate
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(
                port_b, port_server, udpServerPort: port_a,
                useEncryption: false, useCertificate: false, useChecksum: true,
                statusCallback: status => statusMessages.Add($"Client: {status}")
            ));

            try
            {
                await Task.WhenAll(serverTask.Value, clientTask.Value);
            }
            catch (Exception e)
            {
                throw new Exception("\n" + string.Join("\n", statusMessages) + "\n" + e.Message);
            }

            // Assert: Beide sollten sich auf die Nutzung von Prüfsummen einigen, ohne Verschlüsselung oder Zertifikate
            Assert.True(serverCommunicator.UseChecksum && clientCommunicator.UseChecksum, "Prüfsummen sollten aktiviert sein.");
            Assert.False(serverCommunicator.UseEncryption || clientCommunicator.UseEncryption, "Verschlüsselung sollte nicht aktiviert sein.");
            Assert.False(serverCommunicator.UseCertificate || clientCommunicator.UseCertificate, "Zertifikate sollten nicht aktiviert sein.");

            // Clean up
            serverCommunicator.Disconnect();
            clientCommunicator.Disconnect();
            PortManager.ReleasePort(port_a);
            PortManager.ReleasePort(port_b);
            PortManager.ReleasePort(port_server);
        }


    }
}