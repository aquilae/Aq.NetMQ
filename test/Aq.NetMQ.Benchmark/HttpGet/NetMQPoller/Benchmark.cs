using System;
using System.Net;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Benchmark.HttpGet.NetMQPoller {
    public class Benchmark : BenchmarkBase {
        protected override string Name => "NetMQPoller";
        
        protected override Task RunServer() {
            var endpoint = this.Options.Endpoint;
            var messageSize = this.Options.MessageSize;
            var messageCount = this.Options.MessageCount;
            var httpRoundtripCount = this.Options.HttpRoundtripCount;

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

                        var webClient = new WebClient();
                        var webRequest = msg.CloneData();

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

                        webClient.Dispose();

                        msg.InitGC(webRequest, 0, messageSize);

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

            return Task.FromResult(0);
        }
    }
}
