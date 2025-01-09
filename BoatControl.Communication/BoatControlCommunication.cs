using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BoatControl.Communication.Connections;
using BoatControl.Communication.Connections.Ble.Searching;
using BoatControl.Communication.Connections.Shared.Searching;
using BoatControl.Communication.Connections.Tcp.Searching;
using BoatControl.Communication.Models;
using BoatControl.Communication.Storage;
using BoatControl.Shared.Messaging;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoatControl.Communication
{
    public class BoatControlCommunication : IBoatControlCommunication
    {
        public static BoatControlCommunication Instance;
        private readonly IServiceProvider _serviceProvider;
        private readonly IBroadcastSearch _broadcastSearch;
        private readonly IPersistedStorage _persistedStorage;
        private BoatControlDeviceSearching _deviceSearching;

        public AuthenticationUser Owner { get; private set; }
        public event IsCloudConnectedChangeDelegate IsCloudConnectedChangeEvent;
        public event ConnectionChangeDelegate OnConnectionChange;
        public event DeviceMessageDelegate OnDeviceMessage;

        public IDictionary<DeviceInfo, IDeviceConnectionManager> Devices { get; } = new ConcurrentDictionary<DeviceInfo, IDeviceConnectionManager>();



        public BoatControlCommunication(IServiceProvider serviceProvider, IBroadcastSearch broadcastSearch, IPersistedStorage persistedStorage)
        {
            this._serviceProvider = serviceProvider;
            _broadcastSearch = broadcastSearch;
            _persistedStorage = persistedStorage;
        }

        public void Start(AuthenticationUser owner)
        {
            if (Owner != null)
            {
                // Must stop and restart, as this will happens when switching user
                Stop();
            }

            Owner = owner;
            _deviceSearching = new BoatControlDeviceSearching(
                _serviceProvider.GetRequiredService<ILogger<BoatControlDeviceSearching>>(),
                _broadcastSearch, owner);
            _deviceSearching.StartSearching(
                SearchingFoundDevice,
                async isConnected => IsCloudConnected = isConnected
            );
        }

        private void SearchingFoundDevice(AFoundDevice device)
        {
            IDeviceConnectionManager deviceConn;
            if (!Devices.TryGetValue(device, out deviceConn))
            {
                Devices[device] = (deviceConn = new DeviceConnectionManager(
                    _serviceProvider.GetRequiredService<ILogger<DeviceConnectionManager>>(),
                    _serviceProvider,
                    Owner, device, _persistedStorage));
                deviceConn.OnConnectionChange += connection => OnConnectionChange?.Invoke(connection);
                deviceConn.OnDeviceMessage += (connection, message) => OnDeviceMessage?.Invoke(connection, message);
            }

            if (device is FoundLocalDevice localDevice)
            {
                deviceConn.SetFoundLocally(localDevice);
            }
            else if (device is FoundCloudDevice cloudDevice)
            {
                deviceConn.SetFoundInCloud(cloudDevice);
            }
            else if (device is FoundBleDevice bleDevice)
            {
                deviceConn.SetFoundBle(bleDevice);
            }
        }

        public void Dispose()
        {
            _deviceSearching.StopSearching();
            foreach (var device in Devices)
            {
                device.Value.Dispose();
            }
        }


        private bool? _isCloudConnected;

        public bool? IsCloudConnected
        {
            get => _isCloudConnected;
            internal set
            {
                _isCloudConnected = value;
                IsCloudConnectedChangeEvent?.Invoke(value.Value);
            }
        }

        public void Stop()
        {
            try
            {
                _deviceSearching.StopSearching();
            }
            catch (Exception e)
            {

            }

            foreach (var device in Devices)
            {
                try
                {
                    device.Value.Dispose();
                }
                catch { }
            }
            Devices.Clear();
        }
    }
}
