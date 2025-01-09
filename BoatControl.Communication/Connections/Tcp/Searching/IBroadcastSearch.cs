using System;
using BoatControl.Communication.Connections.Shared.Searching;

namespace BoatControl.Communication.Connections.Tcp.Searching
{
    public interface IBroadcastSearch
    {

        void StartSearching(Action<FoundLocalDevice> foundDevice);
        void StopSearching();
    }
}