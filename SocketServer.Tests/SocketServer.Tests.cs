using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Owin.Hosting;
using System.Diagnostics;

using WebSocketServer;
using System.Threading;

using System.Net.WebSockets;
using System.Threading.Tasks;
using WebSocketServer.Utils;
using System.Collections.Concurrent;

namespace WebSocketServer.Tests
{
    [TestClass]
    public class SocketServerTests
    {

        #region context management

        private TestContext m_testContext;

        public TestContext TestContext
        {
            get
            {
                return m_testContext;
            }
            set
            {
                m_testContext = value;
            }
        }
        #endregion


        private static IDisposable webServer = null;
        private readonly static string _serverAddress = "http://localhost:9001/websocket";
        private readonly static string _serverAddressPublish = "ws://localhost:9001/websocket";


        private static TestWebSocketSessionFactory _echoFactory;

        private static IDisposable startServer()
        {
            Trace.WriteLine(string.Format("Web server is starting on {0}", _serverAddress));

            _echoFactory = new TestWebSocketSessionFactory();

            IDisposable server = WebApp.Start(_serverAddress, app =>
                {
                    app.MapWebSocket<WebSocketSessionManager<TestWebSocketSession>, TestWebSocketSession>(_echoFactory);
                }
            );

            return server;
        }





        [ClassInitialize()]
        public static void init(TestContext testContext)
        {
            webServer = startServer();



        }


        [ClassCleanup()]
        public static void end()
        {

            if (null != webServer)
                webServer.Dispose();

        }




        [TestMethod]
        [TestCategory("SocketServer.Basic")]
        public void BasicListen()
        {
            Thread.Sleep(100);
        }

        [TestMethod]
        [TestCategory("SocketServer.Basic")]
        public async Task MessageVerification()
        {
            var snd = "Hello, World!";
            var rcv = string.Empty;
            _echoFactory.OnReceiveAsyncCallBack = (session, buffer, tuple) =>
            {
                var messageLength = tuple.Item3;
                var actual = buffer.Actualize(messageLength);
                

                rcv = Encoding.UTF8.GetString(actual);
                Assert.AreEqual(snd, rcv);
                Trace.WriteLine(string.Format("Message:{0} received on session Id{1}", rcv, session.SessionId), "info");
                return Task.FromResult(0);
            };


            ClientWebSocket clientWSocket = new ClientWebSocket();
            await clientWSocket.ConnectAsync(new Uri(_serverAddressPublish), CancellationToken.None);
            await clientWSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(snd)),
                                          WebSocketMessageType.Text,
                                          true,
                                          CancellationToken.None);



            await Task.Delay(100); // = console.read();
        }




        [TestMethod]
        [TestCategory("SocketServer.Basic")]
        public async Task Echo()
        {
            var snd = "Hello, World!";
            var rcv = string.Empty;

            _echoFactory.OnReceiveAsyncCallBack = (session, buffer, tuple) =>
            {
                var messageLength = tuple.Item3;
                var actual = buffer.Actualize(messageLength);
                

                rcv = Encoding.UTF8.GetString(actual);
                Assert.AreEqual(snd, rcv);

                // send it back
                session.Send(rcv);
                return Task.FromResult(0);
            };


            ClientWebSocket clientWSocket = new ClientWebSocket();
            await clientWSocket.ConnectAsync(new Uri(_serverAddressPublish), CancellationToken.None);

            // send 
            await clientWSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(snd)),
                                          WebSocketMessageType.Text,
                                          true,
                                          CancellationToken.None);



            await Task.Delay(100);
            var socketRecieved = await ReceiveFromSocket(clientWSocket);

            Assert.AreEqual(snd, socketRecieved);
            await clientWSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,  "close",CancellationToken.None);
        }


        [TestMethod]
        [TestCategory("SocketServer.Advanced")]
        public async Task BroadcastViaSend()
        {


            var numOfSessions = 100;
            var snd = "Hello, World!";
            var receiveTask = new List<Task<String>>();

            _echoFactory.OnReceiveAsyncCallBack = (session, buffer, tuple) =>
            {
                var messageLength = tuple.Item3;
                var actual = buffer.Actualize(messageLength);
                
                var rcv = Encoding.UTF8.GetString(actual);
                Assert.AreEqual(snd, rcv);

                // send it back
                session.Send(rcv);
                return Task.FromResult(0);
            };


            var sessions = new ClientWebSocket[numOfSessions];
            for (var i = 0; i < numOfSessions; i++)
            {
                sessions[i] = new ClientWebSocket();
                await sessions[i].ConnectAsync(new Uri(_serverAddressPublish), CancellationToken.None);
            }

            await _echoFactory.PostToAll(snd);

            for (var i = 0; i < numOfSessions; i++)
                receiveTask.Add(ReceiveFromSocket(sessions[i]));



            await Task.WhenAll(receiveTask);

            foreach (var task in receiveTask)
                Assert.AreEqual(snd, task.Result);


            for (var i = 0; i < numOfSessions; i++)
                await sessions[i].CloseAsync(WebSocketCloseStatus.NormalClosure, "close..", CancellationToken.None);
        }




        [TestMethod]
        [TestCategory("SocketServer.Advanced")]
        public async Task BroadcastViaPost()
        {


            var numOfSessions = 100;
            var snd = "Hello, World!";
            var receiveTask = new List<Task<String>>();

            _echoFactory.OnReceiveAsyncCallBack = (session, buffer, tuple) =>
            {
                var messageLength = tuple.Item3;
                var actual = buffer.Actualize(messageLength);

                var rcv = Encoding.UTF8.GetString(actual);
                Assert.AreEqual(snd, rcv);

                // send it back
                session.Post(rcv);
                return Task.FromResult(0);
            };


            var sessions = new ClientWebSocket[numOfSessions];
            for (var i = 0; i < numOfSessions; i++)
            {
                sessions[i] = new ClientWebSocket();
                await sessions[i].ConnectAsync(new Uri(_serverAddressPublish), CancellationToken.None);
            }

            await _echoFactory.PostToAll(snd);

            for (var i = 0; i < numOfSessions; i++)
                receiveTask.Add(ReceiveFromSocket(sessions[i]));



            await Task.WhenAll(receiveTask);

            foreach (var task in receiveTask)
                Assert.AreEqual(snd, task.Result);


            for (var i = 0; i < numOfSessions; i++) 
              await sessions[i].CloseAsync(WebSocketCloseStatus.NormalClosure, "close..", CancellationToken.None);
        }





        [TestMethod]
        [TestCategory("SocketServer.Advanced")]
        public async Task HighFrequencyMessaging_LONGRUNNING()
        {


            var numOfSessions = 100;
            var numOfMessages = 10000;

            var receiveTask = new List<Task<String>>();
            var sendTasks = new List<Task>();

            var clients = new ConcurrentDictionary<string, ClientWebSocket>();
            _echoFactory.OnReceiveAsyncCallBack = (session, buffer, tuple) =>
            {
                var messageLength = tuple.Item3;
                var actual = buffer.Actualize(messageLength);

                var rcv = Encoding.UTF8.GetString(actual);

                // send it back
                session.Post(rcv);
                return Task.FromResult(0);
            };

            Trace.AutoFlush = true;


            int j = 0;
            do
            {

                       var t = Task.Run(async () =>
                                {
                                    var client = new ClientWebSocket();
                                    await client.ConnectAsync(new Uri(_serverAddressPublish), CancellationToken.None);
                                    int k = 0;
                                        while (k < numOfMessages / numOfSessions)
                                        {
                                              Trace.WriteLine(string.Format("Message on client {0}", j));
                                              await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(j.ToString())),
                                                                                            WebSocketMessageType.Text,
                                                                                            true,
                                                                                            CancellationToken.None);
                                               k++;                                                                   
                                        }
                                        // this exception should never be thrown
                                    clients.AddOrUpdate(Guid.NewGuid().ToString(), client, (key, c) => { throw new Exception("duplicate client!"); });
                                });

                    sendTasks.Add(t);
                j++;
            } while (j < numOfSessions - 1);

            await Task.WhenAll(sendTasks);

            foreach (var client in clients.Values)
                receiveTask.Add(ReceiveFromSocket(client));

            


            await Task.WhenAll(receiveTask);



            /*
            uncomment this if you plan to drain the socket befoire closing., 
            foreach (var client in clients.Values) // be kind rewind
                await client.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "close..", CancellationToken.None);

    */
        }








        private async Task<string> ReceiveFromSocket(ClientWebSocket theSocket)
        {

            // receive 
            
            var arraySegment = new ArraySegment<byte>(new Byte[1024 * 256]);
            var res = await theSocket.ReceiveAsync(arraySegment, CancellationToken.None);

            return Encoding.UTF8.GetString(arraySegment.Actualize(res.Count));
        }



        [TestMethod]
        [TestCategory("SocketServer.Common")]
        public async Task ServerSideCloseCounter()
        {
            var numOfSessions = 100;

            var sessions = new ClientWebSocket[numOfSessions];
            for (var i = 0; i < numOfSessions; i++)
            {
                sessions[i] = new ClientWebSocket();
                await sessions[i].ConnectAsync(new Uri(_serverAddressPublish), CancellationToken.None);
            }

            Assert.AreEqual(numOfSessions, _echoFactory.Count);
            await _echoFactory.CloseAll();
            Assert.AreEqual(0, _echoFactory.Count); // we closed everything, we should be clean
        }



        [TestMethod]
        [TestCategory("SocketServer.Common")]
        public async Task ClientSideCloseCounter()
        {
            var numOfSessions = 100;

            var sessions = new ClientWebSocket[numOfSessions];
            for (var i = 0; i < numOfSessions; i++)
            {
                sessions[i] = new ClientWebSocket();
                await sessions[i].ConnectAsync(new Uri(_serverAddressPublish), CancellationToken.None);
            }

            Assert.AreEqual(numOfSessions, _echoFactory.Count);
            for (var i = 0; i < numOfSessions; i++)
               await sessions[i].CloseAsync(WebSocketCloseStatus.NormalClosure, 
                                            "closing..", 
                                            CancellationToken.None);
            
            Assert.AreEqual(0, _echoFactory.Count); // we closed everything, we should be clean
        }



    }
}
