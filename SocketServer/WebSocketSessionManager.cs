using Microsoft.Owin;
using WebSocketServer.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;




namespace WebSocketServer
{


    public class WebSocketSessionManager<T> : IWebSocketSessionManager<T>, IDisposable  where T : IWebSocketSession
    {
        
        protected CancellationTokenSource m_working_cts = new CancellationTokenSource();
        
        protected readonly ConcurrentDictionary<string, T> m_sessions =
                               new ConcurrentDictionary<string, T>();


        protected readonly SequentialLeveledTaskScheduler m_scheduler = new SequentialLeveledTaskScheduler();
        public SequentialLeveledTaskScheduler Scheduler{get { return m_scheduler; }}
        public int Count { get { return m_sessions.Count; } }
        public WebSocketSessionManager(){}



        protected virtual bool IsSocket(IOwinContext context, ref Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>> accept)
        {
            accept = context.Environment["websocket.Accept"] as
               Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>;

            if (null == accept)
                return false;

            return true;
        }

        protected virtual void AddSocket(T newSocket)
        {
            m_sessions.AddOrUpdate(newSocket.SessionId, 
                                   newSocket, 
                                   (key, socket) => // don't allow duplicates
                                        {
                                            throw new InvalidOperationException("a socket with the same session id is already connected");
                                        }
                                 );
        }


        public virtual Task<bool> AcceptSocket(IOwinContext context)
        {
            Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>> accept = null;

            if (!IsSocket(context, ref accept))
                return Task.FromResult(false);


            var newSocket = (T)Activator.CreateInstance(typeof(T),
                                                        context,
                                                        this,
                                                        m_working_cts.Token);
            accept(null, newSocket.SocketLoop);

            AddSocket(newSocket);

            return Task.FromResult(true);
        }
        
        
        public virtual void AbortAll()
        {
            foreach (var session in m_sessions.Values)
                session.Abort();
        }

        public virtual  void SockedClosed(string SessionId)
        {
            // todo handle false returns. 
            T current;
            m_sessions.TryRemove(SessionId, out current);

        }
        public virtual Task PostToAll(string message)
        {
            return PostToAll(Encoding.UTF8.GetBytes(message), 
                            SocketMessageType.Text);

        }
        public virtual Task PostToAll(byte[] buffer, SocketMessageType messageType)
        {
            var postAllTasks = new List<Task>();
            foreach (var session in m_sessions.Values)
                postAllTasks.Add(Task.Factory.StartNew(() => { session.Post(buffer, messageType); }));

            return Task.WhenAll(postAllTasks);
        }

        public virtual Task CloseAll()
        {
            return CloseAll(CancellationToken.None);
        }

        public virtual Task CloseAll(CancellationToken cancellationToken)
        {
            var closeAllTasks = new List<Task>();
            
            foreach (var session in m_sessions.Values)
                closeAllTasks.Add( Task.Factory.StartNew(() => { session.Close(); } , cancellationToken));

            return Task.WhenAll(closeAllTasks);
        }


        public virtual async Task<bool> Close(string SessionId)
        {
            T session;
            var bFound = m_sessions.TryGetValue(SessionId, out session);
            if (!bFound) return bFound;

            await session.Close();

            return bFound;
        }

        public T this[string SessionId]
        {
            get { return m_sessions[SessionId]; }
        }

        public IEnumerable<T> GetSession(Func<T, bool> Predicate)
        {
            return m_sessions.Values.Where(session => Predicate(session));
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    AbortAll();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            
            Dispose(true);
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}