using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.PlainRoundtrip.RequestDispatcher {
    public class Benchmark : BenchmarkBase {
        protected override string Name => "RequestDispatcher";

        protected override PlainRoundtrip.Options ParseArgs(string[] args) {
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

                var endpoint = this.Options.Endpoint;
                var messageSize = this.Options.MessageSize;
                var messageCount = this.Options.MessageCount;

                using (var socket = new PairSocket($"@{endpoint}")) {
                    var counter = 0;

                    var requestHandler = this.Options.InsertYield
                        ? (RequestHandler) (async ctx => {
                            await Task.Yield();

                            if (ctx.Request.FrameCount != 1) {
                                throw new Exception("unexpected ZMQ_RCVMORE");
                            }

                            if (ctx.Request.First.MessageSize != messageSize) {
                                throw new Exception("received message of invalid size");
                            }

                            await ctx.SendAsync(ctx.Request);

                            if (++counter >= messageCount) {
                                ctx.Dispatcher.Running.Complete();
                            }
                        })
                        : (RequestHandler) (async ctx => {
                            if (ctx.Request.FrameCount != 1) {
                                throw new Exception("unexpected ZMQ_RCVMORE");
                            }

                            if (ctx.Request.First.MessageSize != messageSize) {
                                throw new Exception("received message of invalid size");
                            }

                            await ctx.SendAsync(ctx.Request);

                            if (++counter >= messageCount) {
                                ctx.Dispatcher.Running.Complete();
                            }
                        });

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
