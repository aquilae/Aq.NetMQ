using CommandLine;
using CommandLine.Text;

namespace Aq.NetMQ.Benchmark.PlainRoundtrip {
    public class Options {
        [Option('e', "endpoint", DefaultValue = "ipc://Aq.NetMQ.Benchmark.PlainThroughput",
            HelpText = "Endpoint to use")]
        public string Endpoint { get; set; }

        [Option('s', "message-size", DefaultValue = 2048,
            HelpText = "Message size in bytes")]
        public int MessageSize { get; set; }

        [Option('c', "message-count", DefaultValue = 100000,
            HelpText = "Roundtrip count")]
        public int MessageCount { get; set; }

        [HelpOption('h', "help")]
        public string GetUsage() {
            return HelpText.AutoBuild(this, x => HelpText.DefaultParsingErrorsHandler(this, x));
        }
    }
}
