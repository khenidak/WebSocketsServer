using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketServer.Utils
{
    public class LeveledTask : Task
    {
        public string QueueId = string.Empty;
        public bool IsHighPriority = false;

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
    public class SequentialLeveledMultiQTaskScheduler : TaskScheduler
    {

        private ConcurrentDictionary<string, ConcurrentLinkedList<LeveledTask>> queues
            = new ConcurrentDictionary<string, ConcurrentLinkedList<LeveledTask>>();

       

        
        public const int DefaulltMinTaskBeforeYield = 10;
        public const int DefaultMaxTaskBeforeYield  = 50;

        private int m_MaxTaskBeforeYield = DefaulltMinTaskBeforeYield;

        public int MaxTaskBeforeYield
            {
            set {
                if (value > DefaultMaxTaskBeforeYield)
                {
                    m_MaxTaskBeforeYield = DefaultMaxTaskBeforeYield;
                    return;
                }
                if (value < DefaulltMinTaskBeforeYield)
                {
                    m_MaxTaskBeforeYield = DefaulltMinTaskBeforeYield;
                    return;
                }

                m_MaxTaskBeforeYield = value;
            }
            get { return m_MaxTaskBeforeYield; }
            }




        private ConcurrentLinkedList<LeveledTask> GetOrAddQueue(string QueueId)
        {
            var linkedlist = queues.GetOrAdd(QueueId, new ConcurrentLinkedList<LeveledTask>());
            return linkedlist;
        }
        private bool RemoveQueue(string QueueId, bool cancelTasks = true)
        {
                ConcurrentLinkedList<LeveledTask> ll;
                var bFound = queues.TryRemove(QueueId, out ll);
            throw new NotImplementedException();

            /*

            if (bFound && cancelTasks)
            {
                ConcurrentLinkedListNode<LeveledTask> node = ll.Head;
                do
                {
                    var leveledTask = node.Value;
                    if(!leveledTask.IsCompleted)
                         
                }
                while (null != (node = node.Next));

            }
            */




                return bFound;            
        }

        

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            var masterList = new List<Task>(0);

            foreach (var linkedlist in queues.Values)
                masterList.AddRange(linkedlist);

            return masterList;
        }

        protected override void QueueTask(Task task)
        {
            var leveledtask = task  as LeveledTask;
            if (null == leveledtask)
                throw new InvalidOperationException("this leveled sequential scheduler shouldn't be used with regular Task objects");


            if (leveledtask.QueueId == null ||
                leveledtask.QueueId == string.Empty)
                throw new InvalidOperationException("Task scheduler received a task that does not have a queue assigned to it");

            var linkedList = GetOrAddQueue(leveledtask.QueueId);
            
            if (leveledtask.IsHighPriority)
                linkedList.AddHead(leveledtask);
            else
                linkedList.AddTail(leveledtask);

            ProcessWork(leveledtask.QueueId);   
        }

        private void ProcessWork(string QueueId)
        {
         

            var linkedlist =  GetOrAddQueue(QueueId);
                      
            if (0 == linkedlist.Count)
                return; // was already completed.


            ThreadPool.UnsafeQueueUserWorkItem(w => {

                
                bool bGotLock = false;
                Monitor.TryEnter(linkedlist, ref bGotLock);

                if (!bGotLock) // a thread from the thread pool is already looping on the Q. 
                    return;    // at any point of time ONLY one thread is allwoed to dequeue on the Q to enable order tasks
               

                var ExecutedTasks = 0;
                LeveledTask leveledTask = linkedlist.RemoveHead();
                // basically at minimum we pull 2 tasks even if the first is null. 


                do
                {
                    ExecutedTasks++;

                    //Trace.WriteLine(string.Format("Q:{0} T:{1} Thread:{2}", QueueId, ExecutedTasks, Thread.CurrentThread.ManagedThreadId));
                    //Trace.WriteLine(string.Format("Task Null:{0}", (leveledTask == null) ? "yes" : "no"));

                    if (leveledTask != null)
                    {
                        try
                        {
                            base.TryExecuteTask(leveledTask);
                        }
                        catch(Exception e)
                        {
                            Trace.WriteLine(string.Format("Task Executer: Error Executing Task {0} {1}", e.Message, e.StackTrace), "error");
                        }
                    }

                }
                while ((ExecutedTasks <= m_MaxTaskBeforeYield && queues.Count > 1) &&  null != (leveledTask = linkedlist.RemoveHead()));

                Monitor.Exit(linkedlist);

                // Yielded, call it back to ensure that remaining tasks will be executed. 

                if ((ExecutedTasks > m_MaxTaskBeforeYield && queues.Count > 1))
                {   
                        Trace.WriteLine(string.Format("Queue {0} yielded thread {1} after {2} tasks",
                                                   QueueId,
                                                   Thread.CurrentThread.ManagedThreadId,
                                                   ExecutedTasks - 1));
                    Task.Run(() => {
                        ProcessWork(QueueId); // hopefully by the time this one is called
                                              // the thread has been returned to the pool and picked more 
                                              // work from other queues. 
                    });
                }


            }, null);

        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // if tasks got inlined it will lose this position in the queue.
            // so we can not afford inline tasks here
            return false;
        }
    }
}
