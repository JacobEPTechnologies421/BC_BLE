using System;
using System.Collections.ObjectModel;
using BoatControl.Shared.Messaging;
using BoatControl.Communication;
using BoatControl.Communication.Models;
using DeviceInfo = BoatControl.Communication.Models.DeviceInfo;
using BoatControl.Communication.Helpers;
using BoatControl.Communication.Connections;
using AuthenticationUser=BoatControl.Communication.Models.AuthenticationUser;
using Microsoft.Maui.Controls;
using System.Text.Json;
using BoatControl.Communication.Storage;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using Microsoft.Maui.Devices.Sensors;


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

    public static class StringExtensions
    {
        public static string Sha256(this string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);

            using (SHA256 sha256 = SHA256.Create()) // Updated to use SHA256.Create()
            {
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hash).ToLower(); // More efficient way to convert to hex
            }
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

        double latitude = 0;
        double longitude = 0;

        //string _bearerToken="eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6IkVQVF9UZXN0IiwidXNlcklkIjoxMzExLCJlbWFpbCI6IkJvYXRDb250cm9sQGVwdGVjaG5vbG9naWVzLmRlIiwiaXNzIjoiYXV0aCIsImlhdCI6MTczNjQ1NTgyMCwiZXhwIjoxNzUyMDA3ODIwLCJyb2xlcyI6WyJBZG1pbiIsIkRldmVsb3BlciIsIlNvZnR3YXJlRW5naW5lZXJzIl0sImRldmljZXMiOlsiRDExNTAiLCJEMTE1MyIsIkQxMTU1IiwiRDExNTYiLCJEMTE1NyIsIkQxMTU5Il0sImp0aSI6IjUxNGEzMTBjMzE0ODQ1YTFhZjhiMmU5NzgwMDUyZDY3In0.byIihxpUBlnATnFSFMW8Pvdkc-ZyH1ppgT86elBscD81Zxr0Kog79W2E6C7CQgYfos_jaFpIdQ8PbldQhCWe7-rs4y7zOKHhjMycHtMxzOzpps6pKi-Kqr-izS76o_QOVNJGxZvvwzGZUK4pBorOyGwUwlFf67UBqo0s7bREuTwdMG-WqNFt31AZhu_kGXB1mvQmxY9P_xdLhTItD3QsfTT4MxKH7Zey8B45FplIC2uPKJfHmt9ph6-zuc-L1ysXkv4tHJu_V_nLWvoYA_7I0ELOTlc1uDlyRqi9EPSOgflWLp4yRKzADRmQJ8D8DW681lgE-1yg8_U2UU9piR6gpA";
        string _bearerToken = "";
        int _id=0;
        string _token= "";

        public MainPage(BoatControlCommunication communication)
        {
            InitializeComponent();  // Initialize UI components first
            RequestPermission();  // Request permissions after UI initialization
            _communication = communication;

            // Bind UI components
            //DevicesListView.ItemsSource = _devices;
            hybridWebView.SetInvokeJavaScriptTarget(this);

            // Subscribe to device change events
            _communication.OnDevicesChanged += Communication_OnDevicesChanged;
            _communication.OnDevicesChanged += _communication_OnDevicesChanged;
            _communication.OnDeviceMessage += _communication_OnDeviceMessage;
        }

        private async void RequestPermission()
        {
            try
            {
                // Request Location Permission
                var location = await Geolocation.GetLastKnownLocationAsync();

                if (location == null)
                {
                    // If no cached location is available, get a new one
                    location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
                }

                if (location != null)
                {
                    latitude = location.Latitude;
                    longitude = location.Longitude;
                }
                else
                {
                    await DisplayAlert("Location Error", "Unable to get location.", "OK");
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

        public string DoSyncWorkReturn()
        {
            Console.WriteLine("DoSyncWorkReturn");
            return $"{_id}+{_token}";
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


        //BC Fx
        public string SetUserParams(string bearerToken, int ownerId, string legacyToken)
        {
            // Set user parameters
            _bearerToken = bearerToken;
            _id = ownerId;
            _token = legacyToken;

            // Start communication once parameters are set
            StartCommunication();
            //_communication.Start(new Communication.Models.AuthenticationUser()
            //{
                //BearerToken = _bearerToken,
                //Id = _id,
                //UserToken = _token
            //});
            return _token;
        }

        public string GetDeviceList()
        {
            var DeviceList = JsonSerializer.Serialize(_communication.Devices.Values);
            return DeviceList;
        }


        public void PairDevice(string deviceId)
        {
            var newDeviceId = deviceId;
            var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
            var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));
            if (_communication.Devices.TryGetValue(device, out var connectionManager))
            {
                if (device.IsUnregistered()){
                    connectionManager.Pair();
                    SetOwner(deviceId);
                }
                else {
                    connectionManager.ConnectBle();
                    SetOwner(deviceId);
                }
            }
        }

        private void StartCommunication()
        {
            if (!string.IsNullOrWhiteSpace(_bearerToken) && _id > 0 && !string.IsNullOrWhiteSpace(_token))
            {
                // Initialize communication with the provided authentication parameters
                _communication.Start(new Communication.Models.AuthenticationUser()
                {
                    BearerToken = _bearerToken,
                    Id = _id,
                    UserToken = _token
                });

            }
            else
            {
                // Log or handle missing parameters as needed
                throw new InvalidOperationException("User parameters are not set properly.");
            }
        }

        public string GetLocation(string message)
        {
            var response = $"{latitude}+{longitude}";
            var DeviceInforesult = JsonSerializer.Serialize(response);
            return DeviceInforesult;
        }
        
        public string UpdateFirmware(string deviceId, string downloadVersion)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));



                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var auth2 = $"{newDeviceId}{downloadVersion}".Sha256();
                    using var client = new HttpClient()
                    {
                        BaseAddress = new Uri("https://boatcontrol.net"),
                        Timeout = TimeSpan.FromSeconds(15)
                    };
                    
                    var firmwareResponse = client.GetAsync($"/Software/Download?number={device.Number}&auth2={auth2}").Result;
                    if (firmwareResponse.StatusCode != HttpStatusCode.OK)
                        return $"Forventede statuskode 200, ikke {firmwareResponse.StatusCode}";

                    var filename = firmwareResponse.Content.Headers.ContentDisposition.FileName;
                    var version = Regex.Match(filename, "^[^-]*-(.*)\\.[^.]+$").Groups[1].Value;
                    var content = firmwareResponse.Content.ReadAsByteArrayAsync().Result;
                      
                    var message = DeviceMessage.GetFileWithMd5Message("/firmware.bin", content);

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message == "" ? "Success" : result.Message,
                    };

                    //Return the serialized JSON response
                    var DeviceResponse = JsonSerializer.Serialize(response);

                    return DeviceResponse;

                }
                return "No box found";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
                return ex.Message;
            }
        }

        public string GetDeviceInfo(string deviceId)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage("device info");

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    //Return the serialized JSON response
                    var DeviceInforesult = JsonSerializer.Serialize(response);
                    return DeviceInforesult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }

            // Return a failure message if no matching device or an error occurred
            return JsonSerializer.Serialize(new { Message = "Unable to retrieve device info for ID: " + deviceId });
        }

        public string GetWifiList(string deviceId)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage("wifi list");

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    //Return the serialized JSON response
                    var DeviceInforesult = JsonSerializer.Serialize(response);
                    return DeviceInforesult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }

            // Return a failure message if no matching device or an error occurred
            return JsonSerializer.Serialize(new { Message = "Unable to retrieve device info for ID: " + deviceId });
        }

        public string GetRelay(string deviceId)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage("relay list");

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    //Return the serialized JSON response
                    var DeviceInforesult = JsonSerializer.Serialize(response);
                    return DeviceInforesult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }

            // Return a failure message if no matching device or an error occurred
            return JsonSerializer.Serialize(new { Message = "Unable to retrieve device info for ID: " + deviceId });
        }

        public void SetWifi(string deviceId, string ssid, string password)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            password = "EPTechJyllandsgade6400##";
            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage($"wifi join {ssid} {password}");

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    //Return the serialized JSON response
                    var DeviceInforesult = JsonSerializer.Serialize(response);
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }

            // Return a failure message if no matching device or an error occurred
        }

        public string SetRelay(string deviceId, string index, string relaystatus)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage($"relay set {index} {relaystatus}");

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    //Return the serialized JSON response
                    var DeviceInforesult = JsonSerializer.Serialize(response);
                    return DeviceInforesult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }
            return "Failed Set Relay";
            // Return a failure message if no matching device or an error occurred
        }

        public void SetOwner(string deviceId)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage($"device setowner {_id} {_token}");

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    //Return the serialized JSON response
                    var DeviceInforesult = JsonSerializer.Serialize(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }

            // Return a failure message if no matching device or an error occurred
        }

        public void SetReset(string deviceId)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage($"device reset");

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    //Return the serialized JSON response
                    var DeviceInforesult = JsonSerializer.Serialize(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }

            // Return a failure message if no matching device or an error occurred
        }

        public void SetRestart(string deviceId)
        {
            var newDeviceId = deviceId;
            Console.WriteLine($"GetDeviceInfo Invoked with Device ID: {newDeviceId}");

            try
            {
                // Match the device ID in the _communication.Devices dictionary
                var device = _communication.Devices.Keys.FirstOrDefault(d => d.Number.Contains(newDeviceId));
                var matchingDevice = nearbyBC.FirstOrDefault(d => d.Contains(newDeviceId));

                if (device != null && _communication.Devices.TryGetValue(device, out var connectionManager))
                {
                    var message = DeviceMessage.GetTextMessage($"device restart");

                    var result = connectionManager.SendAsync(message).Result;

                    var response = new
                    {
                        Message = result.Message,
                    };

                    Console.WriteLine($"Device Info Retrieved: {result.Message}");

                    //Return the serialized JSON response
                    var DeviceInforesult = JsonSerializer.Serialize(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving device info: {ex.Message}");
            }

            // Return a failure message if no matching device or an error occurred
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



    }
}



