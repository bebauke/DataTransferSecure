using System.Net;
using System.Net.NetworkInformation;

namespace DataTransferSecure.Utilities
{
    public static class NetworkUtils
    {
        // Methode zur Überprüfung, ob ein Port verfügbar ist
        public static bool IsPortAvailable(int port)
        {
            // Überprüft, ob der Port bereits von einer Anwendung verwendet wird
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpEndPoints = ipGlobalProperties.GetActiveTcpListeners();
            IPEndPoint[] udpEndPoints = ipGlobalProperties.GetActiveUdpListeners();

            foreach (var endPoint in tcpEndPoints)
            {
                if (endPoint.Port == port) return false; // TCP-Port belegt
            }

            foreach (var endPoint in udpEndPoints)
            {
                if (endPoint.Port == port) return false; // UDP-Port belegt
            }

            return true; // Port ist verfügbar
        }

        public static string GetLocalIPAddress()
        {
            string localIP = "127.0.0.1"; // Standardmäßig auf localhost setzen
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        localIP = ip.ToString();
                        break;
                    }
                }
            }
            catch
            {
                // Fehlerbehandlung, falls die IP nicht abgerufen werden kann
                localIP = "Fehler beim Abrufen der IP";
            }
            return localIP;
        }
    }
}
