using System;
using BoatControl.Communication.Models;

namespace BoatControl.Communication.Connections.Shared.Searching
{

    public class FoundLocalDevice : AFoundDevice
    {
        /// <summary>
        /// HostOnLocalNetwork / IP on local network
        /// </summary>
        public string HostOnLocalNetwork { get; set; }
    }

    public class FoundCloudDevice : AFoundDevice
    {
        public bool OnlineInCloud { get; set; }

        public DateTime? LastSeenInCloud { get; set; }

    }
    public abstract class AFoundDevice : DeviceInfo
    {

    }
}
