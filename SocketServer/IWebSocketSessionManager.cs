using Microsoft.Owin;
using WebSocketServer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketServer
{
    public interface IWebSocketSessionManager<out T>  where T : IWebSocketSession
    {
       SequentialLeveledTaskScheduler Scheduler { get; }
        int Count { get; }
       Task<bool> AcceptSocket(IOwinContext context);
        void AbortAll();
        Task PostToAll(byte[] buffer, SocketMessageType messageType);
        Task PostToAll(string message);
        void SockedClosed(string SessionId);

        Task CloseAll();
        Task CloseAll(CancellationToken cancellationToken);
        IEnumerable<T> GetSession(Func<T, bool> Predicate);

        Task<bool> Close(string SessionId);
    }
}
