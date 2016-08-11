using CommandLine;

namespace Aq.NetMQ.Benchmark.HttpGet.RequestDispatcher {
    public class Options : HttpGet.Options {
        [Option("pool-workers-min", DefaultValue = 0,
            HelpText = "Minimum worker thread count")]
        public int PoolWorkerThreadsMin { get; set; }

        [Option("pool-completion-port-min", DefaultValue = 0,
            HelpText = "Minimum completion port thread count")]
        public int PoolCompletionPortThreadsMin { get; set; }

        [Option("pool-workers-max", DefaultValue = 0,
            HelpText = "Maximum worker thread count")]
        public int PoolWorkerThreadsMax { get; set; }

        [Option("pool-completion-port-max", DefaultValue = 0,
            HelpText = "Maximum completion port thread count")]
        public int PoolCompletionPortThreadsMax { get; set; }
    }
}
