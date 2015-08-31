using Microsoft.ServiceFabric.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Fabric;
using System.Threading;

namespace WebSocketServer.ServiceFabric.Clients
{
    public class ServiceFabricWebSocketClientFactory : CommunicationClientFactoryBase<ServiceFabricWebSocketClient>

    {
        protected override void AbortClient(ServiceFabricWebSocketClient client)
        {
            client.AbortAll();
        }

        protected override Task<ServiceFabricWebSocketClient> CreateClientAsync(ResolvedServiceEndpoint endpoint, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ServiceFabricWebSocketClient(endpoint.Address));
        }

        protected override bool ValidateClient(ServiceFabricWebSocketClient clientChannel)
        {
            return true; // clients are valid because they depend on sockets. 
        }

        protected override bool ValidateClient(ResolvedServiceEndpoint endpoint, ServiceFabricWebSocketClient client)
        {
            return (client.BaseAddress == endpoint.Address);

        }
    }
}
