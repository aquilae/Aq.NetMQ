using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ.Showcase {
    public class Program {
        public static void Main(string[] args) {
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
