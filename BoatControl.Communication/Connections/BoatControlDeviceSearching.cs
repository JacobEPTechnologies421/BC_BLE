using System;
using BoatControl.Communication.Connections.Ble.Searching;
using BoatControl.Communication.Connections.Cloud.Searching;
using BoatControl.Communication.Connections.Shared.Searching;
using BoatControl.Communication.Connections.Tcp.Searching;
using BoatControl.Communication.Models;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.Logging;

namespace BoatControl.Communication.Connections
{
    public class BoatControlDeviceSearching
    {
        private readonly IBroadcastSearch _broadcastSearch;
        private readonly AuthenticationUser _user;
        private readonly CloudDeviceSearch _cloudDeviceSearch;
        private BLESearch _bleSearch;

        public BoatControlDeviceSearching(
            ILogger<BoatControlDeviceSearching> logger,
            IBroadcastSearch broadcastSearch,
            AuthenticationUser user
        )
        {
            _broadcastSearch = broadcastSearch;
            _user = user;
            _cloudDeviceSearch = new CloudDeviceSearch(logger);
            _bleSearch = new BLESearch(logger);
        }

        public void StartSearching(Action<AFoundDevice> device, Action<bool> cloudConnectedResponse)
        {
            _broadcastSearch.StartSearching(device);
            _cloudDeviceSearch.StartSearching(_user, device, cloudConnectedResponse);
            _bleSearch.StartSearching(_user, device);
        }

        public void StopSearching()
        {
            _broadcastSearch.StopSearching();
            _cloudDeviceSearch.StopSearching();
            _bleSearch.StopSearching();
        }
    }
}
