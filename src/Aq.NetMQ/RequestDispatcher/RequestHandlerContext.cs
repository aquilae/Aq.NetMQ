using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;

namespace Aq.NetMQ {
    public class RequestHandlerContext : IRequestHandlerContext {
        public RequestDispatcher Dispatcher { get; }

        public CancellationToken Cancellation { get; }
        public bool IsInitialized { get; private set; }
        public NetMQMessage Request { get; private set; }

        public RequestHandlerContext(
            RequestDispatcher dispatcher,
            NetMQSocket socket,
            CancellationToken cancellation) {

            this.Dispatcher = dispatcher;
            this.Socket = socket;
            this.Cancellation = cancellation;
            this.SendQueue = new ConcurrentQueue<Task<bool>>();
        }

        public void Initialize(NetMQMessage request) {
            this.Request = request;
            this.IsInitialized = true;
        }

        public Task<bool> TrySendAsync(NetMQMessage response) {
            if (this.Socket.TrySendMultipartMessage(response)) {
                return Task.FromResult(true);
            }

            var task = new Task<bool>(this.TrySend, response, TaskCreationOptions.DenyChildAttach);
            this.SendQueue.Enqueue(task);

            // NOTE: NetMQ implementation detail - adds PollEvents.PollOut
            this.Socket.SendReady += this.OnSocketSendReady;

            return task;
        }

        public Task<bool> TrySendAsync(NetMQMessage response, CancellationToken cancellation) {
            if (cancellation.IsCancellationRequested) {
                return new Task<bool>(() => false, cancellation);
            }

            if (this.Socket.TrySendMultipartMessage(response)) {
                return Task.FromResult(true);
            }

            var task = new Task<bool>(this.TrySend, response, cancellation, TaskCreationOptions.DenyChildAttach);
            this.SendQueue.Enqueue(task);

            // NOTE: NetMQ implementation detail - adds PollEvents.PollOut
            this.Socket.SendReady += this.OnSocketSendReady;

            return task;
        }

        public async Task SendAsync(NetMQMessage response) {
            if (!this.Socket.TrySendMultipartMessage(response)) {
                // ReSharper disable once MethodSupportsCancellation
                while (!await this.TrySendAsync(response)) {
                }
            }
        }

        public async Task SendAsync(NetMQMessage response, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();

            if (!this.Socket.TrySendMultipartMessage(response)) {
                while (!await this.TrySendAsync(response, cancellation)) {
                }
            }
        }

        private NetMQSocket Socket { get; }
        private ConcurrentQueue<Task<bool>> SendQueue { get; }

        private bool TrySend(object state) {
            var message = (NetMQMessage) state;
            return this.Socket.TrySendMultipartMessage(message);
        }

        private void OnSocketSendReady(object sender, NetMQSocketEventArgs eventArgs) {
            // NOTE: NetMQ implementation detail - removes PollEvents.PollOut if null
            this.Socket.SendReady -= this.OnSocketSendReady;

            Task<bool> task;
            // enter try..catch block while send queue is not empty
            while (this.SendQueue.TryPeek(out task)) {
                try {
                    do {
                        if (task.Status == TaskStatus.Created) {
                            task.RunSynchronously(TaskScheduler.Current);

                            if (!task.Result) {
                                break; // socket can not send more data
                            }
                        }
                        this.SendQueue.TryDequeue(out task);
                    }
                    while (this.SendQueue.TryPeek(out task));
                    break; // no more items in send queue
                }
                catch (InvalidOperationException) {
                    // Task.RunSynchronously() throws if
                    // task's CancellationToken was canceled
                    // dequeue such tasks
                    this.SendQueue.TryDequeue(out task);
                }
            }
        }
    }
}
