using System;
using System.Net;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.HttpGet.AsyncPoller {
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

                            var webClient = new WebClient();
                            var webRequest = msg.CloneData();
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

                            msg.InitGC(webResponse, 0, messageSize);

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
