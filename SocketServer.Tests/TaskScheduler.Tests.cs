using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using WebSocketServer.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;

namespace WebSocketServer.Tests
{
    [TestClass]
    public class TaskSchedulerTest
    {
        class TaskParameters
        {
            public string Q;
            public int Seq;
        }
        private SequentialLeveledTaskScheduler scheduler = new SequentialLeveledTaskScheduler();
        private Random rnd = new Random();

        [TestMethod]
        [TestCategory("Scheduler.Basic")]
        public void Task_T()
        {
            var maxQueue = 10;
            var maxTasks = 5000;
            var maxSleep = 10;
            var IsHighPrority = false;
            var result = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
            var sw = new Stopwatch();
            sw.Start();


            var Tasks = new List<Task<long>>();


            for (var qNum = 1; qNum <= maxQueue; qNum++)
            {
                var qName = string.Concat("Q", qNum);
                for (var Seq = 1; Seq < maxTasks / maxQueue; Seq++)
                {
                    var taskP = new TaskParameters() { Q = qName, Seq = Seq };

                    LeveledTask<long> lt = new LeveledTask<long>( (param) =>
                    {
                        // this force the new task not to be inlined. 

                        var p = param as TaskParameters;

                        var resultQ = result.GetOrAdd(p.Q, new ConcurrentDictionary<string, long>());
                        resultQ.AddOrUpdate(string.Concat("S", p.Seq), DateTime.UtcNow.Ticks, (key, current) => { throw new InvalidOperationException("Found existing key"); });
                        //Trace.WriteLine(string.Format("Task {0} on Queue {1} - Start", p.Seq, p.Q), "info");
                        Thread.Sleep(maxSleep);
                        //Trace.WriteLine(string.Format("Task {0} on Queue {1} - End", p.Seq, p.Q), "info");
                        resultQ.AddOrUpdate(string.Concat("E", p.Seq), DateTime.UtcNow.Ticks, (key, current) => { throw new InvalidOperationException("Found existing key"); });

                        return DateTime.Now.Ticks;
                    },
                    taskP);

                    lt.QueueId = qName;
                    lt.IsHighPriority = IsHighPrority;
                    lt.Start(scheduler);
                    Tasks.Add(lt);
                }
            }


            Task.WhenAll(Tasks).Wait();

            foreach (var task in Tasks)
                Trace.WriteLine(String.Format("Task result was:{0} ", task.Result));

            sw.Stop();
            Trace.WriteLine(string.Format("{0} Tasks on {1} Queues - Executed in {2} sec", maxTasks, maxQueue, sw.Elapsed.Seconds), "info");
            verify(ref result);
        }



        [TestMethod]
        [TestCategory("Scheduler.Basic")]
        public void AsyncAsync()
        {
            var maxQueue = 10;
            var maxTasks = 5000;
            var maxSleep = 10;
            var IsHighPrority = false;
            var result = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
            var sw = new Stopwatch();
            sw.Start();


            var Tasks = new List<Task>();


            for (var qNum = 1; qNum <=  maxQueue; qNum++)
            {
                var qName = string.Concat("Q", qNum);
                for (var Seq = 1; Seq < maxTasks / maxQueue; Seq++)
                {
                    var taskP = new TaskParameters() { Q = qName, Seq = Seq };

                    LeveledTask lt = new LeveledTask(async (param) =>
                    {
                        // this force the new task not to be inlined. 

                        await Task.Factory.StartNew(async () => { await Task.Delay(100); }, TaskCreationOptions.LongRunning );
                        var p = param as TaskParameters;

                        var resultQ = result.GetOrAdd(p.Q, new ConcurrentDictionary<string, long>());
                        resultQ.AddOrUpdate(string.Concat("S", p.Seq), DateTime.UtcNow.Ticks, (key, current) => { throw new InvalidOperationException("Found existing key"); });
                        //Trace.WriteLine(string.Format("Task {0} on Queue {1} - Start", p.Seq, p.Q), "info");
                        Thread.Sleep(maxSleep);
                        //Trace.WriteLine(string.Format("Task {0} on Queue {1} - End", p.Seq, p.Q), "info");
                        resultQ.AddOrUpdate(string.Concat("E", p.Seq), DateTime.UtcNow.Ticks, (key, current) => { throw new InvalidOperationException("Found existing key"); });
                        
                    },
                    taskP);

                    lt.QueueId = qName;
                    lt.IsHighPriority = IsHighPrority;
                    lt.Start(scheduler);
                    Tasks.Add(lt);
                }
            }


            Task.WhenAll(Tasks).Wait();
            sw.Stop();
            Trace.WriteLine(string.Format("{0} Tasks on {1} Queues - Executed in {2} sec", maxTasks, maxQueue, sw.Elapsed.Seconds), "info");
            //verify(ref result);                                 

            // tasks that uses normal start.run will lose order in the queue. 
            // the only way that tasks maintain order is to by using 
            // leveled tasks to create sub tasks. 

        }


        [TestMethod]
        [TestCategory("Scheduler.Basic")]
        public void NormalizedSingleQ()
        {
            var maxQueue = 1;
            var maxTasks = 500;
            var maxSleep = 10;
            var IsHighPrority = false;    
            var result = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
            var sw = new Stopwatch();
            sw.Start();
            var Tasks = createTasks(ref result,
                                        maxTasks,
                                        maxQueue,
                                        maxSleep,
                                        IsHighPrority);
            Task.WhenAll(Tasks).Wait();
            sw.Stop();
            Trace.WriteLine(string.Format("{0} Tasks on {1} Queues - Executed in {2} sec", maxTasks, maxQueue, sw.Elapsed.Seconds), "info");
            verify(ref result);
        }

        
        [TestMethod]
        [TestCategory("Scheduler.Basic")]
        public void NormalizedMultiQ()
        {
            var maxQueue = 5; // the lower the # the highest yield messages you will get (we randomize between Qs it is not round robin)
            var maxTasks = 1000;
            var maxSleep = 10; // the higher the # the higher yeilds you will get. 
            var IsHighPrority = false;
            var result = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
            var sw = new Stopwatch();
            sw.Start();
            var Tasks = createTasks(ref result,
                                        maxTasks,
                                        maxQueue,
                                        maxSleep,
                                        IsHighPrority);
            Task.WhenAll(Tasks).Wait();
            sw.Stop();
            Trace.WriteLine(string.Format("{0} Tasks on {1} Queues - Executed in {2} sec", maxTasks, maxQueue, sw.Elapsed.Seconds), "info");
            verify(ref result);
        }




        [TestMethod]
        [TestCategory("Scheduler.Common")]
        public void NonNormalizedSingleQ()
        {
            var maxQueue = 1; // the lower the # the highest yield messages you will get (we randomize between Qs it is not round robin)
            var maxTasks = 1000;
            var maxSleep = 10; // the higher the # the higher yeilds you will get. 
            var IsHighPrority = false;
            var IsRandomPriroty = true;
            var result = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
            var sw = new Stopwatch();
            sw.Start();
            var Tasks = createTasks(ref result,
                                        maxTasks,
                                        maxQueue,
                                        maxSleep,
                                        IsHighPrority, 
                                        IsRandomPriroty);
            Task.WhenAll(Tasks).Wait();
            sw.Stop();
            Trace.WriteLine(string.Format("{0} Tasks on {1} Queues - Executed in {2} sec", maxTasks, maxQueue, sw.Elapsed.Seconds), "info");
            verify(ref result);
        }


        [TestMethod]
        [TestCategory("Scheduler.Common")]
        public void NonNormalizedMultiQ()
        {
            var maxQueue = 50; // the lower the # the highest yield messages you will get (we randomize between Qs it is not round robin)
            var maxTasks = 5000;
            var maxSleep = 100; // the higher the # the higher yeilds you will get. 
            var IsHighPrority = false;
            var IsRandomPriroty = true;
            var result = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
            var sw = new Stopwatch();
            sw.Start();
            var Tasks = createTasks(ref result,
                                        maxTasks,
                                        maxQueue,
                                        maxSleep,
                                        IsHighPrority,
                                        IsRandomPriroty);
            Task.WhenAll(Tasks).Wait();
            sw.Stop();
            Trace.WriteLine(string.Format("{0} Tasks on {1} Queues - Executed in {2} sec", maxTasks, maxQueue, sw.Elapsed.Seconds), "info");
            verify(ref result);
        }



        [TestMethod]
        [TestCategory("Scheduler.Common")]
        public void LargeNumOfQueue()
        {
            var maxQueue = 1000; // the lower the # the highest yield messages you will get (we randomize between Qs it is not round robin)
            var maxTasks = 5000;
            var maxSleep = 100; // the higher the # the higher yeilds you will get. 
            var IsHighPrority = false;
            var IsRandomPriroty = true;
            var result = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
            var sw = new Stopwatch();
            sw.Start();
            var Tasks = createTasks(ref result,
                                        maxTasks,
                                        maxQueue,
                                        maxSleep,
                                        IsHighPrority,
                                        IsRandomPriroty);
            Task.WhenAll(Tasks).Wait();
            sw.Stop();
            Trace.WriteLine(string.Format("{0} Tasks on {1} Queues - Executed in {2} sec", maxTasks, maxQueue, sw.Elapsed.Seconds), "info");
            verify(ref result);
        }


        

        [TestMethod]
        [TestCategory("Scheduler.Advanced")]
        public void DrainInBetween()
        {
            var maxQueue = 5; // the lower the # the highest yield messages you will get (we randomize between Qs it is not round robin)
            var maxTasks = 2000;
            var maxSleep = 10; // the higher the # the higher yeilds you will get. 
            var IsHighPrority = false;
            var IsRandomPriroty = false;
            var result = new ConcurrentDictionary<string, ConcurrentDictionary<string, long>>();
            var sw = new Stopwatch();
            sw.Start();
            var Tasks = createTasks(ref result,
                                        maxTasks,
                                        maxQueue,
                                        maxSleep,
                                        IsHighPrority,
                                        IsRandomPriroty);


            scheduler.RemoveQueue("Q" + maxQueue);

            var Tasks2 = createTasks(ref result,
                                        maxTasks,
                                        maxQueue - 1,
                                        maxSleep,
                                        IsHighPrority,
                                        IsRandomPriroty,
                                        1000000);
                           





            Task.WhenAll(Tasks).Wait();
            Task.WhenAll(Tasks2).Wait();

            sw.Stop();
            Trace.WriteLine(string.Format("{0} Tasks on {1} Queues - Executed in {2} sec", maxTasks *2 , maxQueue, sw.Elapsed.Seconds), "info");
            verify(ref result);
        }


        private List<LeveledTask> createTasks(ref ConcurrentDictionary<string, ConcurrentDictionary<string, long>> allresult,
                                            int maxTasks,
                                            int MaxQueue,
                                            int MaxSleep, 
                                            bool isHighPirotiy,
                                            bool randomPrority = false,
                                            int seedID = 0)
        {
            var sw = new Stopwatch();
            sw.Start();

            var highProrityTasks = 0;
            var normalProrityTasks = 0;
            var Tasks = new List<LeveledTask>();

            for (var i = seedID + 1; i <= maxTasks + seedID; i++)
            {
                var q = string.Concat("Q", rnd.Next(1, MaxQueue));
                var taskP = new TaskParameters() { Q = q, Seq = i };

                var bIsHighPrority = (isHighPirotiy || (randomPrority && rnd.Next(1, int.MaxValue) % 2 == 0));

                if (bIsHighPrority)
                    highProrityTasks++;
                else
                    normalProrityTasks++;

               // Trace.WriteLine(string.Format("task {0} created on queue {1}", i, q), "info-test");

                    var lt = addTask(ref allresult, taskP, q, rnd.Next(1, MaxSleep), bIsHighPrority);

                Tasks.Add(lt);
            }

            sw.Stop();
            Trace.WriteLine(string.Format("Created N:{0}/H:{1} tasks in {2} ms", normalProrityTasks, highProrityTasks, sw.ElapsedMilliseconds), "info");
            return Tasks;
        }
        private LeveledTask addTask(ref ConcurrentDictionary<string, ConcurrentDictionary<string, long>>  allresult , 
                            TaskParameters taskP, 
                            string q,
                            int maxSleep, 
                            bool IsHighPrority = false)
        {

            var result = allresult;

            LeveledTask lt = new LeveledTask((param) =>
            {
                var p = param as TaskParameters;

                var resultQ = result.GetOrAdd(p.Q, new ConcurrentDictionary<string, long>());
                resultQ.AddOrUpdate(string.Concat("S", p.Seq), DateTime.UtcNow.Ticks, (key, current) => { throw new InvalidOperationException("Found existing key"); });
                //Trace.WriteLine(string.Format("Task {0} on Queue {1} - Start", p.Seq, p.Q), "info");
                Thread.Sleep(maxSleep);
                //Trace.WriteLine(string.Format("Task {0} on Queue {1} - End", p.Seq, p.Q), "info");
                resultQ.AddOrUpdate(string.Concat("E", p.Seq), DateTime.UtcNow.Ticks, (key, current) => { throw new InvalidOperationException("Found existing key"); });
            },
                       taskP);


            lt.QueueId = q;
            lt.IsHighPriority = IsHighPrority; 
            lt.Start(scheduler);

            return lt;
        }


        private void verify(ref ConcurrentDictionary<string, ConcurrentDictionary<string, long>> allresult)
        {
            // results should be sequential 
            // ie S1 start S1 end before S1+n start and end

            foreach (var queuePair in allresult.ToArray())
            {
                var qName = queuePair.Key;
                var q = queuePair.Value; // value is ConcurrentDictionary<string, long>

                var qEntries = q.ToArray();
                var startEntries = qEntries.Where(  kvp => kvp.Key[0] == 'S' );

                // for each start task look for
                   // a task that started (between this start-end). 
                    // a task that ended (between this start-end). 
                // must not use Task sequence (as order) because tasks.
                // are placed in the actual queue based on priority. 
                // slow stuff...
                foreach (var startEntry in startEntries)
                {
                    var TaskSeq = startEntry.Key.Substring(1, startEntry.Key.Length -1 );

                    var endEntry=
                        qEntries.SingleOrDefault(kvp => kvp.Key == string.Concat("E", TaskSeq));

                    var foundTask = qEntries.Where(
                                    kvp =>
                                    (
                                    // can use one comparison without key
                                    // but kept this way to be able to spit 
                                    // out decent fail error if needed.
                                        kvp.Key[0] == 'S' &&
                                        kvp.Value > startEntry.Value &&
                                        kvp.Value < endEntry.Value
                                        ||
                                        kvp.Key[0] == 'E' &&
                                        kvp.Value > startEntry.Value &&
                                        kvp.Value < endEntry.Value
                                    )                        
                            );

                    if (0 != foundTask.Count())
                        Assert.Fail(string.Format("Q:{0} has a task started or ended while Task {1} was running",
                            qName, TaskSeq));
                }   
            }               
         }
    }
}
