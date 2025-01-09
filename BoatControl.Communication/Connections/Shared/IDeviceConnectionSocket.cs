using System;
using System.Threading;
using System.Threading.Tasks;
using BoatControl.Communication.Models;
using BoatControl.Shared.Messaging;
using BoatControl.Shared.UserManagement;

namespace BoatControl.Communication.Connections.Shared
{
    internal interface IDeviceConnectionSocket : IDisposable
    {
        Task<bool> ConnectAsync(AuthenticationUser user, CancellationToken cancellationToken);

        Task<DeviceMessage> ReadMessageAsync(CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress);

        Task SendMessageAsync(DeviceMessage message, CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress);
    }

    internal interface IDirectDeviceConnectionSocket : IDeviceConnectionSocket
    {
        Task<BoatControlChallenge> Peek(CancellationToken cancellationToken);

    }
}