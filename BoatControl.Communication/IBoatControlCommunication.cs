using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using BoatControl.Communication.Connections;
using BoatControl.Communication.Models;
using BoatControl.Shared.Messaging;
using BoatControl.Shared.UserManagement;

namespace BoatControl.Communication
{
    public delegate void DeviceMessageDelegate(IDeviceConnectionManager connectionManager, DeviceMessage message);
    public delegate void ConnectionChangeDelegate(IDeviceConnectionManager deviceConnectionManager);
    public delegate void IsCloudConnectedChangeDelegate(bool isCloudConnected);
    public delegate void DevicesChanged();


    public interface IBoatControlCommunication : IDisposable
    {
        IDictionary<DeviceInfo, IDeviceConnectionManager> Devices { get; }
        bool? IsCloudConnected { get; }

        AuthenticationUser Owner { get;  }

        event IsCloudConnectedChangeDelegate IsCloudConnectedChangeEvent;
        event ConnectionChangeDelegate OnConnectionChange;
        event DeviceMessageDelegate OnDeviceMessage;
        event DevicesChanged OnDevicesChanged;

        void Start(AuthenticationUser owner);
    }
}