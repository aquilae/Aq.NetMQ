using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Aq.NetMQ.Util;

namespace Aq.NetMQ {
    public class RunningRequestDispatcher : IRunningRequestDispatcher {
        public Task Completion { get; }
        public CancellationToken Cancellation => this.CancellationSource.Token;

        public RunningRequestDispatcher(AsyncPoller poller, SimpleAsyncCountdownEvent countdown) {
            const TaskCreationOptions taskCreationOptions = TaskCreationOptions.DenyChildAttach;

            this.Poller = poller;
            this.Countdown = countdown;

            this.Exceptions = new ConcurrentQueue<Exception>();
            this.CancellationSource = new CancellationTokenSource();

            this.Completion = Task.Factory.StartNew(
                this.RunAsync, CancellationToken.None,
                taskCreationOptions, TaskScheduler.Default).Unwrap();
        }

        public void Complete() {
            this.CancellationSource.Cancel();
        }

        public void ThrowAsyncException(Exception exception) {
            this.Exceptions.Enqueue(exception);
            this.CancellationSource.Cancel();
        }

        private AsyncPoller Poller { get; }
        private SimpleAsyncCountdownEvent Countdown { get; }
        private ConcurrentQueue<Exception> Exceptions { get; }
        private CancellationTokenSource CancellationSource { get; }

        private async Task RunAsync() {
            try {
                var stopEventSource = new TaskCompletionSource();
                using (this.Cancellation.Register(stopEventSource.SetComplete)) {
                    var poll = this.Poller.Start();

                    await Task.WhenAny(poll.Completion, stopEventSource.Task);

                    this.CancellationSource.Cancel();
                    this.Countdown.Complete();
                    await this.Countdown.Completion;
                    poll.Complete();

                    await Task.WhenAll(poll.Completion /*, this.TaskManager.Completion*/);
                }
            }
            catch (Exception exc) {
                this.Exceptions.Enqueue(exc);
            }
            finally {
                if (this.Exceptions.Count > 0) {
                    throw new AggregateException(this.Exceptions);
                }
            }
        }
    }
}
