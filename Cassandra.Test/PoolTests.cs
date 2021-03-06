﻿using System;
using System.Collections.Generic;
using System.Text;
using Dev;
using System.Net;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MyUTExt;

namespace Cassandra.Test
{
    public class PoolCompressionTests : PoolTestsBase
    {
        public PoolCompressionTests()
            : base(true)
        {
        }
    }

    public class PoolNoCompressionTests : PoolTestsBase
    {
        public PoolNoCompressionTests()
            : base(false)
        {
        }
    }

    public class PoolTestsBase : IDisposable
    {
        bool _compression = true;
        CompressionType Compression
        {
            get
            {
                return _compression ? CompressionType.Snappy : CompressionType.NoCompression;
            }
        }

        public PoolTestsBase(bool compression)
        {
            _compression = compression;
        }

        Session Session;
        string Keyspace = "";

        public void SetFixture(Dev.SettingsFixture setFix)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");

            var clusterb = Cluster.Builder().AddContactPoint("cassi.cloudapp.net");
            if (_compression)
                clusterb.WithCompression(CompressionType.Snappy);
            var cluster = clusterb.Build();
            Session = cluster.Connect(this.Keyspace);
        }

        public void Dispose()
        {
            Session.Dispose();
        }

        [Fact]
        public void ParallelInsertTest()
        {
            Console.WriteLine("Compression is:"+(Compression== CompressionType.Snappy?"SNAPPY":"OFF"));

            string keyspaceName = "keyspace" + Guid.NewGuid().ToString("N").ToLower();

            Session.Execute(
            string.Format(@"CREATE KEYSPACE {0} 
         WITH replication = {{ 'class' : 'SimpleStrategy', 'replication_factor' : 1 }};"
                , keyspaceName));

            Session.ChangeKeyspace(keyspaceName);

            string tableName = "table" + Guid.NewGuid().ToString("N").ToLower();
            try
            {
                Session.Execute(string.Format(@"CREATE TABLE {0}(
         tweet_id uuid,
         author text,
         body text,
         isok boolean,
         PRIMARY KEY(tweet_id))", tableName));
            }
            catch (AlreadyExistsException)
            {
            }
            Randomm rndm = new Randomm();
            int RowsNo = 300;
            IAsyncResult[] ar = new IAsyncResult[RowsNo];
            List<Thread> threads = new List<Thread>();
            object monit = new object();
            int readyCnt = 0;
            Console.WriteLine();
            Console.WriteLine("Preparing...");

            for (int idx = 0; idx < RowsNo; idx++)
            {

                var i = idx;
                threads.Add(new Thread(() =>
                {
                    try
                    {
                        Console.Write("+");
                        lock (monit)
                        {
                            readyCnt++;
                            Monitor.Wait(monit);
                        }

                        ar[i] = Session.BeginExecute(string.Format(@"INSERT INTO {0} (
         tweet_id,
         author,
         isok,
         body)
VALUES ({1},'test{2}',{3},'body{2}');", tableName, Guid.NewGuid().ToString(), i, i % 2 == 0 ? "false" : "true")
                       , ConsistencyLevel.Default, null, null);
                        Thread.MemoryBarrier();
                    }
                    catch
                    {
                        Console.Write("@");
                    }

                }));
            }

            for (int idx = 0; idx < RowsNo; idx++)
            {
                threads[idx].Start();
            }

            lock (monit)
            {
                while (true)
                {
                    if (readyCnt < RowsNo)
                    {
                        Monitor.Exit(monit);
                        Thread.Sleep(100);
                        Monitor.Enter(monit);
                    }
                    else
                    {
                        Monitor.PulseAll(monit);
                        break;
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Start!");

            HashSet<int> done = new HashSet<int>();
            while (done.Count < RowsNo)
            {
                for (int i = 0; i < RowsNo; i++)
                {
                    Thread.MemoryBarrier();
                    if (!done.Contains(i) &&  ar[i] != null)
                    {
                        if (ar[i].AsyncWaitHandle.WaitOne(10))
                        {
                            try
                            {
                                Session.EndExecute(ar[i]);
                            }
                            catch
                            {
                                Console.Write("!");
                            }
                            done.Add(i);
                            Console.Write("-");
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Inserted... now we are checking the count");

            using (var ret = Session.Execute(string.Format(@"SELECT * from {0} LIMIT {1};", tableName, RowsNo + 100)))
            {
                Assert.Equal(RowsNo, ret.RowsCount);
            }

            Session.Execute(string.Format(@"DROP TABLE {0};", tableName));

            Session.Execute(string.Format(@"DROP KEYSPACE {0};", keyspaceName));

            for (int idx = 0; idx < RowsNo; idx++)
            {
                threads[idx].Join();
            }
         }
    }
}
