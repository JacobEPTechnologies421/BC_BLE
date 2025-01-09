using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BoatControl.Communication.Connections.Shared.Searching;
using BoatControl.Communication.Models;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace BoatControl.Communication.Connections.Cloud.Searching
{

    internal class CloudDeviceSearch
    {
        private readonly ILogger _logger;
        private static readonly TimeSpan _lookEvery = TimeSpan.FromSeconds(20);
        private DateTime _timeSinceLastConnectionTry = DateTime.MinValue; // Throttle to ensure fast connection
        private readonly HttpClient _client;

        private Action<FoundCloudDevice> _foundDevice;
        private bool _searching;
        private Action<bool> _cloudConnectedResponse;
        private AuthenticationUser _user;

        public CloudDeviceSearch(ILogger logger)
        {
            _client = new HttpClient();
            _client.BaseAddress = Configuration.Instance.GetCloudUri();
            _client.Timeout = TimeSpan.FromSeconds(5);
            this._logger = logger;
        }

        public void StartSearching(AuthenticationUser user, Action<FoundCloudDevice> foundDevice, Action<bool> cloudConnectedResponse)
        {
            _user = user;
            _foundDevice = foundDevice;
            _cloudConnectedResponse = cloudConnectedResponse;
            _searching = true;
            new Thread(Loop).Start();
        }

        public void StopSearching()
        {
            _searching = false;
        }

        private void Loop()
        {
            while (_searching)
            {
                var sleepMs = (_timeSinceLastConnectionTry - DateTime.Now + _lookEvery);
                if(sleepMs.TotalSeconds > 0)
                    Thread.Sleep(sleepMs);

                _timeSinceLastConnectionTry = DateTime.Now;
                try
                {
                    var result = HttpRequest().Result;
                    if(result?.Devices?.Any() ?? false)
                    {
                        foreach (var device in result.Devices)
                        {
                            var foundDevice = new FoundCloudDevice()
                            {
                                Number = device.Number,
                                Name = device.Name,
                                LastSeenInCloud = device.LastSeen,
                                OnlineInCloud = device.Online,
                                Version = device.Version
                            };
                            _logger.LogDebug($"Found device on cloud: {device.Number}");
                            _foundDevice(foundDevice);
                            _cloudConnectedResponse(true);

                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Error trying to connect to cloud");
                }
            }
        }

        private async Task<GetDevicesResponse> HttpRequest()
        {
            var data = new Dictionary<string, string>() {
                {"userId", _user.Id.ToString()},
                {"loginToken", _user.UserToken},
            };
            var response = await _client.PostAsync("/api/app/getdevices", new FormUrlEncodedContent(data));
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<GetDevicesResponse>(json);
            //Json
            //var result = await Task.Run(() => JsonConvert.DeserializeObject<ApiResponse<IEnumerable<Device>>>(json));
            //return result;

        }

        
    }
}
