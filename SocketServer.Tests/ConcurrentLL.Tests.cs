using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SocketServer.Utils;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


//TODO: use tick instead of random values 
// for producer consumer tests. 

namespace SocketServer.Tests
{
    [TestClass]
    public class ConcurrentLLTests
    {
        class BOX<T>
        {
            public T VALUE;
            public BOX(T val)
            {
                VALUE = val;
            }
        }



        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Basic")]
        public void AddAtTailCheckCount()
        {
            var LL = new ConcurrentLinkedList<BOX<int>>();
            var n = Enumerable.Range(0, 100);
            foreach (var i in n)
                LL.AddTail(new BOX<int>(i));
           
            Trace.WriteLine(string.Format("Total Entries{0}", LL.Count), "info");
            Assert.AreEqual(LL.Count, n.Count());
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Basic")]
        public void AddAtHeadCheckCount()
        {
            var LL = new ConcurrentLinkedList<BOX<int>>();
            var n = Enumerable.Range(0, 100);
            foreach (var i in n)
                LL.AddHead(new BOX<int>(i));

            Trace.WriteLine(string.Format("Total Entries{0}", LL.Count), "info");
            Assert.AreEqual(LL.Count, n.Count());
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Basic")]
        public void AddAtHeadandTailCheckCount()
        {
            var LL = new ConcurrentLinkedList<BOX<int>>();
            var n = Enumerable.Range(0, 100);
            foreach (var i in n)
                LL.AddHead(new BOX<int>(i));

            foreach (var i in n)
                LL.AddHead(new BOX<int>(i));

            Trace.WriteLine(string.Format("Total Entries{0}", LL.Count), "info");
            Assert.AreEqual(LL.Count, n.Count() * 2);
        }


        #region add - check entries
        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Common")]

        public void AddTailCheckEntries()
        {
            var entriesCount = 100;
            var removedEntries = 0;
            BOX<int> currentNode = null;

            var LL = AddCheckEntries(true, entriesCount);

            currentNode = LL.RemoveTail();
            do
            {
                if (null != currentNode)
                    removedEntries++;
            }

            while (null != (currentNode = LL.RemoveTail()));
            Assert.AreEqual(entriesCount, removedEntries);
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Common")]

        public void AddHeadCheckEntries()
        {
            var entriesCount = 100;
            var removedEntries = 0;
            BOX<int> currentNode = null;

            var LL = AddCheckEntries(false, entriesCount);

            currentNode = LL.RemoveHead();
            do
            {
                if (null != currentNode)
                    removedEntries++;
            }

            while (null != (currentNode = LL.RemoveHead()));
            Assert.AreEqual(entriesCount, removedEntries);
        }
        #endregion

        private ConcurrentLinkedList<BOX<int>> AddCheckEntries(bool bTail = true, int countEntries = 10)
        {
            var LL = new ConcurrentLinkedList<BOX<int>>();
            var n = new List<int>(Enumerable.Range(0, countEntries ));
   
            foreach (var i in n)
            {
                if(bTail)
                    LL.AddTail(new BOX<int>(i));
                else
                    LL.AddHead(new BOX<int>(i));
            }

            
            var nFound = 0;
            var node = LL.Head;

            do
            {
                Assert.AreNotEqual(node.Value, null);
                Assert.AreEqual(true, n.Contains(node.Value.VALUE));
                nFound++;
            }
            while ((node = node.Next) != null);

            Trace.WriteLine(string.Format("Total Entries {0}", LL.Count), "info");
            Assert.AreEqual(nFound, n.Count);

            return LL;
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Common")]
        public void AddAtTailRemoveHead()
        {
            var expectedCount = 100;
            var currentCount = 0;
            var AddAtTail = true;
            var LL = AddCheckEntries(AddAtTail, expectedCount);
            BOX<int> node;
            var TopEntry = 0;
            while (null != (node = LL.RemoveHead()))
            {
                Assert.AreNotEqual(null, node);

                Assert.AreEqual(TopEntry, node.VALUE);
                TopEntry++;
                currentCount++;
            }

            Assert.AreEqual(expectedCount, currentCount);
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Common")]
        public void AddAtHeadRemoveHead()
        {
            var expectedCount = 100;
            var currentCount = 0;
            var AddAtTail = false;
            var LL = AddCheckEntries(AddAtTail, expectedCount);
            BOX<int> node;
            var TopEntry = expectedCount - 1;
            while (null != (node = LL.RemoveHead()))
            {
                Assert.AreNotEqual(null, node);
                Assert.AreEqual(TopEntry, node.VALUE);
                TopEntry--;
                currentCount++;
            }
            Assert.AreEqual(expectedCount, currentCount);
        }



        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Common")]
        public void AddAttailRemovetail()
        {
            var expectedCount = 100;
            var currentCount = 0;
            var AddAtTail = true;
            var LL = AddCheckEntries(AddAtTail, expectedCount);
            BOX<int> node;
            var TopEntry = expectedCount - 1;
            while (null != (node = LL.RemoveTail()))
            {
                
                Assert.AreNotEqual(null, node);
                Assert.AreEqual(TopEntry, node.VALUE);
                TopEntry--;
               currentCount++;
            }
            Assert.AreEqual(expectedCount, currentCount);
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void SingleTail_P_SingleHead_C()
        {

            int countEntries = 100;
            int TailProducers = 1;
            int TailConsumers = 0;
            int HeadProducers = 0;
            int HeadConsumers = 1;
            int MaxProducerRandomDelay = 10;
            int MaxConsumersRandomDelay = 10;
            int InitialConsumerDelay = 10; // gives a producer a chance to do stuff
            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );
                
            }




        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void MultiTail_P_SingleHead_C()
        {

            int countEntries = 1000;
            int TailProducers = 5;
            int TailConsumers = 0;
            int HeadProducers = 0;
            int HeadConsumers = 1;
            int MaxProducerRandomDelay = 10;
            int MaxConsumersRandomDelay = 5;
            int InitialConsumerDelay = 10; // gives a producer a chance to do stuff

            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );

        }



        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void MultiTail_P_C_SingleHead_C()
        {

            int countEntries = 2500;
            int TailProducers = 10;
            int TailConsumers = 10;
            int HeadProducers = 0;
            int HeadConsumers = 1;
            int MaxProducerRandomDelay = 10;
            int MaxConsumersRandomDelay = 5;
            int InitialConsumerDelay = 5; // gives a producer a chance to do stuff

            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );
        }




        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void MultiTail_P_MultiHead_C()
        {

            int countEntries = 1000;
            int TailProducers = 5;
            int TailConsumers = 0;
            int HeadProducers = 0;
            int HeadConsumers = 5;
            int MaxProducerRandomDelay = 10;
            int MaxConsumersRandomDelay = 5;
            int InitialConsumerDelay = 10; // gives a producer a chance to do stuff

            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void MultiTail_P_MultiHead_P_C()
        {

            int countEntries = 3000;
            int TailProducers = 16;
            int TailConsumers = 0;
            int HeadProducers = 16;
            int HeadConsumers = 16;
            int MaxProducerRandomDelay = 5;
            int MaxConsumersRandomDelay = 15;
            int InitialConsumerDelay = 10; // gives a producer a chance to do stuff

            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void MultiTail_P_C_MultiHead_P_C()
        {

            int countEntries = 1000;
            int TailProducers = 8;
            int TailConsumers = 8;
            int HeadProducers = 8;
            int HeadConsumers = 8;
            int MaxProducerRandomDelay = 10;
            int MaxConsumersRandomDelay = 5;
            int InitialConsumerDelay = 10; // gives a producer a chance to do stuff

            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );
        }



        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void SingleTail_C_MultiHead_P()
        {

            int countEntries = 2000;
            int TailProducers = 0;
            int TailConsumers = 1;
            int HeadProducers = 10;
            int HeadConsumers = 0;
            int MaxProducerRandomDelay = 10;
            int MaxConsumersRandomDelay = 5;
            int InitialConsumerDelay = 10; // gives a producer a chance to do stuff

            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );
        }


        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void MultiTail_C_MultiHead_P()
        {

            int countEntries = 2000;
            int TailProducers = 0;
            int TailConsumers = 10;
            int HeadProducers = 10;
            int HeadConsumers = 0;
            int MaxProducerRandomDelay = 10;
            int MaxConsumersRandomDelay = 5;
            int InitialConsumerDelay = 10; // gives a producer a chance to do stuff

            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );
        }

        [TestMethod]
        [TestCategory("ConcurrentLinkedList.Concurrent")]
        public void SingleTail_C_SingleHead_P()
        {

            int countEntries = 1000;
            int TailProducers = 0;
            int TailConsumers = 1;
            int HeadProducers = 1;
            int HeadConsumers = 0;
            int MaxProducerRandomDelay = 10;
            int MaxConsumersRandomDelay = 5;
            int InitialConsumerDelay = 10; // gives a producer a chance to do stuff

            CreateProducersConsumers(
                countEntries,
                TailProducers,
                TailConsumers,
                HeadProducers,
                HeadConsumers,
                MaxProducerRandomDelay,
                MaxConsumersRandomDelay,
                InitialConsumerDelay
                );
        }

        private ConcurrentLinkedList<BOX<long>> CreateProducersConsumers(int countEntries, 
                                                                           int TailProducers, 
                                                                           int TailConsumers, 
                                                                           int HeadProducers, 
                                                                           int HeadConsumers,
                                                                           int MaxProducerRandomDelay, 
                                                                           int MaxConsumersRandomDelay,
                                                                           int InitialConsumerDelay = 10)
        {


            var currentEntry = 0;
            var totalProduced = 0;
            var totalConsumed = 0;
            var LL = new ConcurrentLinkedList<BOX<long>>();
            var rnd = new Random();

            List<Task<int>> consumerTasks = new List<Task<int>>();
            List<Task<int>> producerTasks = new List<Task<int>>();


            Func<bool, string ,Task<int>> producer = async (bIsTail, producerId) =>
            {
                
                var produced = 0;
                Trace.WriteLine(string.Format("Producer {0} is {1}", producerId, bIsTail ? "Tail" : "Head"));


                while (Interlocked.Increment(ref currentEntry) <= countEntries)
                {
                    produced++;
                    var newVal = DateTime.UtcNow.Ticks;
                    Trace.WriteLine(string.Format("Producer {0} + {1}", producerId, newVal));

                    if (bIsTail)
                        LL.AddTail(new BOX<long>(newVal));
                    else
                        LL.AddHead(new BOX<long>(newVal));

                    
                    await Task.Delay(rnd.Next(0, MaxProducerRandomDelay));
                }
                return produced;
            };

            
            Func<bool, string, Task<int>> consumer = async (bIsTail, consumerId) =>
            {
                // all consumer wait a bit this almost elminate the need for that CX below 
                await Task.Delay(InitialConsumerDelay); // 
                Trace.WriteLine(string.Format("Consumer {0} is {1}", consumerId, bIsTail ?  "Tail" : "Head"  ));
                var consumed = 0;
                BOX<long> node; 
                while ( (bIsTail && null != (node = LL.RemoveTail()))  || (!bIsTail && null != (node = LL.RemoveHead())) )
                {
                    Assert.AreNotEqual(null, node.VALUE);
                    Trace.WriteLine(string.Format("Consumer {0} - {1}", consumerId, node.VALUE));
                    consumed++;
                    await Task.Delay(rnd.Next(0, MaxConsumersRandomDelay));
                }

                Trace.WriteLine(string.Format("Consumer {0} is {1} -- Exited", consumerId, bIsTail ? "Tail" : "Head"));
                return consumed;
            };


            // give a head start for consumers.  
            for (int p = 1; p <= TailProducers + HeadProducers; p++)
            {
                //avoid hoisted variables. 
                var isTail = (TailProducers > 0 && p <= TailProducers);
                var producerName = string.Concat("P", p);
                producerTasks.Add(Task.Run(
                            async () => await producer(isTail, producerName))
                          );
            }

            for (int c = 1; c <= TailConsumers + HeadConsumers; c++)
            {
                var isTail = (TailConsumers > 0 && c <= TailConsumers);
                var consumerName = string.Concat("C", c);
                consumerTasks.Add(Task.Run(
                            async () => await consumer(isTail, consumerName))
                          );
            }
            

            Task.WhenAll(producerTasks).Wait();
            Task.WhenAll(consumerTasks).Wait();


            

            // if after all said and done we still have items consume them. 
            if (LL.Count > 0)
            {
                Trace.WriteLine("Consumers exited before producers completed work, creating a CX", "warnings");
                totalConsumed += Task<int>.Run(async () => await consumer(true, "CX")).Result;
            }

            // calculate all produced and consumed

            foreach (var p in producerTasks)
                totalProduced += p.Result;

            foreach (var c in consumerTasks)
                totalConsumed += c.Result;

            // are we clean?
            Trace.WriteLine(string.Format("completed produced:{0} consumed:{1}", totalProduced, totalConsumed), "info");
            Assert.AreEqual( totalProduced, totalConsumed);
            return LL;
        }

    }
}
