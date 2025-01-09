using System;
using System.Threading.Tasks;
using BoatControl.Communication.Connections.Ble.Searching;
using BoatControl.Communication.Connections.Shared.Searching;
using BoatControl.Communication.Models;
using BoatControl.Shared.Messaging;

namespace BoatControl.Communication.Connections
{
    public interface IDeviceConnectionManager : IDisposable
    {
        #region Properties
        DeviceInfo DeviceInfo { get; }
        string HostOnLocalNetwork { get; }
        bool IsConnected { get; }
        bool IsConnectedCloud { get; }
        bool IsConnectedLocal { get; }
        bool? IsPaired { get; }
        DateTime? LastSeenInCloud { get; }

        bool IsDiscoveredBle { get; }

        bool IsConnectedBle { get; }
        #endregion

        event ConnectionChangeDelegate OnConnectionChange;

        event DeviceMessageDelegate OnDeviceMessage;

        /// <summary>
        /// Pair with device
        /// </summary>
        /// <returns></returns>
        Task<bool> Pair();

        Task<DeviceMessage> SendAsync(DeviceMessage message, IProgress<DeviceMessageProgressWithDirection> progress = null);
        void SetFoundInCloud(FoundCloudDevice device);
        void SetFoundLocally(FoundLocalDevice device);

        void SetFoundBle(FoundBleDevice device);
        Task ConnectBle();
    }
}