using System.Threading.Tasks;

namespace Aq.NetMQ.Tests {
    internal static class Extensions {
        public static bool IsPending(this Task self) {
            return !self.IsCompleted && !self.IsCanceled && !self.IsFaulted;
        }
    }
}
