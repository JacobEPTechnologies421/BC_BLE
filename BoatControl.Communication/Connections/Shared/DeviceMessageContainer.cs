using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BoatControl.Shared.Messaging;

namespace BoatControl.Communication.Connections.Shared
{
    internal class DeviceMessageContainer
    {
        public DateTime Created { get; } = DateTime.Now;
        public DateTime LastUpdated { get; internal set; } = DateTime.Now;
        public DeviceMessage Message { get; }
        public TaskCompletionSource<DeviceMessage> Promise { get; }
        public IProgress<DeviceMessageProgress> Progress { get; }


        public DeviceMessageContainer(DeviceMessage message, TaskCompletionSource<DeviceMessage> promise, IProgress<DeviceMessageProgress> progress)
        {
            Message = message;
            Promise = promise;
            Progress = new Progress<DeviceMessageProgress>(p =>
            {
                LastUpdated = DateTime.Now;
                progress?.Report(p);
            });
        }
    }
}
