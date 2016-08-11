using System.Threading.Tasks;

namespace Aq.NetMQ {
    public interface IRunningRequestDispatcher {
        Task Completion { get; }
        void Complete();
    }
}
