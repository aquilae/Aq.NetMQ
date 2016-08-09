using System.Threading.Tasks;

namespace Aq.NetMQ {
    public interface IRunningAsyncPoller {
        Task Completion { get; }
        void Complete();
    }
}
