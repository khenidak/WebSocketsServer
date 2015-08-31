using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketServer.Utils
{
    public class LeveledTask<T> : Task<T>, ILeveledTask
    {
        public string QueueId { get; set; }
        public bool IsHighPriority { get; set; }


        public LeveledTask(Func<T> function) : base(function)
        {
        }

        public LeveledTask(Func<object, T> function, object state) : base(function, state)
        {
        }

        public LeveledTask(Func<T> function, TaskCreationOptions creationOptions) : base(function, creationOptions)
        {
        }

        public LeveledTask(Func<T> function, CancellationToken cancellationToken) : base(function, cancellationToken)
        {
        }

        public LeveledTask(Func<object, T> function, object state, TaskCreationOptions creationOptions) : base(function, state, creationOptions)
        {
        }

        public LeveledTask(Func<object, T> function, object state, CancellationToken cancellationToken) : base(function, state, cancellationToken)
        {
        }

        public LeveledTask(Func<T> function, CancellationToken cancellationToken, TaskCreationOptions creationOptions) : base(function, cancellationToken, creationOptions)
        {
        }

        public LeveledTask(Func<object, T> function, object state, CancellationToken cancellationToken, TaskCreationOptions creationOptions) : base(function, state, cancellationToken, creationOptions)
        {
        }
    }
}
