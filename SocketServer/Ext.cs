using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketServer
{
    public static class Ext
    {
        public static byte[] Actualize(this ArraySegment<byte> buffer, int ActualLength)
        {

            var actual = new Byte[ActualLength];

            for (var i = 0; i < ActualLength; i++)
                actual[i] = buffer.Array[i];

            return actual;

        }
    }
}
