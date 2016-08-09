using System;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.PlainRoundtrip.Naive {
    public class Benchmark : BenchmarkBase {
        protected override string Name => "Naive";

        protected override Task RunServer() {
            var endpoint = this.Options.Endpoint;
            var messageSize = this.Options.MessageSize;
            var messageCount = this.Options.MessageCount;

            var msg = new Msg();

            using (var socket = new PairSocket($"@{endpoint}")) {
                this.SetupBarrier();
                this.StartBarrier();

                for (var i = 0; i < messageCount; ++i) {
                    msg.InitEmpty();
                    socket.Receive(ref msg);

                    if (msg.HasMore) {
                        throw new Exception("unexpected ZMQ_RCVMORE");
                    }

                    if (msg.Size != messageSize) {
                        throw new Exception("received message of invalid size");
                    }

                    socket.Send(ref msg, false);
                    msg.Close();
                }

                this.CompleteBarrier();
            }

            return Task.CompletedTask;
        }
    }
}
