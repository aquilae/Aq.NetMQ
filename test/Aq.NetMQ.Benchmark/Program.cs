using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aq.NetMQ.Benchmark {
    public class Program {
        public static void Main(string[] args) {
            //Debug.Listeners.Add(new ConsoleTraceListener());

            var benchmarks = new Dictionary<string, IBenchmark> {
                [$"{nameof (PlainRoundtrip)}.{nameof (PlainRoundtrip.Naive)}"] = new PlainRoundtrip.Naive.Benchmark(),
                [$"{nameof (PlainRoundtrip)}.{nameof (PlainRoundtrip.NetMQPoller)}"] = new PlainRoundtrip.NetMQPoller.Benchmark(),
                [$"{nameof (PlainRoundtrip)}.{nameof (PlainRoundtrip.AsyncPoller)}"] = new PlainRoundtrip.AsyncPoller.Benchmark(),
            };

            IBenchmark benchmark;
            if (benchmarks.TryGetValue(args.FirstOrDefault() ?? "", out benchmark)) {
                benchmark.ExecuteAsync(args.Skip(1).ToArray()).Wait();
            }
            else {
                Console.WriteLine("Usage: <benchmark> [<args>]");
                Console.WriteLine();

                Console.WriteLine("Available benchmarks:");
                foreach (var key in benchmarks.Keys) {
                    Console.WriteLine($"  {key}");
                }
                Console.WriteLine();

                if (args.Length > 0) {
                    Console.WriteLine($"Unknown benchmark: {args[0]}");
                    Console.WriteLine();
                }
            }
        }
    }
}
