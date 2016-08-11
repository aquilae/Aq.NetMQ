using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Showcase {
    public class Program {
        public static void Main(string[] args) {
            RunRequestDispatcher();
        }

        static void RunRequestDispatcher() {
            PairSocket server, client;
            PairSocket.CreateSocketPair(out server, out client);

            var tcs = new TaskCompletionSource<string>();

            RequestHandler requestHandler = ctx => {
                var message = ctx.Request.First.ConvertToString();
                Console.WriteLine("received {0}", message);
                tcs.SetResult(message);
                return tcs.Task;
            };

            using (server)
            using (client)
            using (var dispatcher = new RequestDispatcher(requestHandler)) {
                dispatcher.Add(server);

                var dispatch = dispatcher.Start();
                
                client.SendFrame("Hello");
                Console.WriteLine("sent Hello");

                Debug.Assert("Hello" == tcs.Task.Result);

                dispatch.Complete();
                dispatch.Completion.Wait();
            }
        }

        static void RunAsyncPoller() {
            PairSocket server, client;
            PairSocket.CreateSocketPair(out server, out client);

            using (server)
            using (client) {
                var poller = new AsyncPoller();
                poller.Add(client);

                EventHandler<NetMQSocketEventArgs> clientSend = (s, e) => {
                    e.Socket.SendFrame("Hello");
                    Console.WriteLine("Sent hello");
                };

                client.SendReady += clientSend;

                var poll = poller.Start();

                Debug.Assert("Hello" == server.ReceiveFrameString());

                poller.Remove(client);

                Console.WriteLine("Waiting");
                Task.Delay(1000).Wait();

                Console.WriteLine("Completing");
                poll.Complete();
                poll.Completion.Wait();
            }
        }
    }
}
