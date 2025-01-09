using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoatControl.Communication.Models;
using BoatControl.Shared.Messaging;
using BoatControl.Shared.UserManagement;
using Microsoft.Extensions.Logging;

namespace BoatControl.Communication.Connections.Shared
{




    internal class ReconnectingSocketWrapper : IDisposable, IDeviceConnection
    {
        private static readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);
        // Only pings when there is no communication (progress or received messages)
        private static readonly TimeSpan _pingWhenNoCommunicationAfter = TimeSpan.FromSeconds(5); 

                public event SocketConnectionChangeDelegate OnConnectionChange;
        public event OnBroadcastMessageDelegate OnBroadcastMessage;


        private readonly SemaphoreSlim _sendSemaphore;
        private readonly ILogger<ReconnectingSocketWrapper> _logger;
        private readonly AuthenticationUser _user;
        private readonly DeviceInfo _deviceInfo;
        private readonly string _connectionName;
        private readonly Func<IDeviceConnectionSocket> _deviceConnectionSocketFactory;
        private readonly IProgress<DeviceMessageProgress> _progressDistributor;
        private readonly Thread _mainLoop;
        private Thread _keepAliveLoop;

        // Contains all the threads regarding maintaining the connection
        // If any of them disconnects, the whole process must be recycled

        private DateTime _lastReceivedMessage;


        private bool _connect = true;
        private CancellationTokenSource _disconnectTokenSource = new CancellationTokenSource();
        private IDeviceConnectionSocket _deviceConnectionSocket;
        private ConcurrentDictionary<string, DeviceMessageContainer> _promises = new ConcurrentDictionary<string, DeviceMessageContainer>();
        private IDeviceConnectionSocket _existingConnection;

        public ReconnectingSocketWrapper(
            ILogger<ReconnectingSocketWrapper> logger,
            AuthenticationUser user,
            DeviceInfo deviceInfo,
            SemaphoreSlim sendSemaphore,
            string connectionName,
            Func<IDeviceConnectionSocket> deviceConnectionSocketFactory,
            IDeviceConnectionSocket existingConnection = null)
        {
            this._logger = logger;
            _user = user;
            _sendSemaphore = sendSemaphore;
            _deviceInfo = deviceInfo;
            _connectionName = connectionName;
            _deviceConnectionSocketFactory = deviceConnectionSocketFactory;
            _existingConnection = existingConnection;
            _mainLoop = new Thread(async () => await ConnectAndHandle());
            _progressDistributor = BuildProgressDistributor();
            IsConnected = existingConnection != null;

        }


        public void Start()
        {
            _mainLoop.Start();

        }

        public bool IsConnected { get; set; }


        private async Task ConnectAndHandle()
        {
            while (_connect)
            {
                try
                {
                    CleanupOldPromises();
                    if (await CreateConnection())
                    {
                        _logger.LogInformation($"{GetLogPrefix()} Connected");
                        SetConnected(true);
                        _keepAliveLoop = new Thread(async () => await KeepAliveHandle());
                        _keepAliveLoop.Start();
                        await ReadLoop1();

                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"{GetLogPrefix()} Error in connect handle");
                }
                finally
                {
                    SetConnected(false);
                }

                if (_connect)
                {
                    _logger.LogDebug($"{GetLogPrefix()} Reconnecting delay for {_reconnectDelay:g}");

                    Thread.Sleep(_reconnectDelay);
                }
            }
        }

        private void SetConnected(bool connected)
        {
            if (IsConnected == connected) return; // No reason to keep reporting
            _logger.LogDebug($"{GetLogPrefix()} {(connected ? "Connected" : "Disconnected")}");
            IsConnected = connected;
            OnConnectionChange?.Invoke(connected);
            
        }


        private void CleanupOldPromises()
        {
            _logger.LogDebug($"{GetLogPrefix()} Cleaning up old promises");
            _keepAliveLoop?.Abort();

            Parallel.Invoke(_promises.Values.Select(a => (Action)(() =>
            {
                new Thread(() =>
                {
                    try
                    {
                        a.Promise.SetCanceled();
                    }
                    catch
                    {
                    }
                }).Start();
            })).ToArray());
            _disconnectTokenSource?.Dispose();
            _disconnectTokenSource = new CancellationTokenSource();
            _promises.Clear();

        }

        private async Task<bool> CreateConnection()
        {
            // Connect
            if (_existingConnection != null)
            {
                _deviceConnectionSocket = _existingConnection;
                _existingConnection = null; // To avoid hitting it again
            }
            else
            {
                _deviceConnectionSocket = _deviceConnectionSocketFactory();
                _logger.LogDebug($"{GetLogPrefix()} Connecting");
                if (!await _deviceConnectionSocket.ConnectAsync(_user, new CancellationTokenSource(5000).Token)) // .ConfigureAwait(false))
                    return false;

            }

            return true;
        }

        private async Task ReadLoop1()
        {
            while (true)
            {
                try
                {
                    var msg = await _deviceConnectionSocket.ReadMessageAsync(_disconnectTokenSource.Token, _progressDistributor); //ConfigureAwait(false);
                    _lastReceivedMessage = DateTime.Now;
                    DeviceMessageContainer promise;
                    switch (msg.MessageType)
                    {
                        case DeviceMessageType.Progress:
                            if (_promises.TryGetValue(msg.Id, out promise))
                            {
                                var split = msg.Message.Split(' ');
                                var bytesTransferred = long.Parse(split[0]);
                                var bytesTotal = long.Parse(split[1]);
                                _progressDistributor.Report(new DeviceMessageProgress(msg.Id, bytesTransferred, bytesTotal));
                                //promise.Progress?.Report();
                            }
                            break;
                        case DeviceMessageType.Broadcast:
                            OnBroadcastMessage?.Invoke(msg);
                            break;
                        default:
                            if (_promises.TryRemove(msg.Id, out promise))
                                new Thread(() =>
                                {
                                    try
                                    {

                                        promise.Promise.SetResult(msg);
                                    }
                                    catch (Exception e)
                                    {
                                        _logger.LogError(e, $"{GetLogPrefix()} Promise exception issues");
                                    }
                                }).Start();
                            break;

                    }


                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, $"{GetLogPrefix()} Failed reading");
                    return;
                }
            }
        }



        // More intelligent timeout
        private TimeSpan GetTimeoutBasedOnMessage(DeviceMessage message)
        {
            if (message.Message.StartsWith("file") || message.MessageType == DeviceMessageType.File || message.MessageType == DeviceMessageType.FileWithMd5)
                return TimeSpan.FromSeconds(30);
            if (message.Message.StartsWith("software"))
                return TimeSpan.FromSeconds(15);
            if (message.Message.StartsWith("wifi"))
                return TimeSpan.FromSeconds(15);

            return TimeSpan.FromSeconds(5);
        }

        private async Task KeepAliveHandle()
        {
            var token = _disconnectTokenSource.Token;
            while (true)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    // Skip if we are sending
                    if (_sendSemaphore.CurrentCount == 0)
                    {
                        _lastReceivedMessage = DateTime.Now;
                        await Task.Delay(5000, token);
                        continue;
                    }

                    // Min. 5 sec without any received message
                    if ((DateTime.Now - _lastReceivedMessage).TotalSeconds > 5)
                    {


                        // check if any messages timed out
                        var lastHeardFromServer = _promises.Values.OrderByDescending(a => a.LastUpdated).FirstOrDefault();

                        var messageWithTimeout = _promises.Values.FirstOrDefault(a => (DateTime.Now - a.LastUpdated) > GetTimeoutBasedOnMessage(a.Message));
                        if(messageWithTimeout != null)
                        {
                            var lastHeard = _lastReceivedMessage > lastHeardFromServer.LastUpdated ? _lastReceivedMessage : lastHeardFromServer.LastUpdated;
                            var timeoutBasedOnMessage = GetTimeoutBasedOnMessage(messageWithTimeout.Message);
                            if (messageWithTimeout != null && (DateTime.Now - lastHeard) > timeoutBasedOnMessage)
                            {
                                _logger.LogWarning($"{GetLogPrefix()} No response for message '{messageWithTimeout.Message.Message}', reconnecting (timeout: {timeoutBasedOnMessage.TotalSeconds:N2} s, last heard: {lastHeard:s})");
                                Reconnect();
                                return;
                            }
                        }

                        // Send ping when no response
                        if (_promises.IsEmpty && (DateTime.Now - _lastReceivedMessage) > _pingWhenNoCommunicationAfter)
                        {
                            _logger.LogDebug($"{GetLogPrefix()} Sending ping");

                            // DO NOT USE await, as Cloud will be stuck forever
                            // The message is delivered to cloud but no response ever comes back, thus it will hang
                            // As this method also handles unhandled responses
                            // Because this call is not awaited, execution of the current method continues before the call is completed
                            try
                            {

                                var pingResponse = SendMessageAsync(DeviceMessage.GetTextMessage("ping"), null, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
                                var delayResponse = Task.Delay(5000);
                                if (await Task.WhenAny(pingResponse, delayResponse) == delayResponse)
                                {
                                    _logger.LogWarning($"{GetLogPrefix()} Keep alive error (no response from ping)");
                                    Reconnect();
                                }

                            }
                            catch(Exception e) {
                                if (_disconnectTokenSource.IsCancellationRequested)
                                    return;
                                else
                                    Reconnect();
                                return;
                            }
                            finally
                            {
                                //_sendSemaphore.Release();
                            }
                        }
                    }
                    

                    await Task.Delay(5000, token); //.ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, $"{GetLogPrefix()} Keep alive error");
                    if (_disconnectTokenSource.IsCancellationRequested)
                        return;
                    else
                        Reconnect();
                    return;
                }
            }
        }




        private async Task<DeviceMessage> SendMessageAsync(DeviceMessage message, IProgress<DeviceMessageProgressWithDirection> progress, CancellationToken cancellationToken)
        {
            //Thread.CurrentThread.Name = $"msg:{message.Message}";
            var promise = new TaskCompletionSource<DeviceMessage>();
            try
            {
                //await _sendSemaphore.WaitAsync(cancellationToken);
                DeviceMessageContainer container;
                _promises[message.Id] = container = new DeviceMessageContainer(message, promise, new Progress<DeviceMessageProgress>(p =>
                {
                    _lastReceivedMessage = DateTime.Now;
                    _logger.LogDebug($"{GetLogPrefix()} Progress: {((float)p.BytesTransferred / (float)p.BytesTotal * 100f):N2} %");
                    progress?.Report(new DeviceMessageProgressWithDirection(p, EnumSendProgressDirection.Receiving));
                }));
                await _deviceConnectionSocket.SendMessageAsync(message, cancellationToken, new Progress<DeviceMessageProgress>(p =>
                {
                    _lastReceivedMessage = DateTime.Now;
                    container.LastUpdated = DateTime.Now;
                    _logger.LogDebug($"{GetLogPrefix()} Progress: {((float)p.BytesTransferred / (float)p.BytesTotal * 100f):N2} %");
                    progress?.Report(new DeviceMessageProgressWithDirection(p, EnumSendProgressDirection.Sending));
                })); //.ConfigureAwait(false);
            }
            catch(Exception e)
            {
                _logger.LogError(e, $"{GetLogPrefix()} Error trying to sending message '{message.Message}'");
                _promises.TryRemove(message.Id, out var removed);
                promise.TrySetException(e);
                Reconnect();
            }
            finally
            {
                //if(_sendSemaphore.CurrentCount == 0)  
                //   _sendSemaphore.Release();
            }
            return await promise.Task; //.ConfigureAwait(false);

        }

        public async Task<DeviceMessage> SendMessageAsync(DeviceMessage message, IProgress<DeviceMessageProgressWithDirection> progress)
        {
            return await SendMessageAsync(message, progress, _disconnectTokenSource.Token);
        }

        private void Reconnect()
        {
            try
            {
                _logger.LogDebug($"{GetLogPrefix()} Reconnecting: Cancelling all tokens");
                _disconnectTokenSource.Cancel();
                _logger.LogDebug($"{GetLogPrefix()} Reconnecting: tokens cancelled");

            }
            catch (Exception)
            {

            }
        }

        public void Dispose()
        {
            _connect = false;
            _mainLoop.Abort();
            _deviceConnectionSocket.Dispose();
        }

        private IProgress<DeviceMessageProgress> BuildProgressDistributor()
        {
            return new Progress<DeviceMessageProgress>(p =>
            {
                _lastReceivedMessage = DateTime.Now;
                if (_promises.TryGetValue(p.MessageId, out var container))
                {
                    container.Progress?.Report(p);
                }
            });
        }

        private string GetLogPrefix()
        {
            return $"[{_deviceInfo.Number} ({_connectionName})]";
        }
    }
}