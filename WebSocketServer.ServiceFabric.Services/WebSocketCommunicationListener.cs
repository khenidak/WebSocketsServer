using Microsoft.ServiceFabric.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Fabric;
using System.Threading;
using Microsoft.ServiceFabric.Data;
using Owin;
using System.Globalization;
using Microsoft.Owin.Hosting;
using System.Diagnostics;

namespace WebSocketServer.ServiceFabric.Services
{
    public class WebSocketCommunicationListener : ICommunicationListener
    {
        private Dictionary<string, Type> m_mappedSessions =
            new Dictionary<string, Type>();

        private IReliableStateManager m_ReliableStateManager;
        private IDisposable m_WebServer = null;
        private MultiTypeWebSocketManager m_WebSocketSessionsManager;
        private ServiceInitializationParameters m_InitializationParameters;
        private string m_ListeningAddress = string.Empty;
        private string m_PublishingAddress = string.Empty;

        public string ListeningAddress { get { return m_ListeningAddress; } }
        public string PublishingAddress { get { return m_PublishingAddress; } }

        public ServiceInitializationParameters InitializationParameters { get { return m_InitializationParameters; } }
        public Func<WebSocketCommunicationListener, string> OnCreateListeningAddress { get; set; }
        public Func<WebSocketCommunicationListener, string> OnCreatePublishingAddress { get; set; }

        public Action<WebSocketCommunicationListener, IAppBuilder> OnOwinPreMapping { get; set; }

        public Action<WebSocketCommunicationListener, IAppBuilder> OnOwinPostMapping  { get; set; }


        private void EnsureStubFuncs()
        {
            if (null == OnOwinPreMapping)
                OnOwinPreMapping = (listener, app) => { };

            if (null == OnOwinPostMapping)
                        OnOwinPostMapping = (listener, app) => { };

            if (null == OnCreateListeningAddress)
                OnCreateListeningAddress = (listener) =>
                {
                    StatefulServiceInitializationParameters statefulInitParam;

                    var bIsStateful = (null != (statefulInitParam = listener.InitializationParameters as StatefulServiceInitializationParameters));
                    var port = listener.InitializationParameters.CodePackageActivationContext.GetEndpoint("ServiceEndPoint").Port;

                    
                    if (bIsStateful)
                        return String.Format(
                                    CultureInfo.InvariantCulture,
                                    "http://{0}:{1}/{2}/{3}/",
                                    FabricRuntime.GetNodeContext().IPAddressOrFQDN,
                                    port,
                                    statefulInitParam.PartitionId,
                                    statefulInitParam.ReplicaId);
                    else
                        return String.Format(
                                    CultureInfo.InvariantCulture,
                                    "http://{0}:{1}/",
                                    FabricRuntime.GetNodeContext().IPAddressOrFQDN,
                                    port);
                };


            if (null == OnCreatePublishingAddress)
                OnCreatePublishingAddress = (listener) =>
                {
                    // HTTPListener doesn't like WSS while clients will expect it
                    return listener.m_ListeningAddress.Replace("http://", "ws://");
         
                };

        }


        public WebSocketCommunicationListener(IReliableStateManager StateManager)                               
        {
            m_ReliableStateManager = StateManager;
        }

        
        public IEnumerable<KeyValuePair<string, Type>> Maps
        {
            get { return m_mappedSessions.ToArray(); }
        }

        public MultiTypeWebSocketManager SessionManager
        {
            get { return m_WebSocketSessionsManager; }
        }

        public void Map(string subRoute, Type Socket)
        {
            //TODO: if connected throw

            if (m_mappedSessions.ContainsKey(subRoute))
                throw new InvalidOperationException(string.Format("Sub route {0} is already mapped to {1}", subRoute, m_mappedSessions[subRoute].ToString()));

            if(!Socket.GetInterfaces().Contains(typeof(IWebSocketSession)))
                throw new InvalidOperationException(string.Format("Type {0} is not a web socket", Socket.ToString()));

            m_mappedSessions.Add(subRoute, Socket);
        }

        public void RemoveMap(string subRoute)
        {
            //TODO: if connected throw
            m_mappedSessions.Remove(subRoute);
        }


        public void Abort()
        {
            if (m_WebSocketSessionsManager != null)
                m_WebSocketSessionsManager.AbortAll();

            if (null != m_WebServer) m_WebServer.Dispose();
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if(m_WebSocketSessionsManager != null)
                await m_WebSocketSessionsManager.CloseAll(cancellationToken);

            if (null!= m_WebServer) m_WebServer.Dispose();
        }

        public void Initialize(ServiceInitializationParameters serviceInitializationParameters)
        {            
            m_WebSocketSessionsManager = new MultiTypeWebSocketManager(m_ReliableStateManager, serviceInitializationParameters);
            m_InitializationParameters = serviceInitializationParameters;
        }

        public  Task<string> OpenAsync(CancellationToken cancellationToken)
        {
            EnsureStubFuncs();

            return Task.Factory.StartNew(function:() =>
            {
                m_ListeningAddress = OnCreateListeningAddress(this);
                m_PublishingAddress = OnCreatePublishingAddress(this);
                //ensure that last char is "/"
                if (m_ListeningAddress[m_ListeningAddress.Length - 1] != '/')
                    m_ListeningAddress = string.Concat(m_ListeningAddress, "/");

                if (m_PublishingAddress[m_PublishingAddress.Length - 1] != '/')
                    m_PublishingAddress = string.Concat(m_PublishingAddress, "/");


                // pass the map to the session manager
                m_WebSocketSessionsManager.MappedSessions = m_mappedSessions;

                Trace.WriteLine(string.Format("Service Fabric WebSocket starting on {0}",m_ListeningAddress));

                // start web server
                m_WebServer = WebApp.Start(m_ListeningAddress, app =>
                                        {
                                            // call premap
                                            OnOwinPreMapping(this, app);

                                       
                                            var path = "/"; // Owin expects path segmenets after
                                                            // listening, not at the root path.

                                            // map route for each of the maps
                                            // the root map (ie maps "") remove trailing /
                                            foreach (var pair in m_mappedSessions)
                                            {

                                                var mappedPath = string.Empty;
                                                if (string.Empty != pair.Key)
                                                    mappedPath = string.Concat(path, pair.Key);
                                                else
                                                    mappedPath = path.Substring(0, path.Length - 1); // remove trailing '/'
                                                
                                                app.MapWebSocketRoute<MultiTypeWebSocketManager, ServiceFabricSocketSessionBase>
                                                        (mappedPath, m_WebSocketSessionsManager);
                                                
                                                Trace.WriteLine(string.Format("{0} mapped to {1}", mappedPath, pair.Value.ToString()));
                                            }

                                            OnOwinPostMapping(this, app);
                                            // call post map
                                        }
                                    );
                return m_PublishingAddress;
            });            
        }
    }
}
