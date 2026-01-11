using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// Configure for in-process benchmarking with proper thread pool settings
ThreadPool.SetMinThreads(4, 4);

var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddJob(Job.ShortRun
        .WithToolchain(InProcessEmitToolchain.Instance)
        .WithEnvironmentVariable("DOTNET_SYSTEM_THREADING_THREADPOOL_MINWORKERS", "4"));

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
