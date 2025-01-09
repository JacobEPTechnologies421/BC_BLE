using System;
using System.Threading.Tasks;
using BoatControl.Communication.Models;
using BoatControl.Shared.Messaging;

namespace BoatControl.Communication.Connections.Shared
{
    public delegate void SocketConnectionChangeDelegate(bool connected);
    public delegate void OnBroadcastMessageDelegate(DeviceMessage message);
    interface IDeviceConnection : IDisposable
    {
        event SocketConnectionChangeDelegate OnConnectionChange;
        event OnBroadcastMessageDelegate OnBroadcastMessage;

        bool IsConnected { get; }

        void Start();

        Task<DeviceMessage> SendMessageAsync(DeviceMessage message, IProgress<DeviceMessageProgressWithDirection> progress);

    }
}