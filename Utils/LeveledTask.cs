using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketServer.Utils
{
    public class LeveledTask : Task, ILeveledTask
    {
        public string QueueId { get; set; }
        public bool IsHighPriority { get; set; }

        #region Ctors
        public LeveledTask(Action action) : base(action)
        {
        }

        public LeveledTask(Action<object> action, object state) : base(action, state)
        {
        }

        public LeveledTask(Action action, TaskCreationOptions creationOptions) : base(action, creationOptions)
        {
        }

        public LeveledTask(Action action, CancellationToken cancellationToken) : base(action, cancellationToken)
        {
        }

        public LeveledTask(Action<object> action, object state, TaskCreationOptions creationOptions) : base(action, state, creationOptions)
        {
        }

        public LeveledTask(Action<object> action, object state, CancellationToken cancellationToken) : base(action, state, cancellationToken)
        {
        }

        public LeveledTask(Action action, CancellationToken cancellationToken, TaskCreationOptions creationOptions) : base(action, cancellationToken, creationOptions)
        {
        }

        public LeveledTask(Action<object> action, object state, CancellationToken cancellationToken, TaskCreationOptions creationOptions) : base(action, state, cancellationToken, creationOptions)
        {
        }
        #endregion
    }
}
