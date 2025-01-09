using System;
using System.Collections;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace BoatControl.Shared.Messaging
{
    public class DeviceMessage
    {
        public DeviceMessageType MessageType { get; set; }

        public string Id { get; set; }
        public string Message { get; set; }

        public byte[] Payload { get; set; }

        public byte[] PayloadMD5 { get; set; }

        public override string ToString()
        {
            return Message;
        }

        public string GetPayloadMd5String()
        {
            if (PayloadMD5 == null) return "";
            var sb = new StringBuilder();
            foreach (byte bt in PayloadMD5)
            {
                sb.Append(bt.ToString("x2"));
            }

            return sb.ToString();
        }

        public void GeneratePayloadMd5()
        {
            if(MessageType == DeviceMessageType.File)
            {
                using (var md5 = MD5.Create())
                {
                    PayloadMD5 = md5.ComputeHash(Payload, 0, Payload.Length);
                    MessageType = DeviceMessageType.FileWithMd5;
                }
            }
        }

        /// <summary>
        /// Verify md5, throws Md5MismatchException
        /// </summary>
        public void VerifyMd5()
        {
            if (MessageType != DeviceMessageType.FileWithMd5) return;

            byte[] md5Bytes;
            using (var md5 = MD5.Create())
            {
                md5Bytes = md5.ComputeHash(Payload, 0, Payload.Length);
            }
            if (!md5Bytes.SequenceEqual(PayloadMD5))
                throw new Md5MismatchException();
        }

        public void SetPaymoadMd5ByString(string md5)
        {
            Byte[] bytes = new Byte[md5.Length / 2];
            for (Int32 i = 0; i < md5.Length / 2; i++)
            {
                bytes[i] = Convert.ToByte(md5.Substring(2 * i, 2), 16);
            }

            PayloadMD5 = bytes;
        }

        public string GetPayloadAsString() {
            return (Payload?.Length > 0) ? Encoding.UTF8.GetString(Payload) : "";
        }

        #region Static generators
        public static DeviceMessage GetTextMessage(string message, string id = null)
        {
            return new DeviceMessage()
            {
                Id = id ?? DateTime.Now.Ticks.ToString(),
                Message = message,
                MessageType = DeviceMessageType.Text
            };
        }
        public static DeviceMessage GenerateBroadcastMessage(string message)
        {
            return new DeviceMessage()
            {
                Id = DateTime.Now.Ticks.ToString(),
                Message = message,
                MessageType = DeviceMessageType.Broadcast
            };
        }

        [Obsolete("Use GetFileWithMd5Message")]
        public static DeviceMessage GetFileMessage(string filename, byte[] fileBytes, string id = null)
        {
            //Encoding.Default.GetString(
            return new DeviceMessage()
            {
                Id = id ?? DateTime.Now.Ticks.ToString(),
                Message = "/" + filename.TrimStart('/'),
                MessageType = DeviceMessageType.File,
                Payload = fileBytes
            };
        }

        public static DeviceMessage GetFileWithMd5Message(string filename, byte[] fileBytes, string id = null)
        {
            byte[] md5Bytes;
            using (var md5 = MD5.Create())
            {
                md5Bytes = md5.ComputeHash(fileBytes, 0, fileBytes.Length);
            }
            //Encoding.Default.GetString(
            return new DeviceMessage()
            {
                Id = id ?? DateTime.Now.Ticks.ToString(),
                Message = "/" + filename.TrimStart('/'),
                MessageType = DeviceMessageType.FileWithMd5,
                Payload = fileBytes,
                PayloadMD5 = md5Bytes
            };
        }

        public static DeviceMessage GetProgressMessage(string id, long bytesTransferred, long bytesTotal)
        {
            return new DeviceMessage()
            {
                Id = id,
                Message = $"{bytesTransferred} {bytesTotal}",
                MessageType = DeviceMessageType.Progress
            };
        }

        #endregion

        #region Equality members
        protected bool Equals(DeviceMessage other)
        {
            return MessageType == other.MessageType
                   && string.Equals(Id, other.Id)
                   && string.Equals(Message, other.Message)
                   && (
                       (Payload?.Length ?? 0) == (other.Payload?.Length ?? 0)
                       || Payload != null && other.Payload != null && StructuralComparisons.StructuralEqualityComparer.Equals(Payload, other.Payload)
                   )
                   && (
                       (PayloadMD5?.Length ?? 0) == (other.PayloadMD5?.Length ?? 0)
                       || PayloadMD5 != null && other.PayloadMD5 != null && StructuralComparisons.StructuralEqualityComparer.Equals(PayloadMD5, other.PayloadMD5)
                   );
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DeviceMessage)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)MessageType;
                hashCode = (hashCode * 397) ^ (Id != null ? Id.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Message != null ? Message.GetHashCode() : 0);
                return hashCode;
            }
        }


        #endregion

    }
}