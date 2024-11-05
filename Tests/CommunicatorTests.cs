using System;
using System.Threading.Tasks;
using Xunit; // If using xUnit

using DataTransferSecure.Services;
using DataTransferSecure.Utilities;
using Xunit.Sdk;
using System.Collections.Generic;
using System.Linq;

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
            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(udpPort: port_a, udpServerPort: port_a, tcpServerPort: port_server, statusCallback: status => { statusMessages.Add($"Server: {status}"); }, encrypted: false));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(udpPort: port_b, udpServerPort: port_a, tcpServerPort: port_server, statusCallback: status => { statusMessages.Add($"Client: {status}"); }, encrypted: false));

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
            await clientCommunicator.SendMessageAsync_unencrypted(messageToSend, status => { statusMessages.Add($"Client: {status}"); });
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

            Lazy<Task> serverTask = new Lazy<Task>(() => serverCommunicator.Init(port_a, udpServerPort: port_a, tcpServerPort: port_server, statusCallback: status => { statusMessages.Add($"Server: {status}"); }));
            Lazy<Task> clientTask = new Lazy<Task>(() => clientCommunicator.Init(port_b, udpServerPort: port_a, tcpServerPort: port_server, statusCallback: status => { statusMessages.Add($"Client: {status}"); }));

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
                        serverCommunicator.Reconnect(port_a, port_server, udpServerPort: port_a, encrypted: false),
                        clientCommunicator.Reconnect(port_b, port_server, udpServerPort: port_a, encrypted: false)
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

    }
}