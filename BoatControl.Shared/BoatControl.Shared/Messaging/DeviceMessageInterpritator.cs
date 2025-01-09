using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BoatControl.Shared.Messaging
{
    public class DeviceMessageInterpritator
    {
        protected readonly ILogger _logger;

        public DeviceMessageInterpritator(ILogger logger)
        {
            this._logger = logger;
        }

        public async Task<DeviceMessage> ReadAsync(Stream stream, CancellationToken cancellationToken, IProgress<DeviceMessageProgress> process = null)      
        {
            // Note that there was a lock(_stream) here before we are trying without...
            var buffer = new byte[1];
            var sb = new StringBuilder();
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, 1, cancellationToken)) > 0 && buffer[0] != '\n')
            {
                sb.Append(Encoding.UTF8.GetString(buffer, 0, 1));
            }
            if (read == 0) // Connection closed
            {
                throw new Exception($"Connection closed");
            }
            var header = sb.ToString().Split(' ');
            int leftOver = Int32.Parse(header[2]);

            string message;
            var messageType = StringToMessageType(header[0]);
            message = await ReadMessageAsync(stream, leftOver, cancellationToken);
            _logger.LogDebug("device -> app ({connectionType}): {message}", "TCP", message);
            switch (messageType)
            {
                case DeviceMessageType.Text:
                case DeviceMessageType.Broadcast:
                case DeviceMessageType.Progress:
                    if (header.Length != 3) throw new Exception("Unknown message format, expected: text/broadcast id length");
                    return new DeviceMessage()
                    {
                        Id = header[1],
                        Message = message,
                        MessageType = messageType
                    };
                case DeviceMessageType.File:
                case DeviceMessageType.FileWithMd5:
                    // 0 = type, 0 1= Id, 2 = Message length, 3 = file length
                    if (header.Length != (messageType == DeviceMessageType.File ? 4 : 5)) throw new Exception("Unknown message format, expected: file id msg-length file-length");
                    var messageId = header[1];

                    // Read file
                    leftOver = Int32.Parse(header[3]);
                    byte[] bytes = new byte[leftOver];
                    if (leftOver > 0)
                    {

                        var reportingStopwatch = new Stopwatch();
                        reportingStopwatch.Start();

                        var pos = 0;
                        read = await stream.ReadAsync(bytes, pos, Math.Min(1024, leftOver), cancellationToken);
                        while (read > 0 && leftOver > 0)
                        {
                            if(reportingStopwatch.ElapsedMilliseconds > 500)
                            {
                                process?.Report(new DeviceMessageProgress(messageId, bytes.Length - leftOver, bytes.Length));
                                reportingStopwatch.Restart();
                            }
                            pos += read;
                            leftOver -= read;
                            if (leftOver > 0)
                                read = await stream.ReadAsync(bytes, pos, Math.Min(1024, leftOver), cancellationToken);
                        }

                        if (read == 0)
                            throw new Exception("Read 0 bytes, this is normally a sign of connection closing");
                    }
                    process?.Report(new DeviceMessageProgress(messageId, bytes.Length - leftOver, bytes.Length));

                    var msg = new DeviceMessage()
                    {
                        Id = messageId,
                        Message = message,
                        Payload = bytes,
                        MessageType = messageType,
                    };
                    if (messageType == DeviceMessageType.FileWithMd5)
                        msg.SetPaymoadMd5ByString(header[4]);

                    return msg;

                default:
                    throw new Exception($"Unknown message format: {sb}");
            }
        }

        private async Task<string> ReadMessageAsync(Stream stream, int leftOver, CancellationToken cancellationToken)
        {
            if (leftOver == 0) return "";

            var sb = new StringBuilder();
            var buffer = new byte[1024];
            var read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, leftOver), cancellationToken);
            while (read > 0 && leftOver > 0)
            {
                sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                leftOver -= read;
                if (leftOver > 0)
                    read = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, leftOver), cancellationToken);
            }

            if (read == 0)
                throw new Exception("Read 0 bytes, this is normally a sign of connection closing");

            return sb.ToString().Substring(0,sb.Length -1); // Remove last \n
        }


        public async Task<DeviceMessage> ReadAsync(WebSocket client, CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress = null)
        {
            byte[] buffer = new byte[1024];
            var sb = new StringBuilder();

            WebSocketReceiveResult result;
            do
            {
                result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new Exception("Expected message of type text, not: " + result.MessageType.ToString("G"));

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            } while (!result.EndOfMessage);


            _logger.LogDebug("device -> app ({connectionType}): {message}", "Cloud", sb.ToString());


            var split = sb.ToString().Split(new char[] { '\n' }, 2);
            if (split.Length != 2)
                throw new Exception("Invalid message format: expected \n");

            var header = split[0].Split(' ');
            sb.Clear();
            var messageType = StringToMessageType(header[0]);


            switch (messageType)
            {
                case DeviceMessageType.Text:
                case DeviceMessageType.Broadcast:
                case DeviceMessageType.Progress:
                    if (header.Length != 3) throw new Exception("Unknown message format, expected: text/broadcast id length");
                    return new DeviceMessage()
                    {
                        Id = header[1],
                        Message = split[1].Substring(0, split[1].Length - 1),
                        MessageType = messageType
                    };
                case DeviceMessageType.File:
                case DeviceMessageType.FileWithMd5:
                    if (header.Length != (messageType == DeviceMessageType.File ? 4 : 5)) throw new Exception("Unknown message format, expected: file id msg-length file-length");

                    var fileDownload = new DeviceMessage()
                    {
                        Id = header[1],
                        Message = split[1].Length > 0 ? split[1].Substring(0,split[1].Length-1) : "",
                        Payload = new byte[int.Parse(header[3])],
                        MessageType = messageType,
                    };

                    // Md5 stuff
                    if (messageType == DeviceMessageType.FileWithMd5)
                        fileDownload.SetPaymoadMd5ByString(header[4]);

                    var reportingStopwatch = new Stopwatch();
                    reportingStopwatch.Start();


                    var fileDownloadPos = 0;
                    while (fileDownloadPos < fileDownload.Payload.Length)
                    {

                        if(reportingStopwatch.ElapsedMilliseconds > 500)
                        {
                            progress?.Report(new DeviceMessageProgress(fileDownload.Id, fileDownloadPos, fileDownload.Payload.Length));
                            reportingStopwatch.Restart();
                        }
                        result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        switch (result.MessageType)
                        {
                            case WebSocketMessageType.Binary:
                                Buffer.BlockCopy(buffer, 0, fileDownload.Payload, fileDownloadPos, result.Count);
                                fileDownloadPos += result.Count;
                                break;
                            default:
                                throw new Exception($"Expected binary result to filedownload: {fileDownload.Message}");
                        }
                    }
                    progress?.Report(new DeviceMessageProgress(fileDownload.Id, fileDownloadPos, fileDownload.Payload.Length));
                    return fileDownload;
                default:
                    throw new Exception($"Unknown message format: {sb}");
            }

        }

      
        public async Task WriteAsync(Stream stream, DeviceMessage message, CancellationToken cancellationToken,IProgress<DeviceMessageProgress> progress = null)
        {
            var messageString = GetMessageString(message);
            _logger.LogDebug("app -> device ({connectionType}): {message}", "TCP" ,message);


            var cmdBytes = Encoding.UTF8.GetBytes(messageString);
            await stream.WriteAsync(cmdBytes, 0, cmdBytes.Length, cancellationToken);
            if ((message.MessageType == DeviceMessageType.File || message.MessageType == DeviceMessageType.FileWithMd5) && (message.Payload?.Length ?? 0) > 0)
            {

                var reportingStopwatch = new Stopwatch();
                reportingStopwatch.Start();

                var packages = Math.Ceiling(message.Payload.Length / 1024d);
                for (var i = 0; i < packages; i++)
                {
                    if(reportingStopwatch.ElapsedMilliseconds > 500)
                    {
                        progress?.Report(new DeviceMessageProgress(message.Id, i * 1024, message.Payload.Length));
                        reportingStopwatch.Restart();
                    }
                    await stream.WriteAsync(message.Payload, i * 1024, Math.Min(1024, message.Payload.Length - 1024 * i), cancellationToken);
                }
                progress?.Report(new DeviceMessageProgress(message.Id, message.Payload.Length, message.Payload.Length));
            }
        }

        public async Task WriteAsync(WebSocket socket, DeviceMessage message,  CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress = null)
        {
            var messageString = GetMessageString(message);
            _logger.LogDebug("device -> app ({connectionType}): {message}", "Cloud", messageString);

            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString)), WebSocketMessageType.Text, true, cancellationToken);
            if ((message.MessageType == DeviceMessageType.File || message.MessageType == DeviceMessageType.FileWithMd5) && (message.Payload?.Length ?? 0) > 0)
            {
                var packages = Math.Ceiling(message.Payload.Length / 1024d);
                var reportingStopwatch = new Stopwatch();
                reportingStopwatch.Start();
                for (var i = 0; i < packages; i++)
                {
                    if(reportingStopwatch.ElapsedMilliseconds > 500)
                    {
                        progress?.Report(new DeviceMessageProgress(message.Id, i * 1024, message.Payload.Length));
                        reportingStopwatch.Restart();
                    }
                    var arraySegment = new ArraySegment<byte>(message.Payload, i * 1024, Math.Min(1024, message.Payload.Length - 1024 * i));

                    // Always send as last as we already took care of file sizes
                    await socket.SendAsync(arraySegment, WebSocketMessageType.Binary, i == packages - 1, cancellationToken);
                }
                progress?.Report(new DeviceMessageProgress(message.Id, message.Payload.Length, message.Payload.Length));
            }
        }




        protected string GetMessageString(DeviceMessage message)
        {
            var msg = message.Message;
            switch (message.MessageType)
            {
                case DeviceMessageType.Broadcast:
                case DeviceMessageType.Progress:
                case DeviceMessageType.Text:
                    msg = $"{MessageTypeToString(message.MessageType)} {message.Id} {msg.Length + 1}\n{message.Message}\n";
                    break;
                case DeviceMessageType.File:
                    msg = $"{MessageTypeToString(message.MessageType)} {message.Id} {msg.Length + 1} {message.Payload?.Length}\n{message.Message}\n";
                    break;
                case DeviceMessageType.FileWithMd5:
                    msg = $"{MessageTypeToString(message.MessageType)} {message.Id} {msg.Length + 1} {message.Payload?.Length} {message.GetPayloadMd5String()}\n{message.Message}\n";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return msg;
        }

        protected string MessageTypeToString(DeviceMessageType messageType)
        {
            switch (messageType)
            {
                case DeviceMessageType.File:
                    return "file";
                case DeviceMessageType.FileWithMd5:
                    return "filemd5";
                case DeviceMessageType.Broadcast:
                    return "broadcast";
                case DeviceMessageType.Progress:
                    return "progress";
                case DeviceMessageType.Text:
                default:
                    return "text";
            }
        }

        protected DeviceMessageType StringToMessageType(string messageType)
        {
            switch (messageType)
            {
                case "file":
                    return DeviceMessageType.File;
                case "filemd5":
                    return DeviceMessageType.FileWithMd5;
                case "broadcast":
                    return DeviceMessageType.Broadcast;
                case "progress":
                    return DeviceMessageType.Progress;
                case "text":
                default:
                    return DeviceMessageType.Text;
            }
        }
    }
}