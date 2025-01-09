using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BoatControl.Shared.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BoatControl.Shared.Test
{
    [TestClass]
    public class DeviceMessageInterpritatorTest
    {
        private Random _random = new Random((int)DateTime.Now.Ticks);
        private DeviceMessageInterpritator _interpritator = new DeviceMessageInterpritator();

        [TestMethod]
        public async Task StreamReadWriteTest()
        {
            using (var stream = new MemoryStream())
            {
                var msg = DeviceMessage.GetTextMessage("Dette er en test");
                await _interpritator.WriteAsync(stream, msg,CancellationToken.None);

                stream.Position = 0;
                var readMsg = await _interpritator.ReadAsync(stream, CancellationToken.None);

                Assert.AreEqual(msg, readMsg);
                Assert.AreEqual(stream.Length,stream.Position, "Stream was not at end?!");
            }
        }

        [TestMethod]
        public async Task StreamMultibleReadWriteTest()
        {
            using (var stream = new MemoryStream())
            {
                var messages = new List<DeviceMessage>();
                int messagesToReadWrite = 10;
                for (var i = 0; i < messagesToReadWrite; i++)
                {
                    messages.Add(DeviceMessage.GetTextMessage($"Dette {messages} er en test {messages}"));
                    await _interpritator.WriteAsync(stream, messages.Last(), CancellationToken.None);
                }
                stream.Position = 0;
                for (var i = 0; i < messagesToReadWrite; i++)
                {
                    var readMsg = await _interpritator.ReadAsync(stream, CancellationToken.None);
                    Assert.AreEqual(messages[i], readMsg);
                }

                Assert.AreEqual(stream.Length, stream.Position, "Stream was not at end?!");
            }
        }


        [TestMethod]
        public async Task StreamReadWriteFileTest()
        {
            using (var stream = new MemoryStream())
            {
                var msg = GenerateFile(1024 * 200); // 200kb
                await _interpritator.WriteAsync(stream, msg, CancellationToken.None);

                stream.Position = 0;
                var readMsg = await _interpritator.ReadAsync(stream, CancellationToken.None);

                Assert.AreEqual(msg, readMsg);
                Assert.AreEqual(stream.Length, stream.Position, "Stream was not at end?!");
            }
        }

        [TestMethod]
        public async Task StreamReadWriteFileMd5Test()
        {
            using (var stream = new MemoryStream())
            {
                var msg = GenerateFileMd5(1024 * 200); // 200kb
                await _interpritator.WriteAsync(stream, msg, CancellationToken.None);

                stream.Position = 0;
                var readMsg = await _interpritator.ReadAsync(stream, CancellationToken.None);

                Assert.AreEqual(DeviceMessageType.FileWithMd5, msg.MessageType);
                Assert.AreEqual(msg, readMsg);
                Assert.AreEqual(stream.Length, stream.Position, "Stream was not at end?!");
            }
        }

        [TestMethod]
        public async Task StreamMultibleReadWriteFileTest()
        {
            using (var stream = new MemoryStream())
            {
                var messages = new List<DeviceMessage>();
                int messagesToReadWrite = 10;
                for (var i = 0; i < messagesToReadWrite; i++)
                {
                    messages.Add(GenerateFile(1024 * 200)); // 200kb;
                    await _interpritator.WriteAsync(stream, messages.Last(), CancellationToken.None);
                }
                stream.Position = 0;
                for (var i = 0; i < messagesToReadWrite; i++)
                {
                    var readMsg = await _interpritator.ReadAsync(stream, CancellationToken.None);
                    Assert.AreEqual(messages[i], readMsg);
                }

                Assert.AreEqual(stream.Length, stream.Position, "Stream was not at end?!");
            }
        }


        [TestMethod]
        public async Task WebsocketReadWriteTest()
        {
            using (var socket = new WebSocketMock())
            {
                var msg = DeviceMessage.GetTextMessage("Dette er en test");
                await _interpritator.WriteAsync(socket, msg, CancellationToken.None);

                var readMsg = await _interpritator.ReadAsync(socket, CancellationToken.None);

                Assert.AreEqual(msg, readMsg);
                Assert.AreEqual(0, socket.Messages.Count, "Expected no more messages");
            }
        }

        [TestMethod]
        public async Task WebsocketMultibleReadWriteTest()
        {
            using (var socket = new WebSocketMock())
            {
                var messages = new List<DeviceMessage>();
                int messagesToReadWrite = 10;
                for (var i = 0; i < messagesToReadWrite; i++)
                {
                    messages.Add(DeviceMessage.GetTextMessage($"Dette {messages} er en test {messages}"));
                    await _interpritator.WriteAsync(socket, messages.Last(), CancellationToken.None);
                }
                for (var i = 0; i < messagesToReadWrite; i++)
                {
                    var readMsg = await _interpritator.ReadAsync(socket, CancellationToken.None);
                    Assert.AreEqual(messages[i], readMsg);
                }

                Assert.AreEqual(0, socket.Messages.Count, "Expected no more messages");
            }
        }


        [TestMethod]
        public async Task WebsocketReadWriteFileTest()
        {
            using (var socket = new WebSocketMock())
            {
                var msg = GenerateFile(1024 * 200); // 200kb
                await _interpritator.WriteAsync(socket, msg, CancellationToken.None);

                var readMsg = await _interpritator.ReadAsync(socket, CancellationToken.None);

                Assert.AreEqual(msg, readMsg);
                Assert.AreEqual(0, socket.Messages.Count, "Expected no more messages");
            }
        }

        [TestMethod]
        public async Task WebsocketReadWriteFileMd5Test()
        {
            using (var socket = new WebSocketMock())
            {
                var msg = GenerateFileMd5(1024 * 200); // 200kb
                await _interpritator.WriteAsync(socket, msg, CancellationToken.None);

                var readMsg = await _interpritator.ReadAsync(socket, CancellationToken.None);

                Assert.AreEqual(DeviceMessageType.FileWithMd5,msg.MessageType);
                Assert.AreEqual(msg, readMsg);
                Assert.AreEqual(0, socket.Messages.Count, "Expected no more messages");
            }
        }


        [TestMethod]
        public async Task WebsocketMultibleReadWriteFileTest()
        {
            using (var socket = new WebSocketMock())
            {
                var messages = new List<DeviceMessage>();
                int messagesToReadWrite = 10;
                for (var i = 0; i < messagesToReadWrite; i++)
                {
                    messages.Add(GenerateFile(1024 * 200)); // 200kb;
                    await _interpritator.WriteAsync(socket, messages.Last(), CancellationToken.None);
                }
                for (var i = 0; i < messagesToReadWrite; i++)
                {
                    var readMsg = await _interpritator.ReadAsync(socket,CancellationToken.None);
                    Assert.AreEqual(messages[i], readMsg);
                }

                Assert.AreEqual(0, socket.Messages.Count, "Expected no more messages");
            }
        }

        [TestMethod]
        public async Task WebsocketUploadStatusTest()
        {
            using (var socket = new WebSocketMock())
            {
                var msg = GenerateFile(1024 * 200); // 200kb

                bool initial = false;
                bool middle = false;
                bool final = false;

                await _interpritator.WriteAsync(socket, msg, CancellationToken.None, new Progress<DeviceMessageProgress>(async s =>
                {
                    Assert.AreEqual(msg.Payload.Length, s.BytesTotal);
                    if (s.BytesTransferred == 0)
                    {
                        Assert.IsFalse(initial, "Only expected 0 bytes once");
                        initial = true;
                    }
                    else if (s.BytesTransferred < s.BytesTotal)
                    {
                        middle = true;
                    }
                    else if (s.BytesTransferred == s.BytesTotal)
                    {
                        Assert.IsFalse(final, "Only expected 0 bytes once");
                        final = true;
                    }
                    else
                        Assert.Fail("Did not expect this");
                }));



                var readMsg = await _interpritator.ReadAsync(socket,CancellationToken.None);

                Assert.AreEqual(msg, readMsg);
                //Assert.IsTrue(initial);
                //Assert.IsTrue(middle);
                Assert.IsTrue(final);
                Assert.AreEqual(0, socket.Messages.Count, "Expected no more messages");
            }
        }

        [TestMethod]
        public async Task WebsocketDownloadStatusTest()
        {
            using (var socket = new WebSocketMock())
            {
                var msg = GenerateFile(1024 * 200); // 200kb

                bool initial = false;
                bool middle = false;
                bool final = false;

                await _interpritator.WriteAsync(socket, msg,CancellationToken.None);

                var readMsg = await _interpritator.ReadAsync(socket, CancellationToken.None, new Progress<DeviceMessageProgress>( s =>
                {
                    Assert.AreEqual(msg.Payload.Length, s.BytesTotal);
                    if (s.BytesTransferred == 0)
                    {
                        Assert.IsFalse(initial, "Only expected 0 bytes once");
                        initial = true;
                    }
                    else if (s.BytesTransferred < s.BytesTotal)
                    {
                        middle = true;
                    }
                    else if (s.BytesTransferred == s.BytesTotal)
                    {
                        Assert.IsFalse(final, "Only expected 0 bytes once");
                        final = true;
                    }
                    else
                        Assert.Fail("Did not expect this");

                }));

                Assert.AreEqual(msg, readMsg);
                //Assert.IsTrue(initial);
                Assert.IsTrue(middle);
                Assert.IsTrue(final);
                Assert.AreEqual(0, socket.Messages.Count, "Expected no more messages");
            }
        }

        [TestMethod]
        public async Task TcpDownloadStatusTest()
        {
            using (var stream = new MemoryStream())
            {
                var msg = GenerateFile(1024 * 200); // 200kb

                bool initial = false;
                bool middle = false;
                bool final = false;

                await _interpritator.WriteAsync(stream, msg, CancellationToken.None);

                stream.Position = 0;

                var readMsg = await _interpritator.ReadAsync(stream, CancellationToken.None, new Progress<DeviceMessageProgress>(s => {
                    Assert.AreEqual(msg.Payload.Length, s.BytesTotal);
                    if (s.BytesTransferred == 0)
                    {
                        Assert.IsFalse(initial, "Only expected 0 bytes once");
                        initial = true;
                    }
                    else if (s.BytesTransferred < s.BytesTotal)
                    {
                        middle = true;
                    }
                    else if (s.BytesTransferred == s.BytesTotal)
                    {
                        Assert.IsFalse(final, "Only expected 0 bytes once");
                        final = true;
                    }
                    else
                        Assert.Fail("Did not expect this");
                }));

                Assert.AreEqual(msg, readMsg);
                //Assert.IsTrue(initial);
                //Assert.IsTrue(middle);
                Assert.IsTrue(final);
                Assert.AreEqual(stream.Length, stream.Position, "Stream was not at end?!");
            }
        }


        [TestMethod]
        public async Task TcpUploadStatusTest()
        {
            using (var stream = new MemoryStream())
            {
                var msg = GenerateFile(1024 * 200); // 200kb

                bool initial = false;
                bool middle = false;
                bool final = false;

                await _interpritator.WriteAsync(stream, msg,CancellationToken.None, new Progress<DeviceMessageProgress>( s => 
                {
                    Assert.AreEqual(msg.Payload.Length, s.BytesTotal);
                    if (s.BytesTransferred == 0)
                    {
                        Assert.IsFalse(initial, "Only expected 0 bytes once");
                        initial = true;
                    }
                    else if (s.BytesTransferred < s.BytesTotal)
                    {
                        middle = true;
                    }
                    else if (s.BytesTransferred == s.BytesTotal)
                    {
                        Assert.IsFalse(final, "Only expected 0 bytes once");
                        final = true;
                    }
                    else
                        Assert.Fail("Did not expect this");
                }));

                stream.Position = 0;

                var readMsg = await _interpritator.ReadAsync(stream, CancellationToken.None);

                Assert.AreEqual(msg, readMsg);
                //Assert.IsTrue(initial);
                //Assert.IsTrue(middle);
                Assert.IsTrue(final);
                Assert.AreEqual(stream.Length, stream.Position, "Stream was not at end?!");
            }
        }

        [TestMethod]
        public async Task Md5GenerateFileTest()
        {
            // Generated online
            var content = "Hejsa med dig";
            var md5 = "4FAE00BA37956D8027CB47DAEE830E08".ToLower();

            var result = DeviceMessage.GetFileWithMd5Message("My file", Encoding.UTF8.GetBytes(content));
            var md5Result = result.GetPayloadMd5String();
            Assert.AreEqual(md5, md5Result, "Md5 must be the same");
        }



        private DeviceMessage GenerateFile(int size, string filename = null)
        {
            var bytes = new byte[size];
            _random.NextBytes(bytes);
            return DeviceMessage.GetFileMessage(filename ?? (DateTime.Now.Ticks + ".demo"),bytes);
        }

        private DeviceMessage GenerateFileMd5(int size, string filename = null)
        {
            var bytes = new byte[size];
            _random.NextBytes(bytes);
            return DeviceMessage.GetFileWithMd5Message(filename ?? (DateTime.Now.Ticks + ".demo"), bytes);
        }
    }
}
