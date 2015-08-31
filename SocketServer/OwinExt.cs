using Microsoft.Owin;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketServer
{
    public static class OwinExt
    {
#region OWIN USE Helpers
        public static void MapWebSocketRoute<F, S>(this IAppBuilder app, 
                                                string route, 
                                                F factory) 
                                                where F : WebSocketSessionManager<S>
                                                where S : WebSocketSessionBase 
                                                                 
        {
            app.Map(route, builder => builder.Use<WebSocketServerOwinMW<F,S>>(factory));
        }

        public static void MapWebSocket<F,S>(this IAppBuilder app, 
                                            F factory) 
                                            where F : WebSocketSessionManager<S>
                                            where S : WebSocketSessionBase
        {
            app.Use<WebSocketServerOwinMW<F,S>>(factory);
        }



        public static void ExeclusiveMapWebSocketRoute<F,S>(this IAppBuilder app,
                                                string route,
                                                F factory) 
                                                where F : WebSocketSessionManager<S>
                                                where S : WebSocketSessionBase
        { 
            app.Map(route, builder => builder.Use<WebSocketServerOwinMW<F,S>>(factory, true));
        }


        public static void ExeclusiveMapWebSocket<F,S>(this IAppBuilder app, 
                                                    F factory)
                                                    where F : WebSocketSessionManager<S>
                                                    where S : WebSocketSessionBase
        {
            app.Use<WebSocketServerOwinMW<F,S>>(factory, true);
        }


#endregion

        public static void Fault(this IOwinContext context, int StatusCdoe,  string error)
        {
            context.Response.StatusCode = StatusCdoe;
            context.Response.Write(error);
        }
        public static void FaultNotaSocket(this IOwinContext context)
        {
            context.Fault(500, "Not a socket request, server expects a web socket");
        }
    }
}
