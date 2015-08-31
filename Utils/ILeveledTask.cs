using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketServer.Utils
{
    public interface ILeveledTask
    {
            string QueueId { get; set; }
            bool IsHighPriority { get; set; }
    }
}
