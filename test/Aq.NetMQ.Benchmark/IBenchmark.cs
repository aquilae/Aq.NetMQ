using System.Threading.Tasks;

namespace Aq.NetMQ.Benchmark {
    public interface IBenchmark {
        Task ExecuteAsync(string[] args);
    }
}
