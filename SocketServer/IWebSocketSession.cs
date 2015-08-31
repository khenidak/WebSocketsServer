using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketServer
{
    public interface IWebSocketSession
    {
        uint MaxMessageSize
        {
            get;
            set;
        }
        string SessionId
        {
            get;
            set;
        }

        Task SocketLoop(IDictionary<string, object> websocketContext);


        Task Close();
        Task DrainThenClose();
        

        void Abort();
        Task OnReceiveAsync(ArraySegment<byte> buffer,Tuple<int, bool, int> received);


        Task Send(ArraySegment<byte> data,
                                SocketMessageType messageType,
                                bool EndOfMessage);
        Task Send(string sMessage);

        Task Send(byte[] data, SocketMessageType messageType);
        Task Send<T>(T O);
        Task Post(ArraySegment<byte> data,
                                SocketMessageType messageType,
                                bool EndOfMessage);
        Task Post(string sMessage);
        Task Post(byte[] data, SocketMessageType messageType);

        Task Post<T>(T O);
    }
}
