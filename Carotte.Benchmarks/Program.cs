using BenchmarkDotNet.Running;
using Carotte.Benchmarks.Config;

namespace Carotte.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args, new BenchmarkConfig());
    }
}
