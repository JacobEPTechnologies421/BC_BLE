namespace BoatControl.Shared.Messaging
{
    public class DeviceMessageProgress
    {
        public string MessageId { get;  }

        public long BytesTransferred { get; }

        public long BytesTotal { get; }

        public DeviceMessageProgress(string messageId, long bytesTransferred, long bytesTotal)
        {
            MessageId = messageId;
            BytesTransferred = bytesTransferred;
            BytesTotal = bytesTotal;
        }
    }
}