using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.HttpGet {
    public abstract class BenchmarkBase : IBenchmark {
        public async Task ExecuteAsync(string[] args) {
            this.Options = this.ParseArgs(args);

            Console.WriteLine($"Benchmarking {nameof (HttpGet)} with {this.Name} implementation");
            Console.WriteLine($"  Endpoint: {this.Options.Endpoint}");
            Console.WriteLine($"  Message size: {this.Options.MessageSize} [B]");
            Console.WriteLine($"  Roundtrip count: {this.Options.MessageCount}");
            Console.WriteLine();

            using (this.Barrier = new Barrier(4)) {
                var httpTask = Task.Factory.StartNew(
                    this.RunHttpServer, CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach
                        | TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

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

                Console.Write($"{nameof (HttpGet)}::{this.Name} running...");

                this.CompleteBarrier();

                stopwatch.Stop();

                Console.WriteLine(" Completed");

                await serverTask;
                await clientTask;
                await httpTask;

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
        protected int HttpPort { get; private set; }
        protected Options Options { get; private set; }

        protected virtual Options ParseArgs(string[] args) {
            var options = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, options)) {
                throw new Exception("CommandLine parser failed");
            }
            return options;
        }

        protected abstract Task RunServer();

        protected void SetupBarrier() => this.SignalBarrier("setup");
        protected void StartBarrier() => this.SignalBarrier("start");
        protected void CompleteBarrier() => this.SignalBarrier("complete");

        private void SignalBarrier(string name) {
            //Console.WriteLine($"Signalling {name}");
            this.Barrier.SignalAndWait();
        }

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

        private void RunHttpServer() {
            var messageCount = this.Options.MessageCount;
            var messageSize = this.Options.MessageSize;
            var httpRoundtripCount = this.Options.HttpRoundtripCount;
            var iterationCount = messageCount * httpRoundtripCount;
            var buffer = new byte[messageSize + 1];

            this.HttpPort = GetRandomTcpPort();
            RegisterWithNetsh($"http://127.0.0.1:{this.HttpPort}/");

            try {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{this.HttpPort}/");

                listener.Start();
                try {
                    this.SetupBarrier();
                    this.StartBarrier();

                    for (var j = 0; j < iterationCount; ++j) {
                        var ctx = listener.GetContext();

                        if (messageSize != ctx.Request.InputStream.Read(buffer, 0, messageSize + 1)) {
                            throw new Exception("received message of invalid size");
                        }

                        ctx.Response.StatusCode = 200;
                        ctx.Response.OutputStream.Write(buffer, 0, messageSize);
                        ctx.Response.Close();
                    }

                    this.CompleteBarrier();
                }
                finally {
                    listener.Stop();
                }
            }
            finally {
                UnregisterWithNetsh($"http://127.0.0.1:{this.HttpPort}/");
            }
        }

        private static int GetRandomTcpPort() {
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 0);
            listener.Start();

            try {
                return ((IPEndPoint) listener.LocalEndpoint).Port;
            }
            finally {
                listener.Stop();
            }
        }

        private static void RegisterWithNetsh(string address) {
            var user = Environment.UserName;
            var domain = Environment.UserDomainName;

            var args = $"http add urlacl url={address} user={domain}\\{user}";

            var psi = new ProcessStartInfo("netsh", args) {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            // ReSharper disable once PossibleNullReferenceException
            Process.Start(psi).WaitForExit();
        }

        private static void UnregisterWithNetsh(string address) {
            var user = Environment.UserName;
            var domain = Environment.UserDomainName;

            var args = $"http delete urlacl url={address}";

            var psi = new ProcessStartInfo("netsh", args) {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            // ReSharper disable once PossibleNullReferenceException
            Process.Start(psi).WaitForExit();
        }
    }
}
