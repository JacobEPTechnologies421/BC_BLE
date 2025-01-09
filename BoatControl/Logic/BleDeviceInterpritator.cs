using BoatControl.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoatControl.Logic
{
    // Todo, implement timeout
    public class BLEDeviceMessageInterpritator : DeviceMessageInterpritator
    {

        private static TimeSpan _bleCommunicationTimeout = TimeSpan.FromSeconds(5);

        public delegate void DownloadProgress(DeviceMessageProgress progress);
        public delegate void DeviceMessageReceived(DeviceMessage deviceMessage);
        public event DeviceMessageReceived OnDeviceMessageReceived;
        public event DownloadProgress OnDownloadProgress;


        // For sending file
        private SemaphoreSlim _sendFileSemaphoreSlim = new SemaphoreSlim(0, 1);
        private bool _sendingFile = false;


        private ICharacteristic _subscribedCharacteristic;
        private int packageSize = 20;
        private StringBuilder headerBuilder = new StringBuilder();
        private StringBuilder messageBuilder = new StringBuilder();
        private int messageLeftover;
        private string messageId;
        private DeviceMessageType messageType;
        private bool headerComplete = false;
        private string[] header;

        // File related
        private DeviceMessage fileDownload;
        private long fileDownloadBytesReceived;
        private int packagesSentSinceReport;

        public BLEDeviceMessageInterpritator(ILogger<DeviceMessageInterpritator> logger) : base(logger)
        {
        }

        public async Task WriteAsync(ICharacteristic characteristic, DeviceMessage message, CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress = null)
        {
            try
            {
                var messageString = GetMessageString(message);
                _logger.LogDebug("App -> Device (BLE): {message}", messageString);

                for (var i = 0; i < Math.Ceiling(messageString.Length / (double)packageSize); i++)
                {
                    var partialMessageString = String.Join("", messageString.Skip(i * packageSize).Take(packageSize));
                    //Debug.Print($"[ble] part message: {partialMessageString}");
                    var partialMessageBytes = Encoding.UTF8.GetBytes(partialMessageString);

                    var resultTask = characteristic.WriteAsync(partialMessageBytes, cancellationToken);
                    await Task.WhenAny(new[] { resultTask, Task.Delay(_bleCommunicationTimeout, cancellationToken) });

                    if (!resultTask.IsCompleted || resultTask.Result == 0)
                    {
                        _logger.LogDebug("App -> Device (BLE): {message}", messageString);
                        _logger.LogError("App -> BLE: Failed sending: {partialMessageString}", partialMessageString);

                        throw new CoultNotWriteBleException();
                    }

                }

                if ((message.MessageType == DeviceMessageType.File || message.MessageType == DeviceMessageType.FileWithMd5) && (message.Payload?.Length ?? 0) > 0)
                {
                    _sendingFile = true;

                    _logger.LogDebug("[ble] Waiting for 'ok' message");
                    await _sendFileSemaphoreSlim.WaitAsync(cancellationToken);
                    _logger.LogDebug("[ble] OK Received, sending file");

                    var packages = Math.Ceiling(message.Payload.Length / (double)packageSize);
                    var reportingStopwatch = new Stopwatch();
                    reportingStopwatch.Start();
                    for (var i = 0; i < packages; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Reporting
                        if (reportingStopwatch.ElapsedMilliseconds > 200)
                        {
                            progress?.Report(new DeviceMessageProgress(message.Id, i * packageSize, message.Payload.Length));
                            packagesSentSinceReport = 0;
                            reportingStopwatch.Restart();
                        }
                        packagesSentSinceReport++;


                        // Sending
                        var partialMessageBytes = message.Payload.Skip(i * packageSize).Take(packageSize).ToArray();
                        //_logger.LogDebug($"[ble] part message: {Encoding.UTF8.GetString(partialMessageBytes)}");

                        var resultTask = characteristic.WriteAsync(partialMessageBytes, cancellationToken);
                        await Task.WhenAny(new[] { resultTask, Task.Delay(_bleCommunicationTimeout, cancellationToken) });

                        if (!resultTask.IsCompleted || resultTask.Result == 0)
                        {
                            _logger.LogError("App -> BLE: Failed sending: {message}", message.Message);
                            throw new CoultNotWriteBleException();
                        }

                    }
                    progress?.Report(new DeviceMessageProgress(message.Id, message.Payload.Length, message.Payload.Length));
                }
            }
            catch (Exception e)
            {
                throw;
            }


        }

        private void ValueUpdated(object sender, CharacteristicUpdatedEventArgs e)
        {
            //if(_logger.IsTraceEnabled)
            //    _logger.Trace($"[ble] incomming: {e.Characteristic.StringValue}\r\n");
            _logger.LogDebug("[BLE] Incoming: {stringValue}", e.Characteristic.StringValue);

            if (_sendingFile && _sendFileSemaphoreSlim.CurrentCount == 0 && e.Characteristic.StringValue == "ok\n")
            {
                _sendingFile = false;
                _sendFileSemaphoreSlim.Release();
                return;
            }


            if (fileDownload != null)
            {

                for (var i = 0; i < e.Characteristic.Value.Length; i++)
                {
                    if (i + fileDownloadBytesReceived < fileDownload.Payload.Length)
                        fileDownload.Payload[i + fileDownloadBytesReceived] = e.Characteristic.Value[i];
                }
                fileDownloadBytesReceived += e.Characteristic.Value.Length;


                // Reporting
                if (packagesSentSinceReport > 1000 / packageSize || fileDownloadBytesReceived >= fileDownload.Payload.Length)
                {
                    OnDownloadProgress?.Invoke(new DeviceMessageProgress(fileDownload.Id, fileDownloadBytesReceived, fileDownload.Payload.Length));
                    packagesSentSinceReport = 0;
                }
                packagesSentSinceReport++;


                // If file is finished, tell about it :)
                if (fileDownloadBytesReceived >= fileDownload.Payload.Length)
                {
                    _logger.LogDebug(
                        "{fileId}: Done: {bytesReceived} / {payloadLength} ({progress}%)",
                        fileDownload.Id,
                        fileDownloadBytesReceived,
                        fileDownload.Payload.Length,
                        Math.Round((decimal)fileDownloadBytesReceived * 100 / (decimal)fileDownload.Payload.Length, 2)
                    );
                    OnDeviceMessageReceived?.Invoke(fileDownload);
                    fileDownload = null;
                }
                else
                {
                    _logger.LogDebug(
                        "{fileId}: Loading: {bytesReceived} / {payloadLength} ({progress}%)",
                        fileDownload.Id,
                        fileDownloadBytesReceived,
                        fileDownload.Payload.Length,
                        Math.Round((decimal)fileDownloadBytesReceived / (decimal)fileDownload.Payload.Length * 100, 2)
                    );

                }
                return;
            }

            foreach (var b in e.Characteristic.StringValue)
            {
                if (!headerComplete)
                {
                    if (b == '\n')
                    {
                        header = headerBuilder.ToString().Split(' '); // 0 = type, 1 = id, 2 = messageBuilder length
                        messageLeftover = Int32.Parse(header[2]);
                        messageId = header[1];
                        messageType = StringToMessageType(header[0]);
                        headerComplete = true;
                    }
                    else
                    {
                        headerBuilder.Append(b);
                    }
                }
                else
                {
                    if (messageLeftover > 0)
                    {
                        messageBuilder.Append(b);
                        messageLeftover--;
                    }
                }
            }

            if (headerComplete && messageLeftover <= 0)
            {
                string messageStr = messageBuilder.ToString().TrimEnd('\n');
                _logger.LogDebug(
                    "Device -> App (BLE): {header}{message}",
                    headerBuilder.ToString(),
                    messageStr
                );
                switch (messageType)
                {
                    case DeviceMessageType.Text:
                    case DeviceMessageType.Broadcast:
                    case DeviceMessageType.Progress:
                        if (header == null || header.Length != 3) throw new Exception("Unknown message format, expected: text/broadcast id length");

                        var message = new DeviceMessage()
                        {
                            Id = messageId,
                            Message = messageStr,
                            MessageType = messageType
                        };

                        OnDeviceMessageReceived?.Invoke(message);
                        break;
                    case DeviceMessageType.File:
                    case DeviceMessageType.FileWithMd5:
                        if (header == null || header.Length != (messageType == DeviceMessageType.File ? 4 : 5)) throw new Exception("Unknown message format, expected: file id msg-length file-length");

                        var split = messageStr.Split(' ');
                        fileDownloadBytesReceived = 0;
                        fileDownload = new DeviceMessage()
                        {
                            Id = header[1],
                            Message = split[0].Length > 1 ? split[0] : "",
                            Payload = new byte[int.Parse(header[3])],
                            MessageType = messageType,
                        };

                        // Md5 stuff
                        if (messageType == DeviceMessageType.FileWithMd5)
                            fileDownload.SetPaymoadMd5ByString(header[4]);

                        break;
                }
                // Clean up
                headerBuilder.Clear();
                messageBuilder.Clear();
                headerComplete = false;
            }
        }

        public async Task SubscribeAsync(ICharacteristic bleRx)
        {
            Unsubscribe();
            _subscribedCharacteristic = bleRx;
            bleRx.ValueUpdated += ValueUpdated;
            await bleRx.StartUpdatesAsync();
        }

        public void Unsubscribe()
        {
            _sendFileSemaphoreSlim = new SemaphoreSlim(0, 1);
            _sendingFile = false;
            if (_subscribedCharacteristic != null)
            {
                _subscribedCharacteristic.ValueUpdated -= ValueUpdated;
                messageBuilder.Clear();
                headerBuilder.Clear();
                fileDownload = null;

                headerComplete = false;
            }
        }

        public void SetMtuSize(int mtuSize)
        {
            this.packageSize = mtuSize - 3;
        }
    }
}
