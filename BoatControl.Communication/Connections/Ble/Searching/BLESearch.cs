using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using BoatControl.Communication.Models;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.Logging;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace BoatControl.Communication.Connections.Ble.Searching
{
    public class BLESearch
    {
        private readonly ILogger _logger;
        private AuthenticationUser _user;
        private Action<FoundBleDevice> _foundDevice;
        private bool _searching;
        private IBluetoothLE _ble;
        private IAdapter _adapter;
        private bool _isScanning;


        public BLESearch(ILogger logger)
        {
            try
            {
                _ble = CrossBluetoothLE.Current;
                _adapter = CrossBluetoothLE.Current.Adapter;
            }
            catch
            {
                /*Not implemented when running locally */
            }

            _logger = logger;
        }

        public void StartSearching(AuthenticationUser user, Action<FoundBleDevice> foundDevice)
        {
            if (_ble == null) return;
            _user = user;
            _foundDevice = foundDevice;
            _searching = true;
            _adapter.ScanMode = ScanMode.LowLatency;
            _adapter.DeviceDiscovered += DeviceDiscovered;
            _adapter.ScanTimeoutElapsed += ScanTimeoutElapsed;
            new Thread(Loop).Start();
        }

        private void ScanTimeoutElapsed(object sender, EventArgs e)
        {
            _logger.LogDebug("ScanTimeoutElapsed");
            _isScanning = false;
        }

        private async void DeviceDiscovered(object sender, Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs a)
        {
            try
            {
                if (a?.Device?.Name?.Contains("BC-") ?? false)
                {
                    var test = "wicked";
                }

                var data = a.Device?.AdvertisementRecords.FirstOrDefault(b => b.Type == AdvertisementRecordType.ManufacturerSpecificData);
                if (data == null) return;



                // Revision 1
                if (data.Data != null && data.Data[0] == 1)
                {
                    if (a.Device == null || a.Device.Name == null || !a.Device.Name.StartsWith("BC-", StringComparison.CurrentCulture) || data.Data.Length != 5)
                        return;

                    // Reverse to get correct number
                    var owner = BitConverter.ToInt32(data.Data.Skip(1).Reverse().ToArray(), 0);
                    var number = a.Device.Name.Substring(3);


                    if (owner > 0 && owner != _user.Id)
                    {
                        _logger.LogDebug("Not owned by user");
                        return;
                    }

                    var foundDevice = new FoundBleDevice(
                        a.Device,
                        number,
                        "",
                        "",
                        owner > 0
                    );
                    _foundDevice?.Invoke(foundDevice);

                    _logger.LogDebug("Device discovered!");
                }
                else
                {
                    DeviceDiscoveredOld(a, data.Data);
                }
            }
            catch 
            {

            }
        }

        private async void DeviceDiscoveredOld(Plugin.BLE.Abstractions.EventArgs.DeviceEventArgs a, byte[] data)
        {
            var manufacturerData = Encoding.UTF8.GetString(data ?? new byte[] { });

            var unique = new HashSet<string>();
            var properties = manufacturerData.Split('&')
                .Select(b => b.Split('='))
                .Where(c => unique.Add(c[0]))
                .ToDictionary(c => c[0], c => c.Length > 1 ? c[1] : "");


            string number;
            if (!properties.TryGetValue("n", out number) || !Regex.IsMatch(number, "(D[0-9]+|Unregistered)"))
                return;


            if (!properties.TryGetValue("o", out var ownerStr) || !Int32.TryParse(ownerStr, out var owner))
            {
                _logger.LogWarning("Owner property not found");
                return;
            }

            if (owner > 0 && owner != _user.Id)
            {
                _logger.LogDebug("Not owned by user");
                return;
            }

            var foundDevice = new FoundBleDevice(
                a.Device,
                number,
                properties.TryGetValue("nm", out var name) ? name : "",
                properties.TryGetValue("v", out var version) ? version : "",
                owner > 0
            );
            _foundDevice?.Invoke(foundDevice);

            _logger.LogDebug("Device discovered!");
        }


        public void StopSearching()
        {
            _searching = false;
        }

        private void Loop()
        {
            while (_searching)
            {


                //CrossPermissions.Current.RequestPermissionsAsync(Plugin.Permissions.Abstractions.Permission.);
                //adapter.ScanMode = ScanMode.;

                if (!_isScanning)
                {
                    _logger.LogDebug("Start scanning");
                    _isScanning = true;
                    _adapter.StartScanningForDevicesAsync().Wait();
                }

                Thread.Sleep(5000);
            }
        }
    }
}
