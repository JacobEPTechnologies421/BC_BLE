using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using BoatControl.Communication.Connections.Shared.Searching;
using Microsoft.Extensions.Logging;

namespace BoatControl.Communication.Connections.Tcp.Searching
{
    public class UdpBroadcastSearch : IBroadcastSearch
    {
        private readonly ILogger<UdpBroadcastSearch> _logger;
        private static byte[] _searchString = Encoding.UTF8.GetBytes("boatcontrol-search");
        private UdpClient _client;
        private Action<FoundLocalDevice> _foundDevice;
        private ConcurrentDictionary<string, DateTime> _lastSeen = new ConcurrentDictionary<string, DateTime>();
        private TimeSpan _intervalBetweenReporting = TimeSpan.FromSeconds(20);


        public UdpBroadcastSearch(ILogger<UdpBroadcastSearch> logger)
        {
            
        }

        public void StartSearching(Action<FoundLocalDevice> foundDevice)
        {
            _foundDevice = foundDevice;
            this._client = new UdpClient();
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _client.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.
            _client.EnableBroadcast = true;
            var broadcastAddress = new IPEndPoint(IPAddress.Any, 16021);
            _client.Client.Bind(broadcastAddress);
            new Thread(BroadcastSearch).Start();
            new Thread(StartListening).Start();
        }


        private void BroadcastSearch()
        {
            while (_foundDevice != null)
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork && adapter.OperationalStatus == OperationalStatus.Up && !adapter.IsReceiveOnly)
                        {
                            var broadcastAddress = GetBroadcastAddress(unicastIPAddressInformation.Address, unicastIPAddressInformation.IPv4Mask);
                            try
                            {
                                _client.Send(_searchString, _searchString.Length, new IPEndPoint(broadcastAddress, 16021));

                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "Error in StartListening");
                            }

                        }
                    }
                }
                Thread.Sleep(5000);
            }
        }

        private void StartListening()
        {
            try {
                _client.BeginReceive(new AsyncCallback(Receive), null);

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in StartListening");
            }
        }
        private void Receive(IAsyncResult ar)
        {
            try
            {
                IPEndPoint remoteIp = new IPEndPoint(IPAddress.Any, 16021);
                byte[] bytes = _client.EndReceive(ar, ref remoteIp);
                string message = Encoding.ASCII.GetString(bytes);

                var match = Regex.Match(message, "\\[BoatControl-(?<id>[^\\]]+)\\]");
                if (match.Success)
                {
                    FoundDeviceNumber(match.Groups["id"].Value, remoteIp);
                }
            }
            catch(Exception e)
            {
                _logger.LogError(e,"Error during receive");
            }

            if (_foundDevice != null)
                StartListening();
        }

        private IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }
        private void FoundDeviceNumber(string deviceNumber, IPEndPoint remoteIp)
        {
            if (!_lastSeen.TryGetValue(deviceNumber, out var lastSeen) || (DateTime.Now - lastSeen) > _intervalBetweenReporting)
            {
                _lastSeen[deviceNumber] = DateTime.Now;
                _foundDevice?.Invoke(new FoundLocalDevice()
                {
                    Number = deviceNumber,
                    HostOnLocalNetwork = remoteIp.Address.ToString()
                });
            }
        }


        public void StopSearching()
        {
            _foundDevice = null;
            _client.Dispose();
            _client = null;
        }
    }
}
