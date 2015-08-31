using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using WebSocketServer.Utils;
using Newtonsoft.Json;
using System.Diagnostics;


// TODO: 
// Modify abort and cancel to maintan status and handle reentryance
// create a sepraete cancelation token for abort and cancel. 


namespace WebSocketServer
{
    using WebSocketSendAsync =
        Func
        <
            ArraySegment<byte> /* data */,
            int /* messageType */,
            bool /* endOfMessage */,
            CancellationToken /* cancel */,
            Task
        >;

    using WebSocketReceiveAsync =
        Func
        <
            ArraySegment<byte> /* data */,
            CancellationToken /* cancel */,
            Task
            <
                Tuple
                <
                    int /* messageType */,
                    bool /* endOfMessage */,
                    int /* count */
                >
            >
        >;

    using WebSocketCloseAsync =
        Func
        <
            int /* closeStatus */,
            string /* closeDescription */,
            CancellationToken /* cancel */,
            Task
        >;

    public abstract class WebSocketSessionBase : IWebSocketSession, IDisposable
    {
        


        protected readonly IOwinContext  m_context;
        protected  WebSocketSendAsync    m_SendAsync;
        protected  WebSocketReceiveAsync m_ReceiveAsync;
        protected  WebSocketCloseAsync   m_CloseAsync;
        protected readonly IWebSocketSessionManager<IWebSocketSession> m_factory;
        protected readonly CancellationToken     m_root_factory_working_token;
        protected readonly SequentialLeveledTaskScheduler m_Scheduler;
        protected string m_SessionId = string.Empty;


        private uint m_MaxMessageSize = 1024 * 256;

        
        public uint MaxMessageSize
        {
            get { return m_MaxMessageSize; }
            set
            {
                m_MaxMessageSize = value;
            }
        }


        public string SessionId
        {
            get { return m_SessionId; }
            set {
                // todo: validate if we are connected, fail if we are
                m_SessionId = value;
            }
        }

        public WebSocketSessionBase(IOwinContext context, 
                             IWebSocketSessionManager<IWebSocketSession> factory,
                             CancellationToken cancelToken)
        {
            m_context = context;
            m_factory = factory;
            m_SessionId = Guid.NewGuid().ToString();
            m_root_factory_working_token = cancelToken;
            m_Scheduler = factory.Scheduler;
        }



        public async Task SocketLoop(IDictionary<string, object> websocketContext)
        {
            m_SendAsync = (WebSocketSendAsync)websocketContext["websocket.SendAsync"];
            m_ReceiveAsync = (WebSocketReceiveAsync)websocketContext["websocket.ReceiveAsync"];
            m_CloseAsync = (WebSocketCloseAsync)websocketContext["websocket.CloseAsync"];

            var bytes = new byte[MaxMessageSize];
            bool bClose = true;
            Tuple<int, bool, int> received = null;
            try
            {
                do
                {
                    // keep track of item3 (size). the rest of the buffer 
                    // will evantually become garbage as messages received. 

                    ArraySegment<byte> buffer = new ArraySegment<byte>(bytes);
                    received = await m_ReceiveAsync(buffer, m_root_factory_working_token);

                    if (received.Item3 > MaxMessageSize)
                    {
                        await Close(SocketCloseStatus.MessageTooBig);
                        break;
                    }

                    if (received.Item3 > 0)
                        await OnReceiveAsync(buffer, received);
                }
                while (received.Item1 != (int)SocketMessageType.Close &&
                       !m_root_factory_working_token.IsCancellationRequested);
            }
            catch (AggregateException ae)
            {
                var flat = ae.Flatten();
                Trace.WriteLine(string.Format("socket {0} encountered {1} error during receiving and is aborting", m_SessionId, ae.Message), "error");
                await Close(SocketCloseStatus.EndpointUnavailable);
                bClose = false;
            }
            catch (Exception E)
            {
                Trace.WriteLine(string.Format("socket {0} encountered {1} error dropped by client, aborting", m_SessionId, E.Message), "warning");
                await Close(SocketCloseStatus.EndpointUnavailable);
                bClose = false;
            }

            if(bClose)
                await Close(SocketCloseStatus.NormalClosure);
        }

        public virtual Task DrainThenClose()
        {
            // this posts a task at the end of 
            // low priority task queue. 

            // if a sender tries to send now
            // s/he might get a socket closing error 
            // from the underlying OWIN
            var lt = new LeveledTask(
                    () =>
                    {
                        Close().Wait();
                    }
                );

            lt.QueueId = m_SessionId;
            lt.IsHighPriority = false;
            lt.Start(m_Scheduler);
            return lt;

        }

        public virtual async Task Close()
        {
            await Close(SocketCloseStatus.NormalClosure);
        }

        protected virtual async Task Close(SocketCloseStatus closeStatus)
        {
                // this will close the socket even if there are messages
                // in flight waiting on the down stream.
                await m_CloseAsync((int)closeStatus, closeStatus.ToString(), this.m_root_factory_working_token);
                m_Scheduler.RemoveQueue(m_SessionId);
                m_factory.SockedClosed(m_SessionId);
        
        
        }

        public virtual void Abort()
        {
            // no wait. 
            var state = SocketCloseStatus.EndpointUnavailable;
            m_CloseAsync((int)state, state.ToString(), this.m_root_factory_working_token);
            m_Scheduler.RemoveQueue(m_SessionId);
            m_factory.SockedClosed(m_SessionId);
        }



        public abstract Task OnReceiveAsync(ArraySegment<byte> buffer,
                                                 Tuple<int, bool, int> received);

        protected Task<O> GetFromBufferAsJson<O>(ArraySegment<byte> buffer, Tuple<int, bool, int> received) where O: new()
        {
            return Task.Factory.StartNew<O>( function: ()=> 
                                             {
                                                var messageLength = received.Item3;
                                                var actual = buffer.Actualize(messageLength);
                                                return JsonConvert.DeserializeObject<O>(Encoding.UTF8.GetString(actual));
                                             }
                                           );
        }

        protected Task<string> GetFromBufferAsString(ArraySegment<byte> buffer, Tuple<int, bool, int> received)
        {
            return Task.Factory.StartNew(function: () =>
                                            {
                                                var bytes = buffer.Actualize(received.Item3);
                                                var message = Encoding.UTF8.GetString(bytes);
                                                return message;
                                            }
                                         );
        }
        
        public virtual Task Send(ArraySegment<byte> data, 
                                SocketMessageType messageType, 
                                bool EndOfMessage)
        {
            var bIsHighPrority = true; // puts it at the begining of the Q
            return makeSendTask(data, messageType, EndOfMessage, bIsHighPrority);
        }
        public virtual Task Send(string sMessage)
        {
            var bytes = UTF8Encoding.UTF8.GetBytes(sMessage);
            return Send(new ArraySegment<Byte>(bytes), SocketMessageType.Text, true); 
        }

        public virtual Task Send(byte[] data, SocketMessageType messageType)
        {
            var arraySegement = new ArraySegment<byte>(data);
            return Send(arraySegement, messageType, true);
        }
        public virtual Task Send<T>(T O) 
        {
                var sJson = JsonConvert.SerializeObject(O);
                return Send(sJson);
        }

        public virtual Task Post(ArraySegment<byte> data,
                                SocketMessageType messageType,
                                bool EndOfMessage)
        {
            var bIsHighPrority = false; // puts it at the end of the Q
            return makeSendTask(data, messageType, EndOfMessage, bIsHighPrority);
        }
        public virtual Task Post(string sMessage)
        {
            var bytes = UTF8Encoding.UTF8.GetBytes(sMessage);
            return Post(new ArraySegment<Byte>(bytes), SocketMessageType.Text, true);
        }

        public virtual Task Post<T>(T O)
        {
            var sJson = JsonConvert.SerializeObject(O);
            return Post(sJson);
        }


        public virtual Task Post(byte[] data, SocketMessageType messageType)
        {
            var arraySegement = new ArraySegment<byte>(data);
            return Post(arraySegement, messageType, true);
        }

        protected virtual Task makeSendTask(ArraySegment<byte> data,
                                            SocketMessageType messageType,
                                            bool EndOfMessage,
                                            bool isHighPriority)
        {
            var lt = new LeveledTask(() => m_SendAsync(data, (int)messageType, EndOfMessage, m_root_factory_working_token).Wait());
            lt.QueueId = m_SessionId;
            lt.IsHighPriority = isHighPriority;
            lt.Start(m_Scheduler);
            return lt;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    Abort();

                disposedValue = true;
            }
        }

       

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
