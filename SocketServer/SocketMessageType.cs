using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketServer
{
    public enum SocketMessageType : int
    {
        Text = 0x1, 
        Binary = 0x2, 
        Close = 0x8 
    }
}
