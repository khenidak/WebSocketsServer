using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketServer
{
    public enum SocketCloseStatus
    {
        EndpointUnavailable = 1001,
        InvalidMessageType  = 1003,
        InvalidPayloadData  = 1007,  
        MandatoryExtension  = 1010,
        MessageTooBig       = 1004,
        NormalClosure       = 1000,
        PolicyViolation     = 1008,
        ProtocolError       = 1002
    }
}
