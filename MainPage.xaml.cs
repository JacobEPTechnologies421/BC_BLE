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

        private static readonly string BC_SECRET = "c1278456931740f0a78775565d6c881fdd5c7d75130b4d2d92901d2e3fd145efabc755f4dd3d4a95b1bf200af8ea93cb";

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
                    bcName = advertisementData.Name;
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
            string _userToken = "3756099711ee42dc8d4cfb5145895568";

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


                // Step 1: Send initial pairing command
                var randomChallenge = Guid.NewGuid().ToString("N");
                await DisplayAlert("Info", $"Succeeded sending challenge: {randomChallenge}", "OK");
                var deviceMessage = DeviceMessage.GetTextMessage(randomChallenge, "auth");

                var textToSend = _deviceMessageInterpritator.GetMessageStringAsBytes(deviceMessage);
                await txCharacteristic.WriteAsync(textToSend);

                var response = await ReceiveBleMessageAsync(rxCharacteristic);
                if (response.StartsWith("text"))
                {
                    Console.WriteLine("Pairing succeeded with response: " + response);
                    await DisplayAlert("Info", $"Succeeded with 1st response: {response}", "OK");
                    
                }
                var contraChallenge = ExtractChallenge(response); 
                await DisplayAlert("Info", $"Contra Challenge: {contraChallenge}", "OK");

                // Step 2 : SHA256 encrypted challenge response + contra-challenge
                var randomChallenge2 = contraChallenge;
                var expectedResponse = $"{contraChallenge}{userToken}";
                await DisplayAlert("Info", $"Contra Challenge: {contraChallenge}", "OK");
                var deviceMessage2 = DeviceMessage.GetTextMessage(contraChallenge, "auth");

                var textToSend2 = _deviceMessageInterpritator.GetMessageStringAsBytes(deviceMessage2);
                await txCharacteristic.WriteAsync(textToSend2);

                var response2 = await ReceiveBleMessageAsync(rxCharacteristic);
                if (response2.StartsWith("text"))
                {
                    Console.WriteLine("Pairing succeeded with 2nd response: " + response2);
                    await DisplayAlert("Info", $"Succeeded with 2nd response: {response2}", "OK");
                    return true;
                }

                throw new Exception("Unexpected response from device: " + response2);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to pair device: {ex.Message}", "OK");
                return false;
            }
        }

        private string ExtractChallenge(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                throw new ArgumentException("Response cannot be null or empty.", nameof(response));
            }

            // Define a regex to match the "challenge" key and capture its value
            var challengeRegex = new Regex(@"challenge=([a-fA-F0-9]+)", RegexOptions.Compiled);

            // Attempt to match the regex to the response string
            var match = challengeRegex.Match(response);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value; // Return the captured value
            }

            throw new Exception("Challenge not found in the response.");
        }

        private Dictionary<string, string> ParseResponse(string response)
        {
            // Create a dictionary to hold key-value pairs
            var result = new Dictionary<string, string>();

            // Split the response string into key-value pairs using '&' as a delimiter
            var pairs = response.Split('&');
            foreach (var pair in pairs)
            {
                // Split each pair into key and value using '=' as a delimiter
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2) // Ensure there is both a key and a value
                {
                    result[keyValue[0]] = keyValue[1]; // Add the key-value pair to the dictionary
                }
                else if (keyValue.Length == 1) // Handle keys with no value (e.g., "name=")
                {
                    result[keyValue[0]] = string.Empty;
                }
            }

            return result;
        }
        private async Task<string> ReceiveBleMessageAsync(ICharacteristic rxCharacteristic, int timeoutMilliseconds = 5000)
        {
            var tcs = new TaskCompletionSource<string>();

            // Define a timeout for waiting for BLE responses
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeoutMilliseconds);

            // Buffer to accumulate received data
            StringBuilder messageBuilder = new StringBuilder();

            // Event handler for receiving data
            EventHandler<CharacteristicUpdatedEventArgs> handler = (s, e) =>
            {
                try
                {
                    // Get the data received in the BLE notification
                    var data = e.Characteristic.Value;
                    var messagePart = Encoding.UTF8.GetString(data, 0, data.Length);

                    // Append the received chunk to the buffer
                    Console.WriteLine($"Received chunk: {messagePart}");
                    messageBuilder.Append(messagePart);

                    // Check if we have received the entire message
                    // You can use a specific delimiter or check for a known ending pattern
                    if (messagePart.Contains("challenge="))  // Modify this based on your message structure
                    {
                        // Complete message received
                        Console.WriteLine($"Complete message received: {messageBuilder.ToString()}");
                        tcs.TrySetResult(messageBuilder.ToString()); // Set the result when the full message is received
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error decoding message: {ex.Message}");
                    tcs.TrySetException(ex); // In case of an error, set exception
                }
            };

            // Subscribe to the characteristic's ValueUpdated event
            rxCharacteristic.ValueUpdated += handler;

            try
            {
                await rxCharacteristic.StartUpdatesAsync(); // Start listening for notifications

                // Wait for the message or timeout
                using (cts.Token.Register(() => tcs.TrySetCanceled()))  // Cancel after timeout
                {
                    return await tcs.Task; // Await the TaskCompletionSource
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during BLE message reception: {ex.Message}");
                throw;
            }
            finally
            {
                // Clean up: unsubscribe and stop updates
                rxCharacteristic.ValueUpdated -= handler;
                await rxCharacteristic.StopUpdatesAsync();
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

