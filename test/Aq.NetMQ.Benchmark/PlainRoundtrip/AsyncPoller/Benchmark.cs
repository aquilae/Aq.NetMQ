using System;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.PlainRoundtrip.AsyncPoller {
    public class Benchmark : BenchmarkBase {
        protected override string Name => "AsyncPoller";

        protected override async Task RunServer() {
            try {
                var endpoint = this.Options.Endpoint;
                var messageSize = this.Options.MessageSize;
                var messageCount = this.Options.MessageCount;

                var msg = new Msg();

                using (var socket = new PairSocket($"@{endpoint}")) {
                    var counter = 0;

                    var poller = new NetMQ.AsyncPoller();
                    poller.Add(socket);

                    var poll = poller.Start();

                    socket.ReceiveReady += (sender, eventArgs) => {
                        var s = eventArgs.Socket;

                        msg.InitEmpty();
                        if (s.TryReceive(ref msg, TimeSpan.Zero)) {
                            if (msg.HasMore) {
                                throw new Exception("unexpected ZMQ_RCVMORE");
                            }

                            if (msg.Size != messageSize) {
                                throw new Exception("received message of invalid size");
                            }

                            s.Send(ref msg, false);

                            if (++counter >= messageCount) {
                                poll.Complete();
                            }
                        }
                        msg.Close();
                    };

                    this.SetupBarrier();
                    this.StartBarrier();

                    await poll.Completion;

                    this.CompleteBarrier();
                }
            }
            catch (Exception exc) {
                Console.WriteLine("server exception:\n{0}", exc);
            }
        }
    }
}
