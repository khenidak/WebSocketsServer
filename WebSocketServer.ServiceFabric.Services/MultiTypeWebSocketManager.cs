using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.ServiceFabric.Data;
using System.Fabric;

namespace WebSocketServer.ServiceFabric.Services
{
    public class MultiTypeWebSocketManager : WebSocketSessionManager<ServiceFabricSocketSessionBase>
    {
        internal Dictionary<string, Type> MappedSessions = null;

        private IReliableStateManager m_StateManager;
        private ServiceInitializationParameters m_ServiceInitializationParameters;

        /// <summary>
        /// used to match to the current request to identify which socket type
        /// Override if you need to match using somethig else 
        /// (i.e. claims in a security token or a header or a query string)
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>

        protected Type Match(IOwinContext context)
        {
            var currentRequestUrl = new Uri(string.Concat("http://someserver",  
                context.Environment["owin.RequestPathBase"].ToString().ToLower()));
            var lastSegment = currentRequestUrl.Segments.Last();


            
            // look for a last segement that matches one of our maps
            foreach (var pair in MappedSessions)
                if (lastSegment == pair.Key.ToLower())
                    return pair.Value;

            // because we are using OwinMap Route we will only 
            // get here if we have the root address. 

            return MappedSessions[""];  //if we are here then we need to look for a key "" or null;
        }

        public MultiTypeWebSocketManager(IReliableStateManager StateManager, 
                                         ServiceInitializationParameters InitializationParameters)
        {
            m_StateManager = StateManager;
            m_ServiceInitializationParameters = InitializationParameters;
        }
        public override Task<bool> AcceptSocket(IOwinContext context)
        {
            if (null == MappedSessions || 0 == MappedSessions.Count)
                throw new InvalidOperationException("Multi types socket manager has no types");



            // do wehave a socket?
            Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>> accept = null;

            if (!IsSocket(context, ref accept))
                return Task.FromResult(false);



            var socketType = Match(context);

            if (null == socketType)
                return Task.FromResult(false);




            var newSocket = (ServiceFabricSocketSessionBase) Activator.CreateInstance(
                                                    socketType,
                                                    m_StateManager,
                                                    context,
                                                    this,
                                                    m_working_cts.Token);
            accept(null, newSocket.SocketLoop);

            AddSocket(newSocket);

            return Task.FromResult(true);
        }

    }
}