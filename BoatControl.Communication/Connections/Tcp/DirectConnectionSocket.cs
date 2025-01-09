using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoatControl.Communication.Connections.Shared;
using BoatControl.Communication.Helpers;
using BoatControl.Communication.Models;
using BoatControl.Shared.Messaging;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.Logging;

namespace BoatControl.Communication.Connections.Tcp
{
    internal class DirectConnectionSocket : IDeviceConnectionSocket
    {
        private DeviceMessageInterpritator _deviceMessageInterpritator;
        internal static readonly string _sharedSecret = "c1278456931740f0a78775565d6c881fdd5c7d75130b4d2d92901d2e3fd145efabc755f4dd3d4a95b1bf200af8ea93cb";
        private readonly ILogger<DirectConnectionSocket> _logger;

        private readonly AuthenticationUser _user;
        private readonly string _host;
        private TcpClient _client;
        private NetworkStream _stream;

        // Used as a workaround, as we want to register stream.close() as stream does not respect cancellation token fully
        private bool _isCancellationTokenRegistered;

        public bool Connected => _client?.Connected?? false;

        public DirectConnectionSocket(ILogger<DirectConnectionSocket> logger, AuthenticationUser user, string host)
        {
            _logger = logger;
            _deviceMessageInterpritator = new DeviceMessageInterpritator(logger);
            _user = user;
            _host = host;
        }

        public async Task<BoatControlChallenge> Peek(CancellationToken cancellationToken)
        {
            try
            {
                _client = new TcpClient() { NoDelay = true };


                _client.Client.Connect(_host, 8080);
                _stream = _client.GetStream();
                byte[] bytes = new byte[_client.ReceiveBufferSize];
                var read = await _stream.ReadAsync(bytes, 0, bytes.Length, cancellationToken);
                var msg = Encoding.UTF8.GetString(bytes, 0, read);
                var serverChallenge = BoatControlChallenge.GetChallenge(msg);
                var closeMsg = Encoding.UTF8.GetBytes("close\n");
                await _stream.WriteAsync(closeMsg,0, closeMsg.Length);
                _stream.Flush();
                _stream.Close();
                _client.Close();
                return serverChallenge;
            }
            catch(Exception e)
            {
                _logger.LogWarning(e, "Exception trying to peek");
                return null;
            }
            finally
            {
                _client?.Dispose();
                _stream?.Dispose();

            }
        }

        public async Task<bool> ConnectAsync(AuthenticationUser user, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug($"[{_host}] Trying to connect..");
                _client = new TcpClient();
                _client.ReceiveBufferSize = 1024 * 20;
                _client.SendBufferSize = 1024 * 20;
                await _client.ConnectAsync(_host, 8080);
                _stream = _client.GetStream();
                return await Authorize(user, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error trying to connect");
                return false;
            }
        }


        public async Task<DeviceMessage> ReadMessageAsync(CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress)
        {
            if (!Connected) throw new Exception("Not connected");

            if (cancellationToken != null && !_isCancellationTokenRegistered)
            {
                _isCancellationTokenRegistered = true;
                cancellationToken.Register(() => _stream.Close(200));
            }


            return await _deviceMessageInterpritator.ReadAsync(_stream, cancellationToken, progress).ConfigureAwait(false);
        }

        public async Task SendMessageAsync(DeviceMessage message, CancellationToken cancellationToken, IProgress<DeviceMessageProgress> progress)
        {
            if (!Connected) throw new Exception("Not connected");
            await _deviceMessageInterpritator.WriteAsync(_stream, message, cancellationToken, progress).ConfigureAwait(false);
        }

        private async Task<bool> Authorize(AuthenticationUser user, CancellationToken cancellationToken)
        {
            byte[] bytes = new byte[_client.ReceiveBufferSize];
            var read = await _stream.ReadAsync(bytes, 0, bytes.Length, cancellationToken);
            var msg = Encoding.UTF8.GetString(bytes, 0, read);

            var serverChallenge = BoatControlChallenge.GetChallenge(msg);
            if (serverChallenge == null)
            {
                Console.WriteLine("Challenge was not valid");
                return false;
            }

            // Identify which secret to use
            var secret = serverChallenge.Owner == 0 ? _sharedSecret : user.UserToken;

            // SendAsync challenge response and new challenge
            var challenge = Guid.NewGuid().ToString("N");


            var challengeResponse = $"{serverChallenge.Challenge}{secret}".ToSha256();
            await Send($"{challengeResponse} {challenge}", cancellationToken);

            // Read response
            read = await _stream.ReadAsync(bytes, 0, bytes.Length, cancellationToken);
            msg = Encoding.UTF8.GetString(bytes, 0, read).TrimEnd('\n');
            var expectedResponse = $"{challenge}{secret}".ToSha256();
            if (msg != expectedResponse)
            {
                _logger.LogError($"[{_host}] Invalid authentication response  response\nGot: {msg}\nExpected: {expectedResponse}");
                return false;
            }
            _logger.LogDebug($"[{_host}] Successfully authenticated with device");
            return true;
        }

        private async Task Send(string cmd, CancellationToken cancellationToken)
        {
            cmd = cmd.TrimEnd('\n');
            _logger.LogDebug($"[{_host}] sending: {cmd}");
            var cmdBytes = Encoding.UTF8.GetBytes(cmd.TrimEnd('\n') + "\n");
            await _stream.WriteAsync(cmdBytes, 0, cmdBytes.Length, cancellationToken).ConfigureAwait(false);
        }

        public override string ToString()
        {
            return $"{this._host}";
        }
        public void Dispose()
        {
            try
            {
                _client?.Close();
                _stream?.Close();
            }
            catch
            {

            }
        }
    }
}