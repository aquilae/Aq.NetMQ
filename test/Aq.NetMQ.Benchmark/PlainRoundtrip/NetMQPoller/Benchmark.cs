using System;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.PlainRoundtrip.NetMQPoller {
    public class Benchmark : BenchmarkBase {
        protected override string Name => "NetMQPoller";

        protected override Task RunServer() {
            var endpoint = this.Options.Endpoint;
            var messageSize = this.Options.MessageSize;
            var messageCount = this.Options.MessageCount;

            var msg = new Msg();

            using (var socket = new PairSocket($"@{endpoint}"))
            using (var poller = new global::NetMQ.NetMQPoller { socket }) {
                var counter = 0;

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
                            poller.StopAsync();
                        }
                    }
                    msg.Close();
                };

                this.SetupBarrier();
                this.StartBarrier();

                poller.Run();

                this.CompleteBarrier();
            }

            return Task.CompletedTask;
        }
    }
}
