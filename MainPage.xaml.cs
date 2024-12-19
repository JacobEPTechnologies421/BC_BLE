using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Microsoft.Maui.ApplicationModel;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Extensions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.Maui.Controls;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BoatControl.Communication;
using BoatControl.Shared.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BoatControl
{
    public class MyDeviceMessageInterpritator : DeviceMessageInterpritator
    {
        public MyDeviceMessageInterpritator() : base(NullLogger<DeviceMessageInterpritator>.Instance)
        {
            
        }

        // Make GetMessageString public
        public byte[] GetMessageStringAsBytes(DeviceMessage message)
        {
            var data = base.GetMessageString(message);
            return Encoding.UTF8.GetBytes(data);
        }
    }


    public interface IWifiScanner
    {
        Task<List<WifiNetwork>> ScanAsync();
    }
    public partial class MainPage : ContentPage
    {
        private readonly IAdapter _adapter;
        private readonly ObservableCollection<IDevice> _devices;

        private MyDeviceMessageInterpritator _deviceMessageInterpritator = new MyDeviceMessageInterpritator();


        bool locationPermissionGranted = false;
        bool bluetoothPermissionGranted = false;
        bool foundBoatControlDevices = false;
        bool authenticatedBoatControlDevice = false;
        bool pairingBoatControlDeviceComplete = false;
        bool registerdBoatControlDevice = false;


        string bcName;
        string bcNo;

        // Define your UUIDs
        private static readonly string BLE_SERVICE_UUID = "02FB504B-B9E9-4DFE-90F3-CF60AB55A8E0";
        private static readonly string BLE_CHARACTERISTIC_UUID_TX = "CFA58E3B-AB9F-48B1-B27F-00088952CA86";
        private static readonly string BLE_CHARACTERISTIC_UUID_RX = "0EAF8BF5-2D2E-4602-A72A-0D2B051874A5";
        private static readonly string BLE_CHARACTERISTIC_UUID_RESET = "119367EB-67AB-4192-93E5-D3ECA3D0FEE7";

        public MainPage()
        {
            InitializeComponent();  // Initialize UI components first
            //hybridWebView.SetInvokeJavaScriptTarget(this);
            _adapter = CrossBluetoothLE.Current.Adapter;
            _devices = new ObservableCollection<IDevice>();
            DevicesListView.ItemsSource = _devices;
            //LoadWifiNetworks();
            RequestPermission();  // Request permissions after UI initialization
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

        private async void LoadWifiNetworks(object sender, EventArgs e)
        {
            var scanner = DependencyService.Get<IWifiScanner>();
            if (scanner != null)
            {
                var networks = await scanner.ScanAsync();
                // Display the list of networks in your UI
                foreach (var network in networks)
                {
                    Console.WriteLine($"SSID: {network.SSID}, Signal: {network.SignalStrength}");
                }
            }
            else
            {
                Console.WriteLine("Wi-Fi scanning not supported on this platform.");
            }
        }

        private async void OnStartScanningClicked(object sender, EventArgs e)
        {
            _devices.Clear(); // Clear old devices
            await ScanForDevicesAsync();
        }

        // Scan for nearby Bluetooth devices
        private async Task ScanForDevicesAsync()
        {
            // Ensure Bluetooth is enabled
            var state = CrossBluetoothLE.Current.State;
            if (state != BluetoothState.On)
            {
                await DisplayAlert("Error", "Bluetooth is not enabled", "OK");
                return;  // Stop scanning if Bluetooth is off
            }

            // Start scanning
            _adapter.DeviceDiscovered += (s, args) =>
            {
                // Check if the device name starts with "BC-" and add it to the list
                if (!string.IsNullOrEmpty(args.Device.Name) && args.Device.Name.StartsWith("BC-"))
                {
                    if (!_devices.Contains(args.Device))
                    {
                        _devices.Add(args.Device);
                        foundBoatControlDevices = true;
                    }
                }
            };

            try
            {
                await _adapter.StartScanningForDevicesAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Scanning failed: {ex.Message}", "OK");
            }
        }
        
        private async void OnDeviceSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is IDevice device)
            {
                try
                {
                    // Attempt to retrieve advertisement records
                    var advertisementData = GetAdvertisementData(device);
                    bcName= advertisementData.Name;
                    bcNo = advertisementData.DeviceNo;


                    // Extract Manufacturer Data and process it
                    string manufacturerData = advertisementData.ManufacturerData ?? "N/A";
                    int lastFourDecimalValue = -1; // Default value if conversion fails

                    if (manufacturerData != "N/A" && manufacturerData.Length >= 11)
                    {
                        // Get the last four characters (2 bytes in hexadecimal)
                        string lastFourHex = manufacturerData.Substring(manufacturerData.Length - 8, 8).Replace("-", "");
                        

                        // Convert from hexadecimal to decimal
                        if (int.TryParse(lastFourHex, System.Globalization.NumberStyles.HexNumber, null, out lastFourDecimalValue))
                        {
                            // Check if the decimal value is 0
                            if (lastFourDecimalValue == 0)
                            {
                                await DisplayAlert("Check Result", "The last four characters of Manufacturer Data represent 0 in decimal.", "OK");
                            }
                            else
                            {
                                manufacturerData = lastFourDecimalValue.ToString();
                                authenticatedBoatControlDevice = true;
                                
                                PairBtn.IsVisible = true;
                                await DisplayAlert("Check Result", $"The last four characters of Manufacturer Data represent {lastFourDecimalValue} in decimal.", "OK");
                                
                            }
                        }
                        else
                        {
                            await DisplayAlert("Error", "Failed to convert Manufacturer Data from hexadecimal to decimal.", "OK");
                        }
                    }
                    else
                    {
                        await DisplayAlert("Error", "Manufacturer Data is not available or too short.", "OK");
                    }
                    var number = device.Name.Substring(3);

                    // Prepare a message with advertisement details
                    string message = $"Device: {device.Name ?? "Unnamed"}\n" +
                                     $"Local Name: {advertisementData.LocalName ?? "N/A"}\n" +
                                     $"DeviceNo.: {number ?? "N/A"}\n" +
                                     $"Manufacturer Data: {advertisementData.ManufacturerData ?? "N/A"}\n" +
                                     $"OwnerID: {manufacturerData ?? "N/A"}\n" +
                                     $"Service UUIDs: {advertisementData.ServiceUuids ?? "N/A"}";
                     
                    // Display the advertisement data
                    await DisplayAlert("Device Advertisement Data", message, "OK");
                    
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to retrieve advertisement data: {ex.Message}", "OK");
                }
            }
        }

        // Event handler for the "Pair" button
        private async void PairDevice(object sender, EventArgs e)
        {
            string _id = "1311";
            string _userToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6IkRlbW8iLCJ1c2VySWQiOjExNDgsImVtYWlsIjoiRGVtbyIsImlzcyI6ImF1dGgiLCJpYXQiOjE3MzM4MzI3NTAsImV4cCI6MTc0OTM4NDc1MCwicm9sZXMiOlsiRGVtbyJdLCJkZXZpY2VzIjpbIkQxMTE1IiwiRDExMjUiLCJEMTEyOCIsIkQxMTI5IiwiRDExMzkiLCJEMTE0NiIsIkRlbW8iLCJEZW1vMSJdLCJqdGkiOiI4MGU3MjhjZWY4MWI0Mzk5YWY4NzQ1ZGUzMjI2NWQxZSJ9.YDteLhD_lmstIQwzv_veFW9du-EY6RfGNXQN5A8-p4KC6qk5M0NmJeIQHVgPTHcJ4U1Q3Y6hU3aeEuYTTaEYKnSbewIaCsb0HNCMIA7c4mPzYuXq08SsJLSTtEt0cuCu1axjzTL9qYz8IqFjgNPWdPNaelT4ShG-rZ1Hbb52vTM2T-Iuwl--i0ZtV3WHOG8Yj_T7HRS1uWynb3M4W_3wEEjnM1PvAhjTyjw1itTUoo_V_ulBc4ZpWWPyp-jx1Hp0m8wTV1uKFeCDZXBV4fhNK4Mz73adGzY2uVY_T5DY9cw-29DFtU0EX4eVwjzbZ1-j7u98NlyRepd_Mg-HsvQd6g\r\n ";

            if (DevicesListView.SelectedItem is IDevice selectedDevice)
            {
                

                bool result = await PairDeviceAsync(selectedDevice, _id, _userToken);

                if (result)
                {
                    await DisplayAlert("Success", "Device paired successfully.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to pair device.", "OK");
                }
            }
            else
            {
                await DisplayAlert("Error", "No device selected.", "OK");
            }
        }

        private async Task<bool> PairDeviceAsync(IDevice device, string id, string userToken)
        {
            id = "1311";
            userToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6IkRlbW8iLCJ1c2VySWQiOjExNDgsImVtYWlsIjoiRGVtbyIsImlzcyI6ImF1dGgiLCJpYXQiOjE3MzM4MzI3NTAsImV4cCI6MTc0OTM4NDc1MCwicm9sZXMiOlsiRGVtbyJdLCJkZXZpY2VzIjpbIkQxMTE1IiwiRDExMjUiLCJEMTEyOCIsIkQxMTI5IiwiRDExMzkiLCJEMTE0NiIsIkRlbW8iLCJEZW1vMSJdLCJqdGkiOiI4MGU3MjhjZWY4MWI0Mzk5YWY4NzQ1ZGUzMjI2NWQxZSJ9.YDteLhD_lmstIQwzv_veFW9du-EY6RfGNXQN5A8-p4KC6qk5M0NmJeIQHVgPTHcJ4U1Q3Y6hU3aeEuYTTaEYKnSbewIaCsb0HNCMIA7c4mPzYuXq08SsJLSTtEt0cuCu1axjzTL9qYz8IqFjgNPWdPNaelT4ShG-rZ1Hbb52vTM2T-Iuwl--i0ZtV3WHOG8Yj_T7HRS1uWynb3M4W_3wEEjnM1PvAhjTyjw1itTUoo_V_ulBc4ZpWWPyp-jx1Hp0m8wTV1uKFeCDZXBV4fhNK4Mz73adGzY2uVY_T5DY9cw-29DFtU0EX4eVwjzbZ1-j7u98NlyRepd_Mg-HsvQd6g";

            try
            {
                // Connect to the device
                await _adapter.ConnectToDeviceAsync(device);
                await DisplayAlert("Success", "Connected Successfully", "OK");

                var service = await device.GetServiceAsync(Guid.Parse(BLE_SERVICE_UUID));
                if (service == null) throw new Exception("Service not found on the device.");

                // Get required characteristics
                var txCharacteristic = await service.GetCharacteristicAsync(Guid.Parse(BLE_CHARACTERISTIC_UUID_TX));
                if (txCharacteristic == null) throw new Exception("TX characteristic not found on the device.");

                var rxCharacteristic = await service.GetCharacteristicAsync(Guid.Parse(BLE_CHARACTERISTIC_UUID_RX));
                if (rxCharacteristic == null) throw new Exception("RX characteristic not found on the device.");

                rxCharacteristic.ValueUpdated += (s, e) =>
                {
                    // Handle incoming data from the device
                    var data = e.Characteristic.Value;
                    var message = Encoding.UTF8.GetString(data, 0, data.Length);
                    Console.WriteLine($"Received: {message}");
                };
                await rxCharacteristic.StartUpdatesAsync();

                //// Step 1: Send initial pairing command

                var randomChallenge = Guid.NewGuid().ToString("N");
                var deviceMessage = DeviceMessage.GetTextMessage(randomChallenge, "auth");
 
                var textToSend = _deviceMessageInterpritator.GetMessageStringAsBytes(deviceMessage);

                await txCharacteristic.WriteAsync(textToSend);

                //// Step 2: Receive challenge response
                //var (data, resultCode) = await rxCharacteristic.ReadAsync();
                //if (resultCode != 0) throw new Exception("Failed to read challenge from the device.");

                //var challengeMessage = Encoding.UTF8.GetString(data);
                //if (!challengeMessage.StartsWith("response="))
                //{
                //    throw new Exception($"Unexpected response: {challengeMessage}");
                //}

                //// Step 3: Compute response to challenge
                //string challenge = ParseChallengeFromResponse(challengeMessage);
                //string secret = "your-secret"; // Replace with the actual secret
                //string expectedResponse = ComputeSha256(challenge + secret);


                //// Step 4: Receive final acknowledgment
                //var responseCommand = Encoding.UTF8.GetBytes(expectedResponse + "\n");
                //await txCharacteristic.WriteAsync(responseCommand);

                //// Step 5: Receive final acknowledgment
                //var (ackData, ackResultCode) = await rxCharacteristic.ReadAsync();
                //if (ackResultCode != 0) throw new Exception("Failed to read acknowledgment from the device.");

                //var ackMessage = Encoding.UTF8.GetString(ackData);
                //if (ackMessage.Equals("ok", StringComparison.OrdinalIgnoreCase))
                //{
                //    await DisplayAlert("Success", "Device successfully paired.", "OK");
                //    return true;
                //}

                //await DisplayAlert("Failure", "Pairing failed. Response: " + ackMessage, "OK");
                return false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to pair device: {ex.Message}", "OK");
                return false;
            }
        }

        private string ParseChallengeFromResponse(string response)
        {
            // Extract the challenge string from the response
            var match = Regex.Match(response, @"challenge=(\w+)");
            if (!match.Success) throw new Exception("Challenge not found in response.");
            return match.Groups[1].Value;
        }

        private string ComputeSha256(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private BluetoothDeviceInfo GetAdvertisementData(IDevice device)
        {
            var info = new BluetoothDeviceInfo
            {
                Name = device.Name,

                LocalName = device.AdvertisementRecords?
                             .FirstOrDefault(record => record.Type == AdvertisementRecordType.CompleteLocalName)?
                             .Data != null
                             ? System.Text.Encoding.UTF8.GetString(device.AdvertisementRecords
                                   .First(record => record.Type == AdvertisementRecordType.CompleteLocalName).Data)
                             : null,

                ManufacturerData = device.AdvertisementRecords?
                                   .FirstOrDefault(record => record.Type == AdvertisementRecordType.ManufacturerSpecificData)?
                                   .Data != null
                                   ? BitConverter.ToString(device.AdvertisementRecords
                                       .First(record => record.Type == AdvertisementRecordType.ManufacturerSpecificData).Data)
                                   : null,

                ServiceUuids = device.AdvertisementRecords?
                              .FirstOrDefault(record => record.Type == AdvertisementRecordType.ServiceData)?
                              .Data != null
                              ? BitConverter.ToString(device.AdvertisementRecords
                                  .First(record => record.Type == AdvertisementRecordType.ServiceData).Data)
                              : null
            };
            

            return info;
        }
        
        private async Task UpdateUIAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Your UI update code here
                // For example, refreshing a ListView or Label
                DevicesListView.ItemsSource = _devices;
            });
        }
    }
}

public class BluetoothDeviceInfo
{
    public string Name { get; set; }
    public string LocalName { get; set; }
    public string DeviceNo { get; set; }
    public string ManufacturerData { get; set; }
    public string OwnerID { get; set; }
    public string ServiceUuids { get; set; }
}

public class WifiNetwork
{
    public string SSID { get; set; }
    public int SignalStrength { get; set; }
}

