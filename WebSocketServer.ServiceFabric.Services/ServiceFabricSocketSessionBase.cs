using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.ServiceFabric.Data;

namespace WebSocketServer.ServiceFabric.Services
{
    // services as base class for service fabric sessions
    // sessions will need access to State Manager
    public abstract class ServiceFabricSocketSessionBase : WebSocketSessionBase
    {
        protected IReliableStateManager StateManager;
        public ServiceFabricSocketSessionBase(IReliableStateManager stateManager,
                                          IOwinContext context, 
                                          IWebSocketSessionManager<IWebSocketSession> factory, 
                                          CancellationToken cancelToken) : 
                                          base(context, factory, cancelToken)
        {
            StateManager = stateManager;
        }

      
    }


}
