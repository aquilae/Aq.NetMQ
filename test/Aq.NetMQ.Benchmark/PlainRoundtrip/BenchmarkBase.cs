using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.PlainRoundtrip {
    public abstract class BenchmarkBase : IBenchmark {
        public async Task ExecuteAsync(string[] args) {
            this.Options = this.ParseArgs(args);

            Console.WriteLine($"Benchmarking {nameof (PlainRoundtrip)} with {this.Name} implementation");
            Console.WriteLine($"  Endpoint: {this.Options.Endpoint}");
            Console.WriteLine($"  Message size: {this.Options.MessageSize} [B]");
            Console.WriteLine($"  Roundtrip count: {this.Options.MessageCount}");
            Console.WriteLine();

            using (this.Barrier = new Barrier(3)) { 
                var clientTask = Task.Factory.StartNew(
                    this.RunClient, CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach
                        | TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                var serverTask = Task.Factory.StartNew(
                    this.RunServer, CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach
                        | TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                var stopwatch = new Stopwatch();

                this.SetupBarrier();

                GC.Collect(2, GCCollectionMode.Forced, true);
                stopwatch.Start();

                this.StartBarrier();

                Console.Write($"{nameof (PlainRoundtrip)}::{this.Name} running...");

                this.CompleteBarrier();

                stopwatch.Stop();

                Console.WriteLine(" Completed");

                await serverTask;
                await clientTask;

                var elapsed = stopwatch.ElapsedMilliseconds / 1000d;
                var throughput = this.Options.MessageCount / elapsed;
                var megabits = (throughput * this.Options.MessageSize * 8) / 1000;
                
                Console.WriteLine("  Elapsed time: {0:###0.0###}", elapsed);
                Console.WriteLine("  Mean throughput: {0:###0.0###} [msg/s]", throughput);
                Console.WriteLine("  Mean throughput: {0:###0.0###} [Mb/s]", megabits);
                Console.WriteLine();
            }
        }

        protected abstract string Name { get; }
        protected Options Options { get; private set; }

        protected virtual Options ParseArgs(string[] args) {
            var options = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, options)) {
                throw new Exception("CommandLine parser failed");
            }
            return options;
        }

        protected abstract Task RunServer();

        protected void SetupBarrier() => this.Barrier.SignalAndWait();
        protected void StartBarrier() => this.Barrier.SignalAndWait();
        protected void CompleteBarrier() => this.Barrier.SignalAndWait();

        private Barrier Barrier { get; set; }

        private void RunClient() {
            var endpoint = this.Options.Endpoint;
            var messageSize = this.Options.MessageSize;
            var messageCount = this.Options.MessageCount;

            var msg = new Msg();
            msg.InitGC(new byte[messageSize], messageSize);

            using (var socket = new PairSocket()) {
                this.SetupBarrier();

                socket.Connect(endpoint);

                this.StartBarrier();

                for (var i = 0; i < messageCount; ++i) {
                    socket.Send(ref msg, false);
                    socket.Receive(ref msg);

                    if (msg.HasMore) {
                        throw new Exception("unexpected ZMQ_RCVMORE");
                    }

                    if (msg.Size != messageSize) {
                        throw new Exception("received message of invalid size");
                    }
                }

                socket.Close();
            }

            msg.Close();

            this.CompleteBarrier();
        }
    }
}
