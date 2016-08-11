using System.Threading.Tasks;

namespace Aq.NetMQ.Util {
    public class SimpleAsyncCountdownEvent {
        public Task Completion => this.CompletionSource.Task;

        public SimpleAsyncCountdownEvent() {
            this.CompletionSource = new TaskCompletionSource();
        }

        public void Complete() {
            this._completing = true;
        }

        public void Increment() {
            ++this._counter;
        }

        public void Decrement() {
            if (--this._counter <= 0) {
                if (this._completing) {
                    this.CompletionSource.TrySetComplete();
                }
            }
        }

        private int _counter;
        private bool _completing;
        private TaskCompletionSource CompletionSource { get; }
    }
}
