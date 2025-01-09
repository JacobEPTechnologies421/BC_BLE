using BoatControl.Communication.Connections.Shared.Searching;
using BoatControl.Communication.Connections.Tcp.Searching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatControl
{
    internal class ReplaceMeWithATcpListener : IBroadcastSearch
    {
        public void StartSearching(Action<FoundLocalDevice> foundDevice)
        {
        }

        public void StopSearching()
        {
        }
    }
}
