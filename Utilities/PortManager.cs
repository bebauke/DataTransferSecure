using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DataTransferSecure.Utilities
{
    public static class PortManager
    {
        private static readonly int PortStart = 8500;
        private static readonly int PortEnd = 9500;
        private static readonly ConcurrentQueue<int> AvailablePorts = new ConcurrentQueue<int>();
        private static readonly HashSet<int> InUsePorts = new HashSet<int>();

        static PortManager()
        {                      
            // Initialize the queue with available ports in the range
            for (int port = PortStart; port <= PortEnd; port++)
            {
                AvailablePorts.Enqueue(port);
            }
        }

        public static int GetAvailablePort()
        {
            if (AvailablePorts.TryDequeue(out int port))
            {
                if (!IsPortAvailable(port))
                {
                    throw new InvalidOperationException("Port is not available.");
                }
                lock (InUsePorts)
                {
                    InUsePorts.Add(port);
                }
                return port;
            }
            throw new InvalidOperationException("No available ports in the specified range.");
        }

        public static void ReleasePort(int port)
        {
            if (!IsPortAvailable(port))
            {
                throw new InvalidOperationException("Port is still in use.");
            }
            lock (InUsePorts)
            {
                if (InUsePorts.Remove(port))
                {
                    AvailablePorts.Enqueue(port);
                }
                else
                {
                    throw new InvalidOperationException("Port was not in use.");
                }
            }
        }

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
    }
}

