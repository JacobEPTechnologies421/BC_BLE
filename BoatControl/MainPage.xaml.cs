using System;
using System.Collections.ObjectModel;
using BoatControl.Shared.Messaging;
using BoatControl.Communication;
using DeviceInfo = BoatControl.Communication.Models.DeviceInfo;
using BoatControl.Communication.Helpers;
using BoatControl.Communication.Connections;
using Microsoft.Maui.Controls;
using System.Text.Json;


namespace BoatControl
{
    public class ExtendedDeviceInfo : DeviceInfo
    {
        public bool IsConnected { get; set; }

        public string Details { get; set; }
        public ExtendedDeviceInfo(DeviceInfo deviceInfo, IDeviceConnectionManager deviceConnectionManager)
        {
            this.Number = deviceInfo.Number;
            this.Name = deviceInfo.Name;
            this.Version = deviceInfo.Version;
            this.IsConnected = deviceConnectionManager.IsConnected;

            this.Details = $"{deviceInfo.Number} - {deviceInfo.Name} - {deviceInfo.Version} - {"Connected: " + deviceConnectionManager.IsConnected}";
        }
    }


    public partial class MainPage : ContentPage
    {
        private readonly ObservableCollection<ExtendedDeviceInfo> _devices = new ObservableCollection<ExtendedDeviceInfo>();

        private readonly BoatControlCommunication _communication;

        List<string> nearbyBC = new List<string>();

        bool locationPermissionGranted = false;
        bool bluetoothPermissionGranted = false;
        bool foundBoatControlDevices = false;
        bool authenticatedBoatControlDevice = false;
        bool pairingBoatControlDeviceComplete = false;
        bool registerdBoatControlDevice = false;

        string _bearerToken;
        string _id;
        string _token;

        public MainPage(BoatControlCommunication communication)
        {
            InitializeComponent();  // Initialize UI components first
            RequestPermission();  // Request permissions after UI initialization
            communication.OnDevicesChanged += Communication_OnDevicesChanged;
            DevicesListView.ItemsSource = _devices;
            hybridWebView.SetInvokeJavaScriptTarget(this);


            communication.Start(new Communication.Models.AuthenticationUser()
            {
                BearerToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6IkVQVF9UZXN0IiwidXNlcklkIjoxMzExLCJlbWFpbCI6IkJvYXRDb250cm9sQGVwdGVjaG5vbG9naWVzLmRlIiwiaXNzIjoiYXV0aCIsImlhdCI6MTczNjQ1NTgyMCwiZXhwIjoxNzUyMDA3ODIwLCJyb2xlcyI6WyJBZG1pbiIsIkRldmVsb3BlciIsIlNvZnR3YXJlRW5naW5lZXJzIl0sImRldmljZXMiOlsiRDExNTAiLCJEMTE1MyIsIkQxMTU1IiwiRDExNTYiLCJEMTE1NyIsIkQxMTU5Il0sImp0aSI6IjUxNGEzMTBjMzE0ODQ1YTFhZjhiMmU5NzgwMDUyZDY3In0.byIihxpUBlnATnFSFMW8Pvdkc-ZyH1ppgT86elBscD81Zxr0Kog79W2E6C7CQgYfos_jaFpIdQ8PbldQhCWe7-rs4y7zOKHhjMycHtMxzOzpps6pKi-Kqr-izS76o_QOVNJGxZvvwzGZUK4pBorOyGwUwlFf67UBqo0s7bREuTwdMG-WqNFt31AZhu_kGXB1mvQmxY9P_xdLhTItD3QsfTT4MxKH7Zey8B45FplIC2uPKJfHmt9ph6-zuc-L1ysXkv4tHJu_V_nLWvoYA_7I0ELOTlc1uDlyRqi9EPSOgflWLp4yRKzADRmQJ8D8DW681lgE-1yg8_U2UU9piR6gpA",
                Id = 1311,
                UserToken = "3756099711ee42dc8d4cfb5145895568"
            });
            this._communication = communication;
            this._communication.OnDevicesChanged += _communication_OnDevicesChanged;
            this._communication.OnDeviceMessage += _communication_OnDeviceMessage;
        }

        private async void RequestPermission()
        {
            try
            {
                // Request Location Permission
                var locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (locationStatus == PermissionStatus.Granted)
                {
                    locationPermissionGranted = true;
                }
                else
                {
                    await DisplayAlert("Permission Denied", "Location permission is required to scan for Bluetooth devices.", "OK");
                }

                // Request Bluetooth Permissions (for Android and Windows)
                if (locationPermissionGranted)
                {
                    // On Android and Windows, Bluetooth permission is required to scan for nearby devices
                    var bluetoothStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
                    if (bluetoothStatus == PermissionStatus.Granted)
                    {
                        bluetoothPermissionGranted = true;
                    }
                    else
                    {
                        await DisplayAlert("Permission Denied", "Bluetooth permission is required to scan for devices.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred while requesting permissions: {ex.Message}", "OK");
            }
        }

        private void OnSendMessageButtonClicked(object sender, EventArgs e)
        {
            hybridWebView.SendRawMessage($"Hello from C#!");
        }

        private async void OnHybridWebViewRawMessageReceived(object sender, HybridWebViewRawMessageReceivedEventArgs e)
        {
            await DisplayAlert("Raw Message Received", e.Message, "OK");
        }

        public void DoSyncWork()
        {
            Console.WriteLine("DoSyncWork");
        }

        public void DoSyncWorkParams(int i, string s)
        {
            Console.WriteLine($"DoSyncWorkParams: {i}, {s}");
        }

        public void SetUserParams(string Legacytoken, string Ownerid, string Bearertoken)
        {
            _bearerToken = Bearertoken;
            _id = Ownerid;
            _token = Legacytoken;
        }

        public string DoSyncWorkReturn()
        {
            Console.WriteLine("DoSyncWorkReturn");
            return "Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!Hello from C#!-Hello from C#!-Hello from C#!-Hello from C#!-Hello from C#!-Hello from C#!-Hello from C#!-Hello from C#!";
        }

        //public async Task<string> GetDeviceInfo()
        //{
        //    Console.WriteLine("DoSyncWorkReturn");
        //    try
        //    {
        //        if (DevicesListView.SelectedItem is DeviceInfo device)
        //        {
        //            if (_communication.Devices.TryGetValue(device, out var connectionManager))
        //            {
        //                var message = DeviceMessage.GetTextMessage("device info");
        //                var result = await connectionManager.SendAsync(message);
        //                var response = new
        //                {
        //                    Message = result.Message,
        //                };
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //    }
        //    return JsonSerializer.Serialize(new { Message = "Unable to retrieve device info." });
        //}

        public async Task<string> GetDeviceInfo(string deviceId)
        {
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {deviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Name.Contains(deviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(deviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage("device info");
                    var result = await connectionManager.SendAsync(message);

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    // Return the serialized JSON response
                    //return JsonSerializer.Serialize(response);
                    return result.Message;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }

            // Return a failure message if no matching device or an error occurred
            return JsonSerializer.Serialize(new { Message = "Unable to retrieve device info for ID: " + deviceId });
        }


        public SyncReturn DoSyncWorkParamsReturn(int i, string s)
        {
            Console.WriteLine($"DoSyncWorkParamReturn: {i}, {s}");
            return new SyncReturn
            {
                Message = "Hello from C#!" + s,
                Value = i
            };
        }

        public class SyncReturn
        {
            public string? Message { get; set; }
            public int Value { get; set; }
        }

        private void _communication_OnDeviceMessage(Communication.Connections.IDeviceConnectionManager connectionManager, DeviceMessage message)
        {
            // This is for broadcasting messages etc., like n2k :)
            Console.WriteLine($"{message.Message}");
            DisplayAlert("Info", $"{message.Message}", "Cancel");
        }

        public string SetInformation(string id, string bearertoken, string token)
        {
            Console.WriteLine("DoSyncWorkReturn");
            return "Hello from C#!";
        }

        private void _communication_OnDevicesChanged()
        {

        }
        
        private void Communication_OnDevicesChanged()
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                _devices.Clear();
                nearbyBC.Clear();

                foreach (var device in _communication.Devices)
                {
                    _devices.Add(new ExtendedDeviceInfo(device.Key, device.Value));
                    nearbyBC.Add(device.ToString());
                    nearbyBC.Add($"{device.Key}: {device.Value}"); // Format as needed
                }
            }).Wait();
        }

        public async void OnDeviceSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is DeviceInfo device)
            {
                if(_communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    if (device.IsUnregistered())
                    {
                        await connectionManager.Pair();
                    }
                    else
                    {
                        await connectionManager.ConnectBle();
                    }

                }
            }
        }

        public async void OnSeeRelaysClick(object sender, EventArgs e)
        {
            try
            {
                if (DevicesListView.SelectedItem is DeviceInfo device)
                {
                    if (_communication.Devices.TryGetValue(device, out var connectionManager))
                    {
                        var message = DeviceMessage.GetTextMessage("relay set 5 1");
                        var result = await connectionManager.SendAsync(message);

                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async void OnSeeDeviceInfo(object sender, EventArgs e)
        {
            try
            {
                if (DevicesListView.SelectedItem is DeviceInfo device)
                {
                    if (_communication.Devices.TryGetValue(device, out var connectionManager))
                    {
                        var message = DeviceMessage.GetTextMessage("device info");
                        var result = await connectionManager.SendAsync(message);

                        var response = new
                        {
                            Message = result.Message,
                        };
                        await DisplayAlert("Wifi List", $"{response}", "Ok");

                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async void OnSeeWifiSearch(object sender, EventArgs e)
        {
            try
            {
                if (DevicesListView.SelectedItem is DeviceInfo device)
                {
                    if (_communication.Devices.TryGetValue(device, out var connectionManager))
                    {
                        var message = DeviceMessage.GetTextMessage("wifi list");
                        var result = await connectionManager.SendAsync(message);

                        await DisplayAlert("Wifi List", $"{result.Message}", "Ok");

                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}








