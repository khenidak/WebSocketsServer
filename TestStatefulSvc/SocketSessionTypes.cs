using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using WebSocketServer;
using WebSocketServer.ServiceFabric.Services;
using Microsoft.ServiceFabric.Data.Collections;

namespace TestStatefulSvc
{
    // these classes are sample socket sessions, 
    // service can reach all sockets via listner.SessionManager. 
    // classes can implement common base where core business operations can be defined
    // or core serializer/de-serialization behaviour can be implemented
    public class CustomerWSSession : ServiceFabricSocketSessionBase
    {
        public static readonly string DictionaryName = "CustomerMessages";

        public CustomerWSSession(IReliableStateManager stateManager, 
                                 Microsoft.Owin.IOwinContext context, 
                                 IWebSocketSessionManager<IWebSocketSession> factory, 
                                 CancellationToken cancelToken) : 
                                 base(stateManager, context, factory, cancelToken)
        {
        }

        public async Task SayHelloToCustomer(string HelloMessage)
        {
            await base.Post(string.Format("Server->Customer: {0}", HelloMessage));
        }
        public async Task CustomerSaid(string HelloMessage)
        {
            // just save them in a dictionary
            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(DictionaryName);

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var newKey = string.Format("{0}-{1}", this.SessionId, DateTime.UtcNow.Ticks);
                    await myDictionary.AddAsync(tx, newKey, HelloMessage);
                    await tx.CommitAsync();
                }
        }

        public override async Task OnReceiveAsync(ArraySegment<byte> buffer, Tuple<int, bool, int> received)
        {
            await CustomerSaid(await GetFromBufferAsString(buffer, received));
        }
    }




    public class OrderWSSession : ServiceFabricSocketSessionBase
    {
        public static readonly string DictionaryName = "OrderMessages";

        public OrderWSSession(IReliableStateManager stateManager,
                                 Microsoft.Owin.IOwinContext context,
                                 IWebSocketSessionManager<IWebSocketSession> factory,
                                 CancellationToken cancelToken) :
                                 base(stateManager, context, factory, cancelToken)
        {
        }

        public async Task SayHelloToOrder(string HelloMessage)
        {
            await base.Post(string.Format("Server->order: {0}", HelloMessage));
        }
        public async Task OrderSaid(string HelloMessage)
        {
            // just save them in a dictionary
            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(DictionaryName);

            using (var tx = this.StateManager.CreateTransaction())
            {
                var newKey = string.Format("{0}-{1}", this.SessionId, DateTime.UtcNow.Ticks);
                await myDictionary.AddAsync(tx, newKey, HelloMessage);
                await tx.CommitAsync();
            }
        }

        public override async Task OnReceiveAsync(ArraySegment<byte> buffer, Tuple<int, bool, int> received)
        {
            await OrderSaid(await GetFromBufferAsString(buffer, received));
        }
    }


    public class GeneralWSSession : ServiceFabricSocketSessionBase
    {
        public static readonly string DictionaryName = "GeneralMessages";

        public GeneralWSSession(IReliableStateManager stateManager,
                                 Microsoft.Owin.IOwinContext context,
                                 IWebSocketSessionManager<IWebSocketSession> factory,
                                 CancellationToken cancelToken) :
                                 base(stateManager, context, factory, cancelToken)
        {
        }

        public async Task SayHelloToGeneral(string HelloMessage)
        {
            await base.Post(string.Format("Server->General: {0}", HelloMessage));
        }
        public async Task GeneralSaid(string HelloMessage)
        {
            // just save them in a dictionary
            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(DictionaryName);

            using (var tx = this.StateManager.CreateTransaction())
            {
                var newKey = string.Format("{0}-{1}", this.SessionId, DateTime.UtcNow.Ticks);
                await myDictionary.AddAsync(tx, newKey, HelloMessage);
                await tx.CommitAsync();
            }
        }

        public override async Task OnReceiveAsync(ArraySegment<byte> buffer, Tuple<int, bool, int> received)
        {
            await GeneralSaid(await GetFromBufferAsString(buffer, received));
        }
    }
}
