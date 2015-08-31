using Microsoft.ServiceFabric.Services;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketServer.ServiceFabric.Clients;

namespace TestClient
{
    class Program
    {
        static readonly string FabricServiceName = "fabric:/TestApp/TestStatefulSvc";
        // basically my sockets are mapped to listening address + this
        // a socket is also mapped to the root address (as in publishing address as is)
         static readonly string[] socketPath = new string[] { "", "customer", "order" };


        // if i have one socket listening on the address i can bypass all this and just connect
        // directly to endpoint address. 
        static readonly object consoleLock = new object();
        static void Main(string[] args)
        {
            try {

                //  UseStandardSocket().Wait();
                UseSFClientInterfaces().Wait();

            }
            catch (AggregateException Ex)
            {
                Console.WriteLine("error : " + Ex.InnerException.Message);
            }

            
            Console.Read();
        }

        #region Using Service Fabric Client Interfaces
        static async Task UseSFClientInterfaces()
        {
            var factory = new ServiceFabricWebSocketClientFactory();

            ServicePartitionClient <ServiceFabricWebSocketClient> partitionClient = new ServicePartitionClient<ServiceFabricWebSocketClient>(factory, new Uri(FabricServiceName));
            var Tasks = new List<Task>();
            await partitionClient.InvokeWithRetryAsync(
                async (client) =>
                {
                    

                    // in here business as usual
                    // get a socket 
                    var socket = await client.GetWebSocketAsync(); // socket at the base address
                    Tasks.Add(useSocket(socket));

                    var anotherSocket = await client.GetWebSocketAsync("customer"); // get socket at baseaddress/customer
                    Tasks.Add(useSocket(anotherSocket));

                    var yetAnotherSocket = await client.GetWebSocketAsync("order"); // get socket at baseaddress/order
                    Tasks.Add(useSocket(yetAnotherSocket));


                    // i can also close the socket here 
                    //client.AbortAll();
                });


            await Task.WhenAll(Tasks);

           // now when the partition goes out of scope, evantually the dispose on client will be 
           // called awhich will take care of closing the client


        }



        static async Task useSocket(ClientWebSocket clientWSocket)
        {


            var upStreamMessageCount = 10;
            var delayMs = 5 * 1000; // every 5 sec
            var rcvWaitCount = 10;

            var UpstreamMessage = string.Format("Hello Server On {0}", socketPath);
            var sendTask = Task.Run(async () =>
            {
                for (var i = 1; i <= upStreamMessageCount; i++)
                {
                    lock (consoleLock)
                        Console.WriteLine(string.Format("sending {0}", UpstreamMessage));

                    await clientWSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(UpstreamMessage)),
                                                  WebSocketMessageType.Text,
                                                  true,
                                                  CancellationToken.None);

                    await Task.Delay(delayMs);
                }
            });



            var rcvTask = Task.Run(async () =>
            {
                var arraySegment = new ArraySegment<byte>(new byte[1024 * 256]);
                for (var i = 1; i <= rcvWaitCount; i++)
                {
                    var res = await clientWSocket.ReceiveAsync(arraySegment, CancellationToken.None);
                    var rcv = Encoding.UTF8.GetString(arraySegment.Array, 0, res.Count);
                    var downstreamMessage = string.Format("Server Message: {0}", rcv);
                    lock (consoleLock)
                        Console.WriteLine(downstreamMessage);

                    await Task.Delay(delayMs);

                }
            });



            await Task.WhenAll(sendTask, rcvTask);
        }





        #endregion




        static async Task UseStandardSocket()
        {

            FabricClient fc = new FabricClient(); //local cluster
            // my service is singlton partition
            var resolvedPartitions = await fc.ServiceManager.ResolveServicePartitionAsync(new Uri(FabricServiceName));
            var ep = resolvedPartitions.Endpoints.SingleOrDefault((endpoint) => endpoint.Role == ServiceEndpointRole.StatefulPrimary);
            var baseUri = ep.Address;

            var Tasks = new List<Task>();

            foreach (var path in socketPath)
                Tasks.Add(ClientSocketLoop(baseUri, path));


            await Task.WhenAll(Tasks);
            
        }

        static async Task ClientSocketLoop(string targetAddress, string socketPath)
        {
            var upStreamMessageCount = 10;
            var delayMs = 5 * 1000; // every 5 sec
            var rcvWaitCount = 10;

            var UpstreamMessage = string.Format("Hello Server On {0}", socketPath);
            var socketAddress = new Uri(string.Concat(targetAddress, socketPath));

            ClientWebSocket clientWSocket = new ClientWebSocket();
            await clientWSocket.ConnectAsync(socketAddress, CancellationToken.None);
            var sendTask = Task.Run(async () =>
               {
                   for (var i = 1; i <= upStreamMessageCount; i++)
                   {
                       lock(consoleLock)
                           Console.WriteLine(string.Format("sending {0}", UpstreamMessage));

                       await clientWSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(UpstreamMessage)),
                                                     WebSocketMessageType.Text,
                                                     true,
                                                     CancellationToken.None);

                       await Task.Delay(delayMs);
                   }
               });



            var rcvTask = Task.Run(async () => 
                {
                    var arraySegment = new ArraySegment<byte>(new byte[1024 * 256]);
                    for (var i = 1; i <= rcvWaitCount; i++)
                    { 
                        var res = await clientWSocket.ReceiveAsync(arraySegment, CancellationToken.None);
                        var rcv = Encoding.UTF8.GetString(arraySegment.Array, 0, res.Count);
                        var downstreamMessage = string.Format("Server Message: {0}", rcv);
                        lock(consoleLock)
                            Console.WriteLine(downstreamMessage);

                        await Task.Delay(delayMs);
                            
                    }
                });


            await Task.WhenAll(sendTask, rcvTask);


        }
    }
}
