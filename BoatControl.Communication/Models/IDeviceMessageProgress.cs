using BoatControl.Shared.Messaging;

namespace BoatControl.Communication.Models
{
  
    public class DeviceMessageProgressWithDirection : DeviceMessageProgress
    {
        public EnumSendProgressDirection Direction { get; }

        public decimal Percentage => (decimal)BytesTransferred / (decimal)BytesTotal * 100m;

        public DeviceMessageProgressWithDirection(string messageId, long bytesTransferred, long bytesTotal, EnumSendProgressDirection direction) : base(messageId, bytesTransferred, bytesTotal)
        {
            Direction = direction;
        }

        public DeviceMessageProgressWithDirection(DeviceMessageProgress deviceMessageProgress, EnumSendProgressDirection direction)
        : this(deviceMessageProgress.MessageId, deviceMessageProgress.BytesTransferred, deviceMessageProgress.BytesTotal, direction)
        {
            
        }
    }
}