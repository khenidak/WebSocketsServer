using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketServer.Utils
{
   
    public class SequentialLeveledTaskScheduler : TaskScheduler
    {

        private ConcurrentDictionary<string, ConcurrentQueue<Task>> m_HiQueues
            = new ConcurrentDictionary<string, ConcurrentQueue<Task>>();

        private ConcurrentDictionary<string, ConcurrentQueue<Task>> m_LowQueues
        = new ConcurrentDictionary<string, ConcurrentQueue<Task>>();

        private ConcurrentDictionary<string, object> MasterQueueLocks
            = new ConcurrentDictionary<string, object>();

        
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

        public void AddQueue(string QueueId)
        {
            var bHighPrority = true;
            var bAddIfNotExist = true;

            GetOrAddQueue(QueueId, bHighPrority, bAddIfNotExist);
            GetOrAddQueue(QueueId, !bHighPrority, bAddIfNotExist);
        }



        public  IEnumerable<Task> GetScheduledTasks(string QueueId)
        {
            var hQ = GetOrAddQueue(QueueId, true, false);
            var lQ = GetOrAddQueue(QueueId, false, false);

            if (null == hQ || null == lQ)
                return null;

            var masterList = new List<Task>();

            masterList.AddRange(hQ);
            masterList.AddRange(lQ);
            

            return masterList;
        }


        private ConcurrentQueue<Task> GetOrAddQueue(string QueueId, bool isHighPriority, bool addIfNotExist = true)
        {
            if (addIfNotExist)
            {
                var hiQueue = m_HiQueues.GetOrAdd(QueueId, new ConcurrentQueue<Task>());
                var lowQueue = m_LowQueues.GetOrAdd(QueueId, new ConcurrentQueue<Task>());

                return (isHighPriority) ? hiQueue : lowQueue;
            }
            else
            {
                var qList = (isHighPriority) ? m_HiQueues : m_LowQueues;
                return qList[QueueId];
            }
        }

        private object GetOrAddLock(string QueueId, bool AddIfNotExist = true)
        {
            if (AddIfNotExist)
            {
                object oNewLock = new object();
                var o = MasterQueueLocks.GetOrAdd(QueueId, oNewLock);
                return o;
            }
            else
            {
                return MasterQueueLocks[QueueId];
            }
            
        }

        public void RemoveQueue(string QueueId)
        {
            LeveledTask lt = new LeveledTask(() =>
            {

                // this will remove the Q from the list of Q
                // but will not null it so if we have an going exection it will just keep on going. 
                // because the list of Q and locks no longer maintain a reference to lQ & HQ and lock
                // it will evantually be null

                Trace.WriteLine(string.Format("queue {0} will be removed", QueueId), "info");

                ConcurrentQueue<Task> q;
                object oLock;
                
                
                m_HiQueues.TryRemove(QueueId, out q);
                m_LowQueues.TryRemove(QueueId, out q);

                MasterQueueLocks.TryRemove(QueueId, out oLock);

            });

            lt.IsHighPriority = false;
            lt.QueueId = QueueId;
            lt.Start(this);

        }

        

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            var masterList = new List<Task>();

            foreach (var hqueue in m_HiQueues.Values)
                masterList.AddRange(hqueue);

            foreach (var lqueue in m_LowQueues.Values)
                masterList.AddRange(lqueue);
            return masterList;
        }



        protected override void QueueTask(Task task)
        {
            var leveledtask = TaskAsLeveledTask(task);

            /* 

            if (null == leveledtask)
                throw new InvalidOperationException("this leveled sequential scheduler shouldn't be used with regular Task objects"); // bang!
            
                TPL favors the same schedules for the child tasks
                if a leveled task created another task TPL will attempt to
                schedule it here. 
            */


            if (null == leveledtask)
            {
                Trace.WriteLine("Sequential scheduler encounterd an unlevled task and will execute it as usual");
                ProcessUnleveledTask(task);
                return;
            }



            if (leveledtask.QueueId == null ||
                leveledtask.QueueId == string.Empty)
                throw new InvalidOperationException("Task scheduler received a task that does not have a queue assigned to it"); // bang!

            var Q = GetOrAddQueue(leveledtask.QueueId, leveledtask.IsHighPriority);
            Q.Enqueue(task);

            ProcessWork(leveledtask.QueueId);   
        }

        private void ProcessUnleveledTask(Task task)
        {

            // regular tasks go directly to the thread pool
            ThreadPool.UnsafeQueueUserWorkItem(w =>
            {
                try
                {
                    base.TryExecuteTask(task);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(string.Format("Task Executer: Error Executing Unleveled Task {0} {1}", e.Message, e.StackTrace), "error");
                }

            }, null);
        }

        private ILeveledTask TaskAsLeveledTask(Task task)
        {
            return task as ILeveledTask;
        }

        private void ProcessWork(string QueueId)
        {
            ThreadPool.UnsafeQueueUserWorkItem(w => {
                var oLock = GetOrAddLock(QueueId);

                bool bGotLock = false;
                Monitor.TryEnter(oLock, ref bGotLock);

                var hQ = GetOrAddQueue(QueueId, true);
                var lQ = GetOrAddQueue(QueueId, false);

                if (0 == hQ.Count && 0 == lQ.Count)
                    return; // was already completed.

                if (!bGotLock) // a thread from the thread pool is already looping on the Q. 
                {
                    //Trace.WriteLine(string.Format("Scheduler attempt to acquire lock on {0} and failed", QueueId), "info");
                    return;    // at any point of time ONLY one thread is allwoed to dequeue on the Q to enable order tasks
                }


                var ExecutedTasks = 0;
                
                
                while (
                            // should yield
                            ExecutedTasks <= m_MaxTaskBeforeYield ||
                            // don't yeild if we have only one queue.
                            (ExecutedTasks > m_MaxTaskBeforeYield  && m_HiQueues.Count + m_LowQueues.Count == 2)  || 
                            // don't yeild if this queue has been removed, drain it before dropping the reference. 
                            (ExecutedTasks > m_MaxTaskBeforeYield && (!m_HiQueues.ContainsKey(QueueId) && !m_LowQueues.ContainsKey(QueueId) ) ) 
                      ) 
                             
                {


                    if (ExecutedTasks > m_MaxTaskBeforeYield && (!m_HiQueues.ContainsKey(QueueId) && !m_LowQueues.ContainsKey(QueueId)))
                        Trace.WriteLine(string.Format("Queue {0} has been removed. Draining.. (remaining {1} tasks)", QueueId, lQ.Count + hQ.Count), "info");


                    Task leveledTask = null;
                    var bFound = false;

                    // found in High Priority Queue
                    bFound = hQ.TryDequeue(out leveledTask);

                    // found in Low Priority Queue
                    if (!bFound && null == leveledTask)
                        lQ.TryDequeue(out leveledTask);

                    if (!bFound && null == leveledTask) //nothing here to work on
                        break;

                    //{
                    //Trace.WriteLine(string.Format("faild! count {0}/{1} queue {2}", hQ.Count, lQ.Count, QueueId), "info");
                    ///break;
                    //}

                    try
                    {
                        base.TryExecuteTask(leveledTask);
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(string.Format("Task Executer: Error Executing Task {0} {1}", e.Message, e.StackTrace), "error");
                    }

                    ExecutedTasks++;

                }

                if (0 == ExecutedTasks) // we were unsucessfull picking up tasks 
                    Trace.WriteLine(string.Format("Scheduler attempted to execute on queue {0} with count {1} and found 0 tasks", QueueId, lQ.Count + hQ.Count), "info");


                Monitor.Exit(oLock);
             

                

                if ((ExecutedTasks > m_MaxTaskBeforeYield && hQ.Count + lQ.Count > 0))
                {

                    // current thread is about to be released back to the pool (and we still have more as we yielded).
                    // call it back to ensure that remaining tasks will be executed (even if no more tasks are sent to the scheduler). 
                    Trace.WriteLine(string.Format("Queue {0} yielded thread {1} after {2} tasks",
                                                   QueueId,
                                                   Thread.CurrentThread.ManagedThreadId,
                                                   ExecutedTasks - 1));

                    Task.Run(() => { ProcessWork(QueueId);} );
                }



            }, null);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;

            // TODO: future enhancement, execute regular tasks here. 
        }
    }
}
