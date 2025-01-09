using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoatControl.Communication.Connections.Shared;
using BoatControl.Communication.Helpers;
using BoatControl.Communication.Models;
using BoatControl.Shared.Messaging;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.Logging;

namespace BoatControl.Communication.Connections.Cloud
{
    internal class CloudConnectionSocket : IDeviceConnectionSocket
    {
        private readonly DeviceInfo _deviceInfo;
        private readonly DeviceMessageInterpritator _deviceMessageInterpritator;
        private readonly ILogger<CloudConnectionSocket> _logger;
        private bool isUploading;
        private ClientWebSocket _client;
        private bool _connected;

        public CloudConnectionSocket(ILogger<CloudConnectionSocket> logger, DeviceInfo deviceInfo)
        {
            _logger = logger;
            _deviceMessageInterpritator = new DeviceMessageInterpritator(logger);
            _deviceInfo = deviceInfo;

        }

        public void Dispose()
        {

            //return;
            //// -------
            try
            {
                try
                {
                    _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", new CancellationTokenSource(3000).Token)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch
                {

                }
                _client?.Dispose();
                _client = null;

            }
            catch { /* Sometimes throws errors */}
        }

        public async Task<bool> ConnectAsync(AuthenticationUser user, CancellationToken cancellationToken)
        {

            try
            {
                _connected = false;
                _client = new ClientWebSocket();
                var deviceUri = Configuration.Instance.GetDeviceCloudWebserviceUri(_deviceInfo.Number, user.Id);
                _logger.LogDebug($"[{_deviceInfo.Number} (cloud)] Connecting to {deviceUri.AbsoluteUri}");
                await _client.ConnectAsync(deviceUri, cancellationToken);// &&.ConfigureAwait(false);
                if (await AuthenticateAsync(user, cancellationToken)) //.ConfigureAwait(false))
                {
                    _connected = true;

                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error trying to connect");
                return false;
            }
        }

        public async Task<DeviceMessage> ReadMessageAsync(CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress)
        {

            /*
             * This is really a hack based on hours of debugging android.
             * Android crashes when disconnect token is triggered when waiting for read.
             * But it does not crash if we just close the connection
             */
            if (_connected)
            {
                cancellationToken.Register(() =>
                {
                     _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", new CancellationTokenSource(3000).Token).Wait();
                });
            }

            try
            {
                if (_client == null || _client.State != WebSocketState.Open) throw new Exception("Not connected");
                var message = await _deviceMessageInterpritator.ReadAsync(_client, cancellationToken, WrapProgress(progress, false));

                if (message.MessageType == DeviceMessageType.Progress)
                {
                    // Modify accordingly to match the right progress (x2)
                    var split = message.Message.Split(' ');
                    var bytesTransferred = long.Parse(split[0]);
                    var bytesTotal = long.Parse(split[1]);
                    message.Message = $"{bytesTransferred + bytesTotal * (isUploading ? 1 : 0)} {bytesTotal * 2}";
                }
                return message;

            }
            catch (Exception e)
            {
                throw;
            }
        }


        public async Task SendMessageAsync(DeviceMessage message, CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress)
        {
            //if (_connected)
            //{
            //    while (true)
            //        await Task.Delay(1000, cancellationToken);

            //}



            try
            {
                if (_client == null || _client.State != WebSocketState.Open) return;
                isUploading = message.MessageType == DeviceMessageType.File || message.MessageType == DeviceMessageType.FileWithMd5;
                await _deviceMessageInterpritator.WriteAsync(_client, message, cancellationToken, WrapProgress(progress, true)).ConfigureAwait(false);

            }
            catch (Exception e)
            {

            }
        }

        private async Task<bool> AuthenticateAsync(AuthenticationUser user, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[1024];
            var msgFromServer = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (!msgFromServer.EndOfMessage || msgFromServer.MessageType != WebSocketMessageType.Text)
            {
                _logger.LogDebug($"[{_deviceInfo.Number} (cloud)] Error, expected challenge");
                return false;
            }

            _logger.LogDebug($"[{_deviceInfo.Number} (cloud)] received challenge. Encrypting and sending back challenge");
            var challengeResponse = $"{Encoding.UTF8.GetString(buffer, 0, msgFromServer.Count)}{user.UserToken}".ToSha256();
            var challengeToServer = Guid.NewGuid().ToString("N");
            var msg = $"response={challengeResponse}&challenge={challengeToServer}";
            await _client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, cancellationToken);

            msgFromServer = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (!msgFromServer.EndOfMessage || msgFromServer.MessageType != WebSocketMessageType.Text)
            {
                _logger.LogDebug($"[{_deviceInfo.Number} (cloud)] Error, expected challenge response");
                return false;
            }

            var expectedChallengeResponse = $"{challengeToServer}{user.UserToken}".ToSha256();
            var actualChallengeResponse = Encoding.UTF8.GetString(buffer, 0, msgFromServer.Count);
            if (string.Equals(expectedChallengeResponse, actualChallengeResponse))
            {
                _logger.LogDebug($"[{_deviceInfo.Number} (cloud)] Challenge response accepted, connected, but lets check if cloud is connected to device!");
                await SendMessageAsync(DeviceMessage.GetTextMessage("ping"), cancellationToken, null);
                var pingResponse = await ReadMessageAsync(cancellationToken,null);
                var success = string.Equals(pingResponse?.Message,"pong");
                if (!success)
                {
                    _logger.LogDebug($"[{_deviceInfo.Number} (cloud)] Expected 'pong', got '{pingResponse?.Message ?? "null"}'");
                }

                return success;
            }
            else
            {
                _logger.LogDebug($"[{_deviceInfo.Number} (cloud)] Challenge response rejected, disconnecting..");
                return false;
            }


        }


        private IProgress<DeviceMessageProgress> WrapProgress(IProgress<DeviceMessageProgress> progress, bool isSending)
        {
            if (progress == null) return null;
            return new Progress<DeviceMessageProgress>(p =>
            {
                // Twice as much as we have to go through cloud
                progress?.Report(new DeviceMessageProgress(
                    p.MessageId, 
                    isSending ? p.BytesTransferred : (p.BytesTotal + p.BytesTransferred),
                    p.BytesTotal * 2
                ));
            });
        }

        public override string ToString()
        {
            return $"{_deviceInfo.Number}(cloud)";
        }
    }
}