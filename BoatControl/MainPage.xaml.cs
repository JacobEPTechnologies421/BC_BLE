using System;
using System.Collections.ObjectModel;
using BoatControl.Shared.Messaging;
using BoatControl.Communication;
using DeviceInfo = BoatControl.Communication.Models.DeviceInfo;
using BoatControl.Communication.Helpers;
using BoatControl.Communication.Connections;

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

        public MainPage(BoatControlCommunication communication)
        {
            InitializeComponent();  // Initialize UI components first

            communication.OnDevicesChanged += Communication_OnDevicesChanged;
            DevicesListView.ItemsSource = _devices;


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

        private void _communication_OnDeviceMessage(Communication.Connections.IDeviceConnectionManager connectionManager, DeviceMessage message)
        {
            // This is for broadcasting messages etc., like n2k :)
            Console.WriteLine($"{message.Message}");

        }

        private void _communication_OnDevicesChanged()
        {

        }
        

        private void Communication_OnDevicesChanged()
        {
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                _devices.Clear();
                foreach (var device in _communication.Devices)
                {
                    _devices.Add(new ExtendedDeviceInfo(device.Key, device.Value));
                }
            }).Wait();
        }


        private async void OnDeviceSelected(object sender, SelectedItemChangedEventArgs e)
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


        private async void OnSeeRelaysClick(object sender, EventArgs e)
        {
            try
            {
                if (DevicesListView.SelectedItem is DeviceInfo device)
                {
                    if (_communication.Devices.TryGetValue(device, out var connectionManager))
                    {
                        var message = DeviceMessage.GetTextMessage("wifi list");
                        var result = await connectionManager.SendAsync(message);

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


