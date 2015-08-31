using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services;
using WebSocketServer.ServiceFabric.Services;
using System.Diagnostics;
using Microsoft.Owin;
using Owin;

namespace TestStatefulSvc
{

  
    public class TestStatefulSvc : StatefulService
    {
        private WebSocketCommunicationListener m_listener;
        private Random m_rnd = new Random();
         
        protected override ICommunicationListener CreateCommunicationListener()
        {
            m_listener = new WebSocketCommunicationListener(StateManager);
            // map to any type that implements ServiceFabricWebSocketSessionBase
            m_listener.Map("customer", typeof(CustomerWSSession)); // mapped to <listeningaddress>/customer
            m_listener.Map("order", typeof(OrderWSSession));    // mapped to <listeningaddress>/order
            m_listener.Map("", typeof(GeneralWSSession));    // mapped to <listeningaddress>/


            // you can use the above to filter sockets based on the replica type
            // for example primaries can have different socket types than seconaries. 


            // Listening address is what the server actually listen to
            // publishing address is what is returned to Fabric runtime (commnunicated as EndPoint.Address on client side)
            /*
                if you want to control how listening and publishing addresses are created
                    m_listener.OnCreateListeningAddress = (listener) => { << return my listening address here >>}
                    m_listener.OnCreatePublishingAddress = (listener) => { << return my Publishing ddress here >>}            
            */

            /* 
                if you want to add more OWIN stuff pre or post web socket stages
                    m_listener.OnOwinPreMapping = (listener, appbuilder) => { << appbuilder.UseXX  >>}
                    m_listener.OnOwinPostMapping = (listener, appbuilder) => { << appbuilder.UseXX  >>}
            */


            return m_listener;
        }


        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
        // this a typical case where a service
        // needs to send messages to connected client
        // in my case i am just filtering by session type.
        // you can use the predicate passed to getSession to get your target session

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(5000); // every five secs
            var n = m_rnd.Next(1, 3); // not really that random
            try
            {
                switch (n)
                {
                    case 1: // we will send to all "generalWsSession" connected; 
                        {
                            var clients = m_listener.SessionManager.GetSession((session) => null != (session as GeneralWSSession));
                            foreach (var client in clients)
                                await ((GeneralWSSession)client).SayHelloToGeneral(string.Format("To all general - {0}", DateTime.UtcNow.Ticks));
                            break;
                        }
                    case 2: // we will send to all "customerWsSession" connected;
                        {
                            var clients = m_listener.SessionManager.GetSession((session) => null != (session as CustomerWSSession));
                            foreach (var client in clients)
                                await ((CustomerWSSession)client).SayHelloToCustomer(string.Format("to all customer- {0}", DateTime.UtcNow.Ticks));
                            break;
                        }
                    case 3: // we will send to all "OrderWsSession" connected;
                        {
                            var clients = m_listener.SessionManager.GetSession((session) => null != (session as OrderWSSession));
                            foreach (var client in clients)
                                await ((OrderWSSession)client).SayHelloToOrder(string.Format("to all order - {0}", DateTime.UtcNow.Ticks));
                            break;
                        }
                }
            }
            catch (AggregateException ae)
            {
                Trace.WriteLine("Failed to send!" + ae.InnerException.ToString());
            }

            }
        }
    }
}
