using Microsoft.ServiceFabric.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Fabric;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;

namespace WebSocketServer.ServiceFabric.Clients
{
    public class ServiceFabricWebSocketClient : ICommunicationClient, IDisposable
    {

        private ConcurrentDictionary<string, ClientWebSocket> m_sockets
            = new ConcurrentDictionary<string, ClientWebSocket>();

        private bool disposedValue = false; // To detect redundant calls

        public string BaseAddress { get; private set; }

        public ResolvedServicePartition ResolvedServicePartition { get; set; }


        private string getAddress(string subRoute)
        {
            return string.Concat(BaseAddress, subRoute);
        }
        private Task<ClientWebSocket> GetAddSocket(string sAddress)
        {
            return Task.Factory.StartNew((address) => {
                var strAddress = (string)address;

                var newSocket = new ClientWebSocket();
                var socket =  m_sockets.AddOrUpdate(sAddress, newSocket, (key, val) => { return val; } );

                // check socket 
                if (socket.State != WebSocketState.Open)
                    socket.ConnectAsync(new Uri(strAddress), CancellationToken.None).Wait();

                return socket;
            }, sAddress);
        }

     
        public ServiceFabricWebSocketClient(string baseAddress)
        {
            BaseAddress = baseAddress; 
        }

        public async  Task<ClientWebSocket> GetWebSocketAsync() 
        {
            return await GetAddSocket(BaseAddress);
        }

        public async Task<ClientWebSocket> GetWebSocketAsync(string subRoute) 
        {
            return await GetAddSocket(getAddress(subRoute));
        }

        public async Task CloseWebSocketAsync(string subRoute)
        {
            var socket = m_sockets[getAddress(subRoute)];
            if (null != socket)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "close!" , CancellationToken.None);
        }
        public void AbortAll()
        {
            foreach (var socket in m_sockets.Values)
                socket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "close!", CancellationToken.None).Wait();
        }





        #region IDisposable Support


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    AbortAll();
                }

                disposedValue = true;
            }
        }


        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}
