using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoatControl.Communication.Connections.Ble.Searching;
using BoatControl.Communication.Connections.Shared;
using BoatControl.Communication.Connections.Tcp;
using BoatControl.Communication.Helpers;
using BoatControl.Communication.Models;
using BoatControl.Logic;
using BoatControl.Shared.Messaging;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.Logging;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace BoatControl.Communication.Connections.Ble
{
    internal class BleConnection : IDeviceConnection
    {
        #region Static guids
        private static readonly Guid ServiceGuid = Guid.Parse("02FB504B-B9E9-4DFE-90F3-CF60AB55A8E0");
        private static readonly Guid CharacteristicTxGuid = Guid.Parse("CFA58E3B-AB9F-48B1-B27F-00088952CA86");
        private static readonly Guid CharacteristicRxGuid = Guid.Parse("0EAF8BF5-2D2E-4602-A72A-0D2B051874A5");
        private static readonly Guid CharacteristicResetGuid = Guid.Parse("119367EB-67AB-4192-93E5-D3ECA3D0FEE7");
        #endregion

        #region Fields for connection
        private IService _bleDevice;
        private ICharacteristic _bleRx;
        private ICharacteristic _bleTx;
        private ICharacteristic _bleReset;

        #endregion



        private readonly ConcurrentDictionary<string, DeviceMessageContainer> _promises = new ConcurrentDictionary<string, DeviceMessageContainer>();
        private CancellationTokenSource _disconnectTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
        private readonly AuthenticationUser _user;
        private readonly FoundBleDevice _device;
        private readonly ILogger<BleConnection> _logger;
        private readonly BLEDeviceMessageInterpritator _messageInterpritator;

        private DateTime _lastReceivedMessage;
        private bool _isDisposed;
        private Thread _reconnectingLoopThread = null;

        private SemaphoreSlim _connectingSemaphore;

        #region Authentication
        private bool _authenticated;
        private string _authenticationChallenge;

        #endregion

        public bool IsDiscovered { get; set; } = true;
        public event SocketConnectionChangeDelegate OnConnectionChange;
        public event OnBroadcastMessageDelegate OnBroadcastMessage;
        public bool IsConnected { get; set; }


        public BleConnection(ILogger<BleConnection> logger, AuthenticationUser _user, FoundBleDevice device, SemaphoreSlim sendSemaphore)
        {
            this._user = _user;
            _device = device;
            _logger = logger;
            _messageInterpritator = new BLEDeviceMessageInterpritator(_logger);
            _messageInterpritator.OnDeviceMessageReceived += OnDeviceMessageReceived;
            _messageInterpritator.OnDownloadProgress += OnDownloadProgress;
            CrossBluetoothLE.Current.Adapter.DeviceConnectionLost += DeviceConnectionLost;
            CrossBluetoothLE.Current.Adapter.DeviceDisconnected += DeviceDisconnected;
            CrossBluetoothLE.Current.Adapter.DeviceConnected += Adapter_DeviceConnected;
        }

        internal async Task<bool> Pair()
        {
            var response = await SendMessageAsync(DeviceMessage.GetTextMessage($"device setowner {_user.Id} {_user.UserToken}"), null);

            
            if (string.Equals(response?.Message, "ok"))
            {

                if (_bleReset != null)
                {
                    if ((await _bleReset.WriteAsync(new byte[] { 49 }, _disconnectTokenSource.Token)) != 0)
                        throw new NoConnectionException("Could not reset connection");
                }

                return true;
            }
            _logger.LogError("Error trying to pair, got response: {responseMessage}", response.Message);

            return false;
        }

        private async void Adapter_DeviceConnected(object sender, DeviceEventArgs e)
        {
            if (e.Device != this._device.Device) return; // Event comes for all

            _disconnectTokenSource = new CancellationTokenSource();
            if (_sendSemaphore.CurrentCount == 0) _sendSemaphore.Release();
            
            var cts = new CancellationTokenSource();
            cts.CancelAfter(5000);
            try
            {
                this._bleDevice = await _device.Device.GetServiceAsync(ServiceGuid, cts.Token);
                if(this._bleDevice == null)
                {
                    await CrossBluetoothLE.Current.Adapter.DisconnectDeviceAsync(this._device.Device);
                    return;
                }
                this._bleRx = await this._bleDevice.GetCharacteristicAsync(CharacteristicRxGuid);
                this._bleTx = await this._bleDevice.GetCharacteristicAsync(CharacteristicTxGuid);
                this._bleReset = await this._bleDevice.GetCharacteristicAsync(CharacteristicResetGuid);
                var mtuSize = await this._bleDevice.Device.RequestMtuAsync(517);
                _messageInterpritator.SetMtuSize(mtuSize);
                await _messageInterpritator.SubscribeAsync(this._bleRx);

                await Task.Delay(200); // Relax a bit and let it get up and going
                await AuthenticateAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed while connected");
                try
                {
                    await CrossBluetoothLE.Current.Adapter.DisconnectDeviceAsync(this._device.Device);

                }
                catch
                {
                }
            }
            finally
            {
                if (_connectingSemaphore != null && _connectingSemaphore.CurrentCount == 0)
                    _connectingSemaphore.Release();

            }
        }

        #region Registration

        private async Task RegisterDevice()
        {
            try
            {
                var result = await SendMessageAsync(DeviceMessage.GetTextMessage("device info"), null);

                var version = result.Message.Trim().Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)
                    .First(a => a.StartsWith("version"))
                    .Split(' ').Last();
                    
                result = await SendMessageAsync(DeviceMessage.GetTextMessage("registration mac"), null);
                var dic = result.Message.Trim().Split('&').Select(a => a.Split('=')).ToDictionary(b => b[0], b => b[1]);

                if (dic.TryGetValue("WIFI_STA", out var macSta) && dic.TryGetValue("WIFI_SOFTAP", out var macAp) &&
                    dic.TryGetValue("BT", out var macBt) && dic.TryGetValue("ETH", out var macEth))
                {
                    var registrationResult = await Register("ff6f8e73-1a31-4265-b7ff-10051794bb5d", macSta, "esp32",
                        version, 2, macAp, macBt, macEth);

                    result = await SendMessageAsync(DeviceMessage.GetTextMessage($"registration register {registrationResult.Number} {registrationResult.Secret}"), null);
                    _logger.LogInformation("Device registration: New device number: {newDeviceNumber}", registrationResult.Number);

                    await SendMessageAsync(DeviceMessage.GetTextMessage("device restart"), null);
                    await Task.Delay(1000);
                    await CrossBluetoothLE.Current.Adapter.DisconnectDeviceAsync(_bleDevice.Device);
                }
                else
                {
                    _logger.LogError("Cannot register device, arguments missing");
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not register device");
            }


        }


        private async Task<RegisterResult> Register(string secret, string mac, string platform, string version, int releaseGroup, string macHotspot, string macBluetooth, string macEthernet)
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri("https://boatcontrol.net"),
                Timeout = TimeSpan.FromSeconds(15)
            };
            var url = $"/api/device/register?secret={secret}&platform={platform}&version={version}&releaseGroup={releaseGroup}&mac={mac}&macHotspot={macHotspot}&macBluetooth={macBluetooth}&macEthernet={macEthernet}";
            var response = await client.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Wrong response code: {response.StatusCode}");
            }
            return new RegisterResult(await response.Content.ReadAsStringAsync());
        }

        #endregion


        private async Task AuthenticateAsync(CancellationToken cancellationToken)
        {
            await _sendSemaphore.WaitAsync(cancellationToken);
            _authenticationChallenge = Guid.NewGuid().ToString("N");
            
            await _messageInterpritator.WriteAsync(this._bleTx, DeviceMessage.GetTextMessage(_authenticationChallenge, "auth"), cancellationToken);
        }

        private void DeviceDisconnected(object sender, DeviceEventArgs e)
        {
            if (e.Device != this._device.Device) return; // Event comes for all

            IsConnected = false;

            _logger.LogError("Device disconnected: {deviceNumber}", _device.Number);
            _authenticated = false;
            this.OnConnectionChange?.Invoke(false);
            _disconnectTokenSource.Cancel();
            if (_reconnectingLoopThread == null)
                (_reconnectingLoopThread = new Thread(ReconnectingLoop)).Start();
        }

        private void DeviceConnectionLost(object sender, DeviceErrorEventArgs e)
        {
            if (e.Device != this._device.Device) return; // Event comes for all

            _logger.LogError("Device connection lost: {deviceNumber}, Error: {errorMessage}", _device.Number, e.ErrorMessage);
            IsConnected = false;
            this.OnConnectionChange?.Invoke(false);
            _disconnectTokenSource.Cancel();
            if (_reconnectingLoopThread == null)
                (_reconnectingLoopThread = new Thread(ReconnectingLoop)).Start();

        }

        #region Public Methods
        public async Task ConnectAsync()
        {
            try
            {
                if (_device.Device.State != DeviceState.Connected)
                {
                    _connectingSemaphore = new SemaphoreSlim(0, 1);
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(8000);
                    if (!CrossBluetoothLE.Current.IsAvailable)
                        throw new NoConnectionException("This device does not support BLE");

                    if (!CrossBluetoothLE.Current.IsOn)
                        throw new NoConnectionException("Please turn Bluetooth on");


                    await CrossBluetoothLE.Current.Adapter.ConnectToDeviceAsync(_device.Device, new ConnectParameters(true, true), cts.Token);

                    await _connectingSemaphore.WaitAsync(cts.Token);
                    if (!_device.Connected)
                        throw new NoConnectionException();

                }

                IsConnected = true;
                if (this._device.IsUnregistered())
                {
                    await RegisterDevice();
                }
                else
                {
                    this.OnConnectionChange?.Invoke(IsConnected);
                }


            }
            catch (Exception e)
            {
                IsConnected = false;
            }
        }



        public void Start()
        {
        }

        public async Task<DeviceMessage> SendMessageAsync(DeviceMessage message, IProgress<DeviceMessageProgressWithDirection> progress)
        {
            if (!IsConnected)
                throw new NoConnectionException();




            // Delay min 200ms before sending new message
            //var msSinceLastMessage = (DateTime.Now - _lastReceivedMessage).TotalMilliseconds;
            //if (msSinceLastMessage < 100)
            //    await Task.Delay(100 - (int)msSinceLastMessage);

            var promise = new TaskCompletionSource<DeviceMessage>();
            try
            {
                await _sendSemaphore.WaitAsync(_disconnectTokenSource.Token);
                var fileMessage = false;
                _promises[message.Id] = new DeviceMessageContainer(message, promise, new Progress<DeviceMessageProgress>(p =>
                {
                    fileMessage = true;
                    _lastReceivedMessage = DateTime.Now;
                    progress?.Report(new DeviceMessageProgressWithDirection(p, EnumSendProgressDirection.Receiving));
                }));

                if(_bleReset != null)
                {
                    if (await _bleReset.WriteAsync(new byte[] { 49 }, _disconnectTokenSource.Token) != 0)
                        throw new NoConnectionException("Could not reset connection");
                }

                await _messageInterpritator.WriteAsync(_bleTx, message, _disconnectTokenSource.Token, new Progress<DeviceMessageProgress>(p =>
                {
                    fileMessage = true;
                    _lastReceivedMessage = DateTime.Now;
                    progress?.Report(new DeviceMessageProgressWithDirection(p, EnumSendProgressDirection.Sending));
                }));


                // Loop for incorporating timeouts for file transfer
                Task delay = Task.Delay(15000);
                while(await Task.WhenAny(promise.Task, delay) == delay)
                {
                    delay = Task.Delay(5000);
                    if ((DateTime.Now - _lastReceivedMessage) > TimeSpan.FromSeconds(30) && fileMessage)
                        throw new Exception("File message detected and did not receive any message the last 15 seconds");
                }

                var result = await promise.Task;
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[BLE] Error trying to send message '{message}'", message.Message);
                _promises.TryRemove(message.Id, out var removed);
                promise.TrySetException(e);
                throw;
            }
            finally
            {
                if (_sendSemaphore.CurrentCount == 0)
                    _sendSemaphore.Release();
            }

        }
        #endregion

        //private async void TestConnectionLoop()
        //{
        //    while(!_isDisposed)
        //    {
        //        this._device.Device.State

        //        await Task.Delay(5000);
        //    }
        //}

        private async void ReconnectingLoop()
        {
            while (!IsConnected && !_isDisposed)
            {
                try
                {
                    await ConnectAsync();

                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Error while trying to connect");
                }
                await Task.Delay(5000);
            }

            _reconnectingLoopThread = null;
        }


        #region Notifications from BLE
        private void OnDownloadProgress(DeviceMessageProgress progress)
        {
            _lastReceivedMessage = DateTime.Now;
            if (_promises.TryGetValue(progress.MessageId, out var container))
            {
                container.Progress?.Report(progress);
            }
        }

        private async void OnDeviceMessageReceived(DeviceMessage deviceMessage)
        {
            try
            {
                this._lastReceivedMessage = DateTime.Now;

                if (deviceMessage.Id == "auth")
                {
                    OnDeviceAuthMessageReceived(deviceMessage);
                    return;
                }


                if (_promises.TryRemove(deviceMessage.Id, out var container))
                {
                    container.Promise.SetResult(deviceMessage);
                }

                if (deviceMessage.MessageType == DeviceMessageType.Broadcast)
                    this.OnBroadcastMessage?.Invoke(deviceMessage);

            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }

        }

        private async void  OnDeviceAuthMessageReceived(DeviceMessage message)
        {
            var splittedMessage = message.Message.Split('&').Select(a => a.Split(new[] { '=' },2)).Where(b => b.Length == 2).ToDictionary(b => b[0], b => b[1]);


            try
            {
                this._device.Version = splittedMessage["version"];
                var b64Name = splittedMessage["name"];
                if (!string.IsNullOrEmpty(b64Name))
                {
                    try
                    {
                        this._device.Name = Encoding.UTF8.GetString(System.Convert.FromBase64String(b64Name));
                    }
                    catch(Exception ex)
                    {
                        _logger.LogWarning($"Could not convert '{b64Name}' from base64 to name");
                    }

                }

                var owner = int.Parse(splittedMessage["owner"]);
                var response = splittedMessage["response"];

                if (owner > 0 && owner != _user.Id)
                {
                    await CrossBluetoothLE.Current.Adapter.DisconnectDeviceAsync(this._device.Device);
                    _logger.LogError($"Device now owned by connector (user id: {_user.Id}, owner: {owner}");
                    return;
                }
                var secret = owner > 0 ? _user.UserToken : DirectConnectionSocket._sharedSecret;
                var expectedResponse = $"{_authenticationChallenge}{secret}".ToSha256();

                if (!string.Equals(response, expectedResponse))
                {
                    await CrossBluetoothLE.Current.Adapter.DisconnectDeviceAsync(this._device.Device);
                    _logger.LogError($"Got response {response}, expected{expectedResponse}");
                    return;
                }

                var challengeResponse = $"{splittedMessage["challenge"].TrimEnd('\n')}{secret}".ToSha256();

                var cts = new CancellationTokenSource();
                cts.CancelAfter(5000);

                if (_bleReset != null)
                {
                    if (await _bleReset.WriteAsync(new byte[] { 49 }, _disconnectTokenSource.Token) != 0)
                        throw new NoConnectionException("Could not reset connection");
                }

                await _messageInterpritator.WriteAsync(this._bleTx, DeviceMessage.GetTextMessage(challengeResponse, "auth"), cts.Token);
                _sendSemaphore.Release();
                this.OnConnectionChange?.Invoke(true);
            }
            catch(Exception e)
            {
                try
                {
                    await CrossBluetoothLE.Current.Adapter.DisconnectDeviceAsync(this._device.Device);
                    _logger.LogError(e,$"Got excption during authentication");
                }
                catch (Exception) { }
            }
 

        }
        #endregion

        public void Dispose()
        {
            _isDisposed = true;
            _disconnectTokenSource.Cancel();
        }

        // More intelligent timeout
        private TimeSpan GetTimeoutBasedOnMessage(DeviceMessage message)
        {
            if (message.MessageType == DeviceMessageType.File || message.MessageType == DeviceMessageType.FileWithMd5)
                return TimeSpan.FromSeconds(15);
            if (message.Message.StartsWith("software"))
                return TimeSpan.FromSeconds(15);
            if (message.Message.StartsWith("wifi"))
                return TimeSpan.FromSeconds(15);

            return TimeSpan.FromSeconds(5);
        }
    }

    
}
