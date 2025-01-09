using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BoatControl.Communication.Caching;
using BoatControl.Communication.Connections.Ble;
using BoatControl.Communication.Connections.Ble.Searching;
using BoatControl.Communication.Connections.Cloud;
using BoatControl.Communication.Connections.Shared;
using BoatControl.Communication.Connections.Shared.Searching;
using BoatControl.Communication.Connections.Tcp;
using BoatControl.Communication.Helpers;
using BoatControl.Communication.Models;
using BoatControl.Communication.Storage;
using BoatControl.Logic;
using BoatControl.Shared.Messaging;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;

namespace BoatControl.Communication.Connections
{
    internal class DeviceConnectionManager : IDeviceConnectionManager
    {
        private bool _disposed;
        private readonly AuthenticationUser _user;
        private readonly IPersistedStorage _persistedStorage;
        private readonly ILogger<DeviceConnectionManager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
        private readonly FileCaching _caching;
        private FixedSizeQueue _fixedSizeQueue = new FixedSizeQueue(50);

        private IDeviceConnection _directConnection = null;
        private ReconnectingSocketWrapper _cloudConnection = null;

        private BleConnection _bleConnection = null;

        private DateTime? _lastCheckedForCoreDump;

        public void Dispose()
        {
            _disposed = true;
            _directConnection?.Dispose();
            _cloudConnection?.Dispose();
        }

        public DeviceInfo DeviceInfo { get; }
        public string HostOnLocalNetwork { get; internal set; }
        public bool IsConnected => IsConnectedCloud || IsConnectedLocal || IsConnectedBle;
        public bool IsConnectedCloud => _cloudConnection?.IsConnected ?? false;
        public bool IsConnectedLocal => _directConnection?.IsConnected ?? false;
        public bool? IsPaired { get; internal set; }
        public DateTime? LastSeenInCloud { get; internal set; }
        public bool IsDiscoveredBle => _bleConnection?.IsDiscovered ?? false;
        public bool IsConnectedBle => _bleConnection?.IsConnected ?? false;

        public event ConnectionChangeDelegate OnConnectionChange;
        public event DeviceMessageDelegate OnDeviceMessage;

        public async Task<bool> Pair()
        {
            try
            {
                if(!string.IsNullOrEmpty(HostOnLocalNetwork) && _directConnection == null)
                {
                    _directConnection = BuildReconnectingSocket(HostOnLocalNetwork, () => new DirectConnectionSocket(
                        _serviceProvider.GetRequiredService<ILogger<DirectConnectionSocket>>(), _user, HostOnLocalNetwork));
                }
                else if(IsDiscoveredBle && !IsConnectedBle)
                {
                    await ConnectBle();
                }

                if (!IsConnected)
                {
                    _logger.LogError("Could not connect while trying to pair");
                    return false;
                }
                if(await _bleConnection.Pair())
                {
                    IsPaired = true;
                    OnConnectionChange?.Invoke(this);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error trying to pair");
                return false;
            }
        }

        public async Task<DeviceMessage> SendAsync(DeviceMessage message, IProgress<DeviceMessageProgressWithDirection> progress = null)
        {
            try
            {
                // For logging
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                TimeSpan semaphoreWait;
                var connection = "caching";

                await _sendSemaphore.WaitAsync();//.ConfigureAwait(false);
                semaphoreWait = stopwatch.Elapsed;


                if (!IsConnected)
                    throw new NoConnectionException();


                _fixedSizeQueue.Enqueue(message.Id);
                var result =  await _caching.Wrap(message, progress, async (m, p) =>
                {

                    if (_directConnection?.IsConnected ?? false)
                    {
                        connection = "tcp";
                        try
                        {
                            return await _directConnection.SendMessageAsync(m, p); //.ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "Error while trying to send message directly");
                            if (!(_cloudConnection?.IsConnected ?? false) && !(_bleConnection?.IsConnected ?? false))
                                throw;
                        }
                    }

                    if (_bleConnection?.IsConnected ?? false)
                    {
                        connection = "ble";
                        try
                        {
                            return await _bleConnection.SendMessageAsync(m, p); //.ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "Error while trying to send message over BLE");
                            if (!(_cloudConnection?.IsConnected ?? false))
                                throw;
                        }
                    }


                    if (_cloudConnection?.IsConnected ?? false)
                    {
                        connection = "cloud";
                        return await _cloudConnection.SendMessageAsync(m, p); //.ConfigureAwait(false);
                    }

                    throw new NoConnectionException();
                });

                _logger.LogInformation(
                    "Message: {message}, MessageType: {messageType}, Response: {response}, ResponseType: {responseType}, Duration: {duration}ms, DurationSemaphore: {durationSemaphore}ms, Connection: {connection}",
                    message.Message,
                    message.MessageType.ToString("g"),
                    result.Message,
                    result.MessageType.ToString("g"),
                    stopwatch.ElapsedMilliseconds,
                    semaphoreWait.TotalMilliseconds,
                    connection
                );


                return result;

            }
            finally
            {
                _sendSemaphore.Release();
            }
        }



        #region SetFound

        private void SetFound(AFoundDevice foundDevice)
        {
            if (!string.IsNullOrEmpty(foundDevice.Version))
            {
                DeviceInfo.Version = foundDevice.Version;
            }
            if (!string.IsNullOrEmpty(foundDevice.Name))
                DeviceInfo.Name = foundDevice.Name;

        }

        public void SetFoundInCloud(FoundCloudDevice foundCloudDevice)
        {
            SetFound(foundCloudDevice);
            LastSeenInCloud = foundCloudDevice.LastSeenInCloud;
            if (foundCloudDevice.OnlineInCloud && _cloudConnection == null)
            {
                IsPaired = true;
                _cloudConnection = BuildReconnectingSocket("Cloud", () => new CloudConnectionSocket(_serviceProvider.GetRequiredService<ILogger<CloudConnectionSocket>>(), foundCloudDevice));
            }
        }

        public async void SetFoundLocally(FoundLocalDevice device)
        {
            SetFound(device);

            if (_directConnection == null || !string.Equals(HostOnLocalNetwork, device.HostOnLocalNetwork))
            {
                HostOnLocalNetwork = device.HostOnLocalNetwork;
                try
                {
                    var conn = new DirectConnectionSocket(
                        _serviceProvider.GetRequiredService<ILogger<DirectConnectionSocket>>(),
                        _user, device.HostOnLocalNetwork
                    );
                    var tokenCancelSource = new CancellationTokenSource(3000);
                    var peekResult = await conn.Peek(tokenCancelSource.Token);
                    if (peekResult == null)  {
                        _logger.LogWarning($"[{device.HostOnLocalNetwork}] Peek result was null");
                        return; // timeout or error somehow
                    }
                    this.DeviceInfo.Version = peekResult.DeviceVersion;
                    if (!string.IsNullOrEmpty(peekResult.Name))
                        this.DeviceInfo.Name = peekResult.Name;

                    if (peekResult.Owner == _user.Id)
                    {
                        this.IsPaired = true;
                        _directConnection?.Dispose();
                        _directConnection = BuildReconnectingSocket(HostOnLocalNetwork, () => new DirectConnectionSocket(
                            _serviceProvider.GetRequiredService<ILogger<DirectConnectionSocket>>(), _user, device.HostOnLocalNetwork));

                    }
                    else if(peekResult.Owner == 0)
                    {
                        IsPaired = false;
                        this.OnConnectionChange?.Invoke(this);
                    } 
                }
                catch(Exception e)
                {
                    _logger.LogError(e, e.Message);
                }
            } 
        }

        public async void SetFoundBle(FoundBleDevice device)
        {
            SetFound(device);

            IsPaired = device.IsPaired;
            if (_bleConnection == null)
            {
                _bleConnection = new BleConnection(this._serviceProvider.GetRequiredService<ILogger<BleConnection>>(), this._user, device, _sendSemaphore);
                _bleConnection.OnConnectionChange += connected =>
                {
                    //if (connected && (IsPaired ?? false)) Task.Run(CheckForCrashReport);
                    OnConnectionChange?.Invoke(this);
                };
                _bleConnection.OnBroadcastMessage += m =>
                {
                    // Do not broadcast messages we sent ourselves
                    if (!_fixedSizeQueue.Contains(m.Id))
                    {
                        _fixedSizeQueue.Enqueue(m.Id); // To avoid two broadcast messages
                        OnDeviceMessage?.Invoke(this, m);
                    }
                };
                _bleConnection.Start();
                OnConnectionChange?.Invoke(this);

                if (device.IsPaired || device.IsUnregistered())
                    await _bleConnection.ConnectAsync();

            } else if (!_bleConnection.IsConnected && device.IsPaired || device.IsUnregistered())
            {
                await _bleConnection.ConnectAsync();
            }
        }
        #endregion

        public async Task ConnectBle()
        {
            if (_bleConnection == null) throw new NoConnectionException("No connection found");
            await _bleConnection.ConnectAsync();
        }

        public DeviceConnectionManager(ILogger<DeviceConnectionManager> logger, IServiceProvider serviceProvider, AuthenticationUser user, DeviceInfo device, IPersistedStorage persistedStorage)
        {
            _logger = logger;
            this._serviceProvider = serviceProvider;
            _user = user;
            _persistedStorage = persistedStorage;
            DeviceInfo = device;
            _caching = new FileCaching(serviceProvider.GetRequiredService<ILogger<FileCaching>>(), this, persistedStorage);

        }

        private ReconnectingSocketWrapper BuildReconnectingSocket(string connectionName, Func<IDeviceConnectionSocket> buildConnection, IDeviceConnectionSocket existingconnection = null)
        {
            var conn = new ReconnectingSocketWrapper(
                _serviceProvider.GetRequiredService<ILogger<ReconnectingSocketWrapper>>(),
                _user,
                this.DeviceInfo,
                _sendSemaphore,
                connectionName,
                buildConnection,
                existingconnection
            );
            conn.OnConnectionChange += connected =>
            {
                //if (connected && (IsPaired ?? false)) Task.Run(CheckForCrashReport);
                OnConnectionChange?.Invoke(this);
            };
            conn.OnBroadcastMessage += m =>
            {
                // Do not broadcast messages we sent ourselves
                if (!_fixedSizeQueue.Contains(m.Id))
                {
                    _fixedSizeQueue.Enqueue(m.Id); // To avoid two broadcast messages
                    OnDeviceMessage?.Invoke(this, m);
                }
            };
            conn.Start();
            return conn;
        }

  
        //private async Task CheckForCrashReport()
        //{
        //    await Task.Delay(TimeSpan.FromMinutes(3)); // Wait 3 minutes to avoid bad experience

        //    // Max check every minute
        //    if (_lastCheckedForCoreDump.HasValue && (DateTime.Now - _lastCheckedForCoreDump.Value).TotalMinutes < 1) return;
        //    _lastCheckedForCoreDump = DateTime.Now;

        //    var magicNumber = await SendAsync(DeviceMessage.GetTextMessage("coredump magicnumber"));
        //    if (magicNumber.Message.StartsWith("error"))
        //        return;

        //    var persistedMagicNumber = _persistedStorage.GetValueOrDefault<string>($"magicnumber:{this.DeviceInfo.Number}", null);
        //    if (!string.Equals(magicNumber.Message, persistedMagicNumber))
        //    {
        //        try
        //        {
        //            var dump = await SendAsync(DeviceMessage.GetTextMessage("file download /coredump.bin"), new Progress<DeviceMessageProgressWithDirection>(p =>
        //            {
        //                // DO nothing
        //            }));
        //            if (dump.MessageType == DeviceMessageType.File || dump.MessageType == DeviceMessageType.FileWithMd5)
        //            {
        //                var logObj = new
        //                {
        //                    deviceNumber = this.DeviceInfo.Number,
        //                    deviceVersion = this.DeviceInfo.Version,
        //                    deviceName = this.DeviceInfo.Name,
        //                    coredump = Convert.ToBase64String(dump.Payload),
        //                    coredumpMagicNumber = magicNumber.Message
        //                };
        //                var success = await ExternalLogger.LogAsync("deviceconnection", "curedump", LogLevel.Error, logObj);
        //                if (success)
        //                    _persistedStorage.AddOrUpdateValue($"magicnumber:{this.DeviceInfo.Number}", magicNumber.Message);
        //            }

        //        }
        //        catch (Exception e)
        //        {
        //            _logger.LogError(e, "Could not download crash dump");
        //        }
        //    }
        //}
    }
}
