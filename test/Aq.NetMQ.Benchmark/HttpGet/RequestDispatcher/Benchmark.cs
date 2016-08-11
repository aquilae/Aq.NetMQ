using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.HttpGet.RequestDispatcher {
    public class Benchmark : BenchmarkBase {
        protected override string Name => "RequestDispatcher";

        protected override HttpGet.Options ParseArgs(string[] args) {
            var options = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, options)) {
                throw new Exception("CommandLine parser failed");
            }
            return options;
        }

        protected override async Task RunServer() {
            int workerThreadMin, completionPortThreadMin;
            ThreadPool.GetMinThreads(out workerThreadMin, out completionPortThreadMin);

            int workerThreadMax, completionPortThreadMax;
            ThreadPool.GetMinThreads(out workerThreadMax, out completionPortThreadMax);

            try {
                ThreadPool.SetMinThreads(
                    this.Options.PoolWorkerThreadsMin == 0
                        ? workerThreadMin : (this.Options.PoolWorkerThreadsMin == -1
                            ? workerThreadMax : this.Options.PoolWorkerThreadsMin),
                    this.Options.PoolCompletionPortThreadsMin == 0
                        ? completionPortThreadMin : (this.Options.PoolCompletionPortThreadsMin == -1
                            ? completionPortThreadMax : this.Options.PoolCompletionPortThreadsMin));

                ThreadPool.SetMaxThreads(
                    this.Options.PoolWorkerThreadsMax == 0
                        ? workerThreadMax : this.Options.PoolWorkerThreadsMax,
                    this.Options.PoolCompletionPortThreadsMax == 0
                        ? completionPortThreadMax : this.Options.PoolCompletionPortThreadsMax);

                int workerThreads, completionPortThreads;

                ThreadPool.GetMinThreads(out workerThreads, out completionPortThreads);
                Console.WriteLine("  ThreadPool.WorkerThreadsMin: {0}", workerThreads);
                Console.WriteLine("  ThreadPool.CompletionPortThreadsMin: {0}", completionPortThreads);

                ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
                Console.WriteLine("  ThreadPool.WorkerThreadsMax: {0}", workerThreads);
                Console.WriteLine("  ThreadPool.CompletionPortThreadsMax: {0}", completionPortThreads);
                Console.WriteLine();

                var endpoint = this.Options.Endpoint;
                var messageSize = this.Options.MessageSize;
                var messageCount = this.Options.MessageCount;
                var httpRoundtripCount = this.Options.HttpRoundtripCount;

                using (var socket = new PairSocket($"@{endpoint}")) {
                    var counter = 0;

                    RequestHandler requestHandler = async ctx => {
                        if (ctx.Request.FrameCount != 1) {
                            throw new Exception("unexpected ZMQ_RCVMORE");
                        }

                        if (ctx.Request.First.MessageSize != messageSize) {
                            throw new Exception("received message of invalid size");
                        }

                        var webClient = new WebClient();
                        var webRequest = ctx.Request.First.ToByteArray();

                        for (var i = 0; i < httpRoundtripCount; ++i) { 
                            var webResponse = webClient.UploadData(
                                $"http://127.0.0.1:{this.HttpPort}",
                                "POST", webRequest);

                            if (webResponse.Length != messageSize) {
                                throw new Exception("received HTTP message of invalid size");
                            }

                            for (var j = 0; j < messageSize; ++j) {
                                if (webResponse[j] != webRequest[j]) {
                                    throw new Exception("received invalid HTTP message");
                                }
                            }
                        }

                        var response = new NetMQMessage(new [] { webRequest });
                        await ctx.SendAsync(response);

                        if (++counter >= messageCount) {
                            ctx.Dispatcher.Running.Complete();
                        }
                    };

                    var dispatcher = new NetMQ.RequestDispatcher(requestHandler);

                    dispatcher.Add(socket);
                    dispatcher.Start();

                    this.SetupBarrier();
                    this.StartBarrier();

                    await dispatcher.Running.Completion;

                    this.CompleteBarrier();
                }
            }
            catch (Exception exc) {
                Console.WriteLine("server exception:\n{0}", exc);
            }
            finally {
                ThreadPool.SetMinThreads(workerThreadMin, completionPortThreadMin);
                ThreadPool.SetMaxThreads(workerThreadMax, completionPortThreadMax);
            }
        }

        private new Options Options => (Options) base.Options;
    }
}
