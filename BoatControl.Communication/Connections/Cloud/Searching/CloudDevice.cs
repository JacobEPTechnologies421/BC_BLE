using System;
using Newtonsoft.Json;

namespace BoatControl.Communication.Connections.Cloud.Searching
{
    // {"item":[{"number":"D1006","secret":"827911c1f2594c68a88c1ef3e7ea1ced","version":"1.0.12","lastSeen":"2018-02-13T20:25:13.16","online":true},{"number":"D1007","secret":"7d8ab3dcd0f94560b120caf340a2d7b9","version":"1.0.12","lastSeen":"2018-02-13T20:26:46.693","online":true}],"success":true,"message":""}
    internal class CloudDevice
    {
        [JsonProperty("number")]
        public string Number { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("lastSeen")]
        public DateTime LastSeen { get; set; }

        [JsonProperty("online")]
        public bool Online { get; set; }


    }
}