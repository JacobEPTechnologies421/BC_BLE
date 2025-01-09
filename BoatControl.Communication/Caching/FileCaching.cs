//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using BoatControl.Communication.Models;
//using BoatControl.Communication.Storage;
//using BoatControl.Shared.Messaging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BoatControl.Communication.Connections;
using BoatControl.Communication.Models;
using BoatControl.Communication.Storage;
using BoatControl.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace BoatControl.Communication.Caching
{
    internal class FileCaching
    {
        private readonly ILogger<FileCaching> _logger;
        private readonly IDeviceConnectionManager _device;
        private readonly IPersistedStorage _storage;
        private readonly Dictionary<string, CachedFile> _cachedFiles = new Dictionary<string, CachedFile>();


        public FileCaching(ILogger<FileCaching> logger, IDeviceConnectionManager device, IPersistedStorage storage)
        {
            _logger = logger;
            _device = device;
            _storage = storage;
            _cachedFiles = _storage.GetValueOrDefault("FileCaching:" + device.DeviceInfo.Number, new Dictionary<string, CachedFile>());
        }

        public async Task<DeviceMessage> Wrap(
        DeviceMessage message,
        IProgress<DeviceMessageProgressWithDirection> progress,
        Func<DeviceMessage, IProgress<DeviceMessageProgressWithDirection>, Task<DeviceMessage>> next)
        {
            if (message.MessageType == DeviceMessageType.Text && message.Message.StartsWith("file download"))
            {
                return await FileDownload(message, progress, next);
            }
            else if (message.MessageType == DeviceMessageType.File || message.MessageType == DeviceMessageType.FileWithMd5)
            {
                return await FileUpload(message, progress, next);
            }
            return await next(message, progress);
        }


        private async Task<DeviceMessage> FileDownload(
            DeviceMessage message,
            IProgress<DeviceMessageProgressWithDirection> progress,
            Func<DeviceMessage, IProgress<DeviceMessageProgressWithDirection>, Task<DeviceMessage>> next)
        {
            var split = message.Message.Split(new char[] { ' ' }, 3);
            var name = split[2].Trim('/');

            DeviceMessage response;
            if (_cachedFiles.TryGetValue(name, out var cachedFile) && !string.IsNullOrEmpty(cachedFile.Md5))
            {
                // TODO: REMOVE!!!
                response = await next(DeviceMessage.GetTextMessage($"file download /{name} {cachedFile.Md5}{new Random().Next(0,10)}", message.Id), progress);
                if(response.Message == "cachehit")
                {
                    var msg = new DeviceMessage()
                    {
                        Id = message.Id,
                        Message = $"/{name}",
                        Payload = Convert.FromBase64String(cachedFile.Base64Content),
                        MessageType = DeviceMessageType.FileWithMd5
                    };
                    msg.SetPaymoadMd5ByString(cachedFile.Md5);
                    return msg;
                }
            }
            else
            {
                response = await next(message, progress);
            }

            if(response.Message.StartsWith("error") || (response?.Payload?.Length ?? 0) == 0 || name == "coredump.bin")
                return response;

            if (response.MessageType == DeviceMessageType.File)
                response.GeneratePayloadMd5();

            if (response.MessageType == DeviceMessageType.FileWithMd5)
                response.VerifyMd5();

            _logger.LogDebug("[{deviceNumber}] Cached file not found but stored for '{fileName}'", _device.DeviceInfo.Number, name);

            _cachedFiles[name] = new CachedFile() {  Base64Content = Convert.ToBase64String(response.Payload), Md5 = response.GetPayloadMd5String() };
            Store();
            return response;
        }

        private async Task<DeviceMessage> FileUpload(
            DeviceMessage message,
            IProgress<DeviceMessageProgressWithDirection> progress,
            Func<DeviceMessage, IProgress<DeviceMessageProgressWithDirection>, Task<DeviceMessage>> next)
        {
            var timestamp = DateTime.Now.ToString("O");

            // SendAsync timestamp file and file afterwards
            var name = message.Message.Split(' ').Last().Trim('/');

            if (message.MessageType == DeviceMessageType.File)
                message.GeneratePayloadMd5();

            var response = await next(message, progress);

            // Store if there are any data
            if (!response.Message.StartsWith("error"))
            {
                _logger.LogDebug($"[{_device.DeviceInfo.Number}] Uploaded file stored for '{name}'");
                _cachedFiles[name] = new CachedFile() { Md5 = message.GetPayloadMd5String(), Base64Content = Convert.ToBase64String(message.Payload) };
                Store();
            }
            return response;
        }

        private void Store()
        {
            _storage.AddOrUpdateValue("FileCaching:" + _device.DeviceInfo.Number, _cachedFiles);
            _logger.LogDebug("Saving cached files");
        }


    }
}
