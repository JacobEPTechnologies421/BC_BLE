using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace BoatControl.Shared.Test
{
    public class WebSocketMock : WebSocket
    {
        public class WebsocketMessageQueue
        {
            public WebSocketReceiveResult Result { get; set; }

            public byte[] Bytes { get; set; }
        }

        public Queue<WebsocketMessageQueue> Messages = new Queue<WebsocketMessageQueue>();

        public override void Abort()
        {
            throw new NotImplementedException();
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (Messages.Count == 0)
                return null;


            var msg = Messages.Dequeue();
            msg.Bytes.CopyTo(buffer.Array,0);
            return Task.FromResult(msg.Result);
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var msg = new WebsocketMessageQueue()
            {
                Bytes = new byte[buffer.Count],
                Result = new WebSocketReceiveResult(buffer.Count, messageType, endOfMessage)
            };
            Buffer.BlockCopy(buffer.Array, buffer.Offset, msg.Bytes, 0, buffer.Count);
            Messages.Enqueue(msg);
            return Task.Delay(0);
        }

        public override WebSocketCloseStatus? CloseStatus { get; }
        public override string CloseStatusDescription { get; }
        public override WebSocketState State { get; }
        public override string SubProtocol { get; }
    }
}