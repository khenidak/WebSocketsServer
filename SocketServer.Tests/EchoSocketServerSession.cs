using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WebSocketServer;
using Microsoft.Owin;
using System.Threading;
using System.Diagnostics;

namespace WebSocketServer.Tests
{




    // custom factory and session implementation
    // they do nothing other than exposing a delegate which can be set by test 
    // classes to get whatever was recieved by the web socket on the server side.


    // in your implementation you don't have to implement a custom factory
    // but you will have to implement a custom session (base class is abstract);
    class TestWebSocketSession : WebSocketSessionBase
    {
        public TestWebSocketSession(IOwinContext context,
                             TestWebSocketSessionFactory factory,
                             CancellationToken cancelToken) : base(context,  factory, cancelToken)
        {
            //no op just init base 
        }



        // we home to the factory and call a call back on it.
        // tests will override the call back for testing purposes. 
        public override Task OnReceiveAsync(ArraySegment<byte> buffer, Tuple<int, bool, int> received)
        {
            var factory = m_factory as TestWebSocketSessionFactory;
            return factory.OnReceiveAsyncCallBack(this, buffer, received);
        }        
    }

    class TestWebSocketSessionFactory : WebSocketSessionManager<TestWebSocketSession>
    {



        // tests should set this delegate to get whatever ever recieved by the socket. 
        public Func<IWebSocketSession, ArraySegment<byte>, Tuple<int, bool, int>, Task> OnReceiveAsyncCallBack =
            (session, buffer, tuple) =>
            {

                Trace.WriteLine("Default recieved called");
                return Task.FromResult(0);
            };

    }
}
