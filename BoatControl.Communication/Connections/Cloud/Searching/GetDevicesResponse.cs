using System.Collections.Generic;
using Newtonsoft.Json;

namespace BoatControl.Communication.Connections.Cloud.Searching
{
    internal class GetDevicesResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("item")]
        public List<CloudDevice> Devices { get; set; }
    }
}