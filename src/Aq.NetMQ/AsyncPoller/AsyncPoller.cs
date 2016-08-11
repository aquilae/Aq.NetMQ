using System;
using System.Collections.Concurrent;
using System.Linq;
using Aq.NetMQ.Util;
using NetMQ;
using NetMQ.Sockets;

namespace Aq.NetMQ {
    public class AsyncPoller : IDisposable {
        public AsyncPoller() {
            PairSocket.CreateSocketPair(
                out this._controlBackend,
                out this._controlFrontend);

            this.SelectItems = new ConcurrentDictionary<NetMQSocket, SelectItem>();
        }

        public void Dispose() {
            this.ControlFrontend.Close();
            this.ControlBackend.Close();

            this.ControlFrontend.Dispose();
            this.ControlBackend.Dispose();
        }

        public IRunningAsyncPoller Start() {
            return this.RunningPoller = new RunningAsyncPoller(
                this.ControlBackend, this.ControlFrontend,
                this.SelectItems.Select(x => x.Value));
        }

        public void Add(NetMQSocket socket) {
            this.SelectItems[socket] = new SelectItem(socket);
            socket.AddEventsChangedHandler(this.OnSocketEventsChanged);
            this.RunningPoller?.SendRebuild();
        }

        public void Remove(NetMQSocket socket) {
            SelectItem item;
            if (this.SelectItems.TryRemove(socket, out item)) {
                item.Socket.RemoveEventsChangedHandler(this.OnSocketEventsChanged);
                this.RunningPoller?.SendRebuild();
            }
        }

        private readonly PairSocket _controlBackend;
        private readonly PairSocket _controlFrontend;

        private PairSocket ControlBackend => this._controlBackend;
        private PairSocket ControlFrontend => this._controlFrontend;

        private ConcurrentDictionary<NetMQSocket, SelectItem> SelectItems { get; }

        private RunningAsyncPoller RunningPoller { get; set; }

        private void OnSocketEventsChanged(object sender, NetMQSocketEventArgs eventArgs) {
            SelectItem item;
            if (this.SelectItems.TryGetValue(eventArgs.Socket, out item)) {
                item.Events = item.Socket.GetPollEvents() | PollEvents.PollError;
                this.RunningPoller?.SendUpdate(item.Id);
            }
        }
    }
}
