using BoatControl.Communication.Connections.Shared.Searching;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace BoatControl.Communication.Connections.Ble.Searching
{
    public class FoundBleDevice : AFoundDevice
    {
        public IDevice Device { get; }
        public bool IsPaired { get; set; }
        public bool Connected
        {
            get { return Device.State == DeviceState.Connected; }
        }

        public FoundBleDevice(IDevice device, string deviceNumber, string name, string version, bool isPaired)
        {
            Device = device;
            Number = deviceNumber;
            Name = name;
            Version = version;
            IsPaired = isPaired;
        }

    }
}