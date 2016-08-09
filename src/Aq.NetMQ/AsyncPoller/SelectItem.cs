using System.Net.Sockets;
using System.Threading;
using Aq.NetMQ.Util;
using NetMQ;

namespace Aq.NetMQ {
    public sealed class SelectItem {
        private static int NextId;

        public object State;
        public readonly NetMQSocket Socket;

        public readonly int Id;
        public readonly Socket Handle;

        public PollEvents Events;
        public PollEvents Result;

        public SelectItem(
            NetMQSocket socket, int? id = null) {

            this.Socket = socket;

            this.Id = id ?? Interlocked.Increment(ref NextId);
            this.Handle = socket.GetHandle();

            this.Events = socket.GetPollEvents() | PollEvents.PollError;
        }

        public override string ToString() {
            var check =
                (this.Events.HasFlag(PollEvents.PollIn) ? "R" : "") +
                (this.Events.HasFlag(PollEvents.PollOut) ? "W" : "") +
                (this.Events.HasFlag(PollEvents.PollError) ? "E" : "");

            var result =
                (this.Result.HasFlag(PollEvents.PollIn) ? "R" : "") +
                (this.Result.HasFlag(PollEvents.PollOut) ? "W" : "") +
                (this.Result.HasFlag(PollEvents.PollError) ? "E" : "");

            return $"[SelectItem #{this.Id} {check}=>{result}]";
        }
    }
}
