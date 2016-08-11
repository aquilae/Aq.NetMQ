using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Aq.NetMQ.Util;
using NetMQ;
using NetMQ.Core;

namespace Aq.NetMQ {
    public class RunningAsyncPoller : IRunningAsyncPoller {
        public Task Completion { get; }

        public RunningAsyncPoller(
            NetMQSocket controlBackend,
            NetMQSocket controlFrontend,
            IEnumerable<SelectItem> items) {

            this.ControlBackend = controlBackend;
            this.ControlFrontend = controlFrontend;

            this.ControlBackend.ReceiveReady += this.OnControlReceiveReady;
            
            this.Items = items.Concat(new [] {
                new SelectItem(controlBackend, 0)
            });

            this.State = new PollState(this);

            this.Completion = new Task(
                this.Run, TaskCreationOptions.LongRunning
                    | TaskCreationOptions.DenyChildAttach);

            this.Completion.Start(TaskScheduler.Default);
        }

        public void Complete() {
            this.SendStop();
        }

        public void SendStop() {
            //Console.WriteLine("?> COMMAND_STOP");

            this.ControlFrontend.SendFrame(
                NetworkOrderBitsConverter.GetBytes(COMMAND_STOP));

            //Console.WriteLine("!> COMMAND_STOP");
        }

        public void SendUpdate(int itemId) {
            //Console.WriteLine("?> COMMAND_UPDATE");

            this.ControlFrontend.SendFrame(new [] {
                NetworkOrderBitsConverter.GetBytes(COMMAND_UPDATE),
                NetworkOrderBitsConverter.GetBytes(itemId)
            }.SelectMany(x => x).ToArray());

            //Console.WriteLine("!> COMMAND_UPDATE");
        }

        public void SendRebuild() {
            //Console.WriteLine("?> COMMAND_REBUILD");

            this.ControlFrontend.SendFrame(
                NetworkOrderBitsConverter.GetBytes(COMMAND_REBUILD));

            //Console.WriteLine("!> COMMAND_REBUILD");
        }

        private const int COMMAND_STOP = 1;
        private const int COMMAND_UPDATE = 2;
        private const int COMMAND_REBUILD = 3;

        private NetMQSocket ControlBackend { get; }
        private NetMQSocket ControlFrontend { get; }
        private IEnumerable<SelectItem> Items { get; }

        private PollState State { get; }

        private void OnControlReceiveReady(
            object sender, NetMQSocketEventArgs eventArgs) {

            var socket = eventArgs.Socket;

            byte[] frame;
            while (socket.TryReceiveFrameBytes(out frame)) {
                var command = NetworkOrderBitsConverter.ToInt32(frame);
                switch (command) {
                    case COMMAND_STOP:
                        //Console.WriteLine("< COMMAND_STOP");

                        this.State.Canceled = true;
                        break;
                    case COMMAND_UPDATE:
                        //Console.WriteLine("< COMMAND_UPDATE");

                        var id = NetworkOrderBitsConverter.ToInt32(frame.Skip(4).ToArray());
                        var item = this.State.Items[id];
                        item.Events = item.Socket.GetPollEvents() | PollEvents.PollError;
                        break;
                    case COMMAND_REBUILD:
                        //Console.WriteLine("< COMMAND_REBUILD");

                        this.State.Build();
                        break;
                    //default:
                    //    Console.WriteLine("< COMMAND_UNKNOWN");
                    //    break;
                }
            }
        }

        private void Run() {
            try {
                this.State.Build();

                do {
                    if (this.State.Init(true)) {
                        this.Select();
                        this.ProcessSelectResult();
                        this.RaiseSocketEvents();
                    }

                    // fake select - use zmq_getsockopts(ZMQ_EVENTS)
                    while (!this.State.Canceled
                        && this.State.Init(false)) {

                        this.ProcessSelectResult();
                        this.RaiseSocketEvents();
                    }
                }
                while (!this.State.Canceled);
            }
            finally {
                while (this.ControlBackend.TrySkipMultipartMessage()) {
                }
            }
        }

        private void Select() {
            const int selectTimeout = -1;

            //Console.WriteLine("~ IN: {0}", string.Join(" ", this.State.CheckRead.Select(x => this.State.Map[x].Id)));
            //Console.WriteLine("~ OUT: {0}", string.Join(" ", this.State.CheckWrite.Select(x => this.State.Map[x].Id)));
            //Console.WriteLine("~ ERROR: {0}", string.Join(" ", this.State.CheckError.Select(x => this.State.Map[x].Id)));

            Socket.Select(this.State.CheckRead, this.State.CheckWrite, this.State.CheckError, selectTimeout);
        }

        private void ProcessSelectResult() {
            foreach (var handle in this.State.CheckRead) {
                var item = this.State.Map[handle];
                
                //Console.WriteLine($"? {item.Id} < POLLIN");

                var events = item.Socket.GetEventsOption();
                if (events.HasFlagFast(PollEvents.PollIn)) {
                    item.Result |= PollEvents.PollIn;

                    //Console.WriteLine($"! {item.Id} < POLLIN");
                }
            }

            foreach (var handle in this.State.CheckWrite) {
                var item = this.State.Map[handle];

                //Console.WriteLine($"? {item.Id} < POLLOUT");

                var events = item.Socket.GetEventsOption();
                if (events.HasFlagFast(PollEvents.PollOut)) {
                    item.Result |= PollEvents.PollOut;

                    //Console.WriteLine($"! {item.Id} < POLLOUT");
                }
            }

            foreach (var handle in this.State.CheckError) {
                var item = this.State.Map[handle];

                //Console.WriteLine($"? {item.Id} < POLLERROR");

                var events = item.Socket.GetEventsOption();
                if (events.HasFlagFast(PollEvents.PollError)) {
                    item.Result |= PollEvents.PollError;

                    //Console.WriteLine($"! {item.Id} < POLLERROR");
                }
            }
        }

        private void RaiseSocketEvents() {
            var items = this.State.Items;

            var control = items[0];

            if (control.Result.HasFlagFast(PollEvents.PollError)) {
                throw new Exception("Poller control socket failure");
            }

            if (control.Result.HasFlagFast(PollEvents.PollIn)) {
                control.Socket.InvokeEvents(this, control.Result);
            }

            if (!this.State.Canceled) {
                foreach (var item in items.Values) {
                    if (item != control) {
                        if (item.Result.HasFlagFast(PollEvents.PollError)) {
                            // TODO
                        }

                        if (item.Result.HasFlagFast(PollEvents.PollIn) ||
                            item.Result.HasFlagFast(PollEvents.PollOut)) {

                            item.Socket.InvokeEvents(this, item.Result);
                        }
                    }
                }
            }
        }

        private sealed class PollState {
            public bool Canceled;

            public Dictionary<int, SelectItem> Items;
            public Dictionary<Socket, SelectItem> Map;
            
            public readonly List<Socket> CheckRead;
            public readonly List<Socket> CheckWrite;
            public readonly List<Socket> CheckError;

            public PollState(RunningAsyncPoller poller) {
                this._poller = poller;

                this.CheckRead = new List<Socket>(0);
                this.CheckWrite = new List<Socket>(0);
                this.CheckError = new List<Socket>(0);
            }

            public void Build() {
                this.Items = this._poller.Items.ToDictionary(x => x.Id);
                this.Map = this.Items.Values.ToDictionary(x => x.Handle);
                this._itemCount = this.Items.Count;
            }

            public bool Init(bool realSelect) {
                this.CheckRead.Clear();
                this.CheckWrite.Clear();
                this.CheckError.Clear();

                this.CheckRead.Capacity = this._itemCount;
                this.CheckWrite.Capacity = this._itemCount;
                this.CheckError.Capacity = this._itemCount;

                var isEmpty = true;
                
                foreach (var item in this.Items.Values) {
                    //item.Events = item.Socket.GetPollEvents() | PollEvents.PollError;
                    item.Result = PollEvents.None;

                    if (realSelect) {
                        if (item.Events.HasFlagFast(PollEvents.PollIn)) {
                            this.CheckRead.Add(item.Handle);
                            isEmpty = false;
                        }
                        if (item.Events.HasFlagFast(PollEvents.PollOut)) {
                            this.CheckWrite.Add(item.Handle);
                            isEmpty = false;
                        }
                        if (item.Events.HasFlagFast(PollEvents.PollError)) {
                            this.CheckError.Add(item.Handle);
                            isEmpty = false;
                        }
                    }
                    else {
                        var events = item.Socket.GetEventsOption();

                        if (events.HasFlagFast(PollEvents.PollIn)) {
                            if (item.Events.HasFlagFast(PollEvents.PollIn)) {
                                this.CheckRead.Add(item.Handle);
                                item.Result |= PollEvents.PollIn;
                                isEmpty = false;
                            }
                        }

                        if (events.HasFlagFast(PollEvents.PollOut)) {
                            if (item.Events.HasFlagFast(PollEvents.PollOut)) {
                                this.CheckWrite.Add(item.Handle);
                                item.Result |= PollEvents.PollOut;
                                isEmpty = false;
                            }
                        }

                        if (events.HasFlagFast(PollEvents.PollError)) {
                            if (item.Events.HasFlagFast(PollEvents.PollError)) {
                                this.CheckError.Add(item.Handle);
                                item.Result |= PollEvents.PollError;
                                isEmpty = false;
                            }
                        }
                    }
                }

                return !isEmpty;
            }

            private readonly RunningAsyncPoller _poller;
            private int _itemCount;
        }
    }
}
