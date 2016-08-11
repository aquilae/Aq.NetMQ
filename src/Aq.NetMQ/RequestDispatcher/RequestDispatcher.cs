using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aq.NetMQ.Util;
using NetMQ;

namespace Aq.NetMQ {
    public class RequestDispatcher : IDisposable {
        public bool IsRunning => this.Dispatch != null;
        public IRunningRequestDispatcher Running => this.Dispatch;

        public RequestHandler RequestHandler {
            get { return this._requestHandler; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof (value));
                }
                this._requestHandler = value;
            }
        }

        public RequestDispatcher(RequestHandler requestHandler) {
            this.RequestHandler = requestHandler;

            this._msg = new Msg();
            this._msg.InitEmpty();
            this._lock = new object();
            this.Poller = new AsyncPoller();
            this.Sockets = new List<NetMQSocket>();
        }

        public void Dispose() {
            this.Poller.Dispose();
            this._msg.Close();
        }

        public void Add(NetMQSocket socket) {
            lock (this._lock) {
                this.Sockets.Add(socket);
                if (this.IsRunning) {
                    socket.ReceiveReady += this.OnSocketReceiveReady;
                }
            }
            this.Poller.Add(socket);
        }

        public void Remove(NetMQSocket socket) {
            lock (this._lock) {
                if (this.IsRunning) {
                    socket.ReceiveReady -= this.OnSocketReceiveReady;
                }
                this.Sockets.Remove(socket);
            }
            this.Poller.Remove(socket);
        }

        public IRunningRequestDispatcher Start() {
            const TaskContinuationOptions taskContinuationOptions
                = TaskContinuationOptions.ExecuteSynchronously
                | TaskContinuationOptions.DenyChildAttach;

            lock (this._lock) {
                if (this.IsRunning) {
                    throw new InvalidOperationException("already running");
                }

                this.Countdown = new SimpleAsyncCountdownEvent();
                this.Dispatch = new RunningRequestDispatcher(this.Poller, this.Countdown);

                foreach (var socket in this.Sockets) {
                    socket.ReceiveReady += this.OnSocketReceiveReady;
                }

                this.Dispatch.Completion.ContinueWith(
                    this.DispatchContinuation,
                    taskContinuationOptions);

                return this.Running;
            }
        }

        private void DispatchContinuation(Task task) {
            lock (this._lock) {
                foreach (var socket in this.Sockets) {
                    socket.ReceiveReady -= this.OnSocketReceiveReady;
                }
                this.Dispatch = null;
            }
        }
        
        private readonly object _lock;
        private Msg _msg;
        private RequestHandler _requestHandler;

        private AsyncPoller Poller { get; }
        private IList<NetMQSocket> Sockets { get; }
        
        private RunningRequestDispatcher Dispatch { get; set; }
        private SimpleAsyncCountdownEvent Countdown { get; set; }

        private void OnSocketReceiveReady(object sender, NetMQSocketEventArgs eventArgs) {
            //const TaskCreationOptions taskCreationOptions = TaskCreationOptions.DenyChildAttach;

            if (eventArgs.Socket.TryReceive(ref this._msg, TimeSpan.Zero)) {
                var message = new NetMQMessage();
                var hasMore = this._msg.HasMore;
                message.Append(this._msg.CloneData());

                while (hasMore) {
                    eventArgs.Socket.Receive(ref this._msg);
                    hasMore = this._msg.HasMore;
                    message.Append(this._msg.CloneData());
                }
                
                this.HandleRequestAsync(eventArgs.Socket, message);
            }
        }

        private async void HandleRequestAsync(NetMQSocket socket, NetMQMessage message) {
            this.Countdown.Increment();
            try {
                var context = new RequestHandlerContext(
                    this, socket, this.Dispatch.Cancellation);

                context.Initialize(message);

                await this.RequestHandler(context);
            }
            finally {
                this.Countdown.Decrement();
            }
        }
    }
}
