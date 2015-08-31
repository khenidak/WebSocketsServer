using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.WebSockets;

using Microsoft.Owin;
using Owin;


namespace WebSocketServer
{
    public class WebSocketServerOwinMW<F, S> : OwinMiddleware 
                                            where F : WebSocketSessionManager<S>
                                            where S : WebSocketSessionBase
    {
        private WebSocketSessionManager<S> m_factory;
        private bool m_IsExeclusive = false;

        public WebSocketServerOwinMW(OwinMiddleware next, 
                                  WebSocketSessionManager<S> factory, 
                                  bool IsExeclusive) : base(next)
        {
            m_factory = factory;
            m_IsExeclusive = IsExeclusive;
        }



        public WebSocketServerOwinMW(OwinMiddleware next, 
                                  WebSocketSessionManager<S> factory) : base(next)
        {
            m_factory = factory;

        }

        public override async Task Invoke(IOwinContext context)
        {
            if (!await m_factory.AcceptSocket(context) && m_IsExeclusive)
            {
                context.FaultNotaSocket();
                return; 
            }
            
            await Next.Invoke(context);
        }
    }
}
