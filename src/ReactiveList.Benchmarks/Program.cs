// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// Configure for in-process benchmarking with proper thread pool settings.
ThreadPool.SetMinThreads(4, 4);

var config = ManualConfig.Create(DefaultConfig.Instance);
var hasJobOverride = args.Any(arg =>
    arg.Equals("--job", StringComparison.OrdinalIgnoreCase) ||
    arg.StartsWith("--job=", StringComparison.OrdinalIgnoreCase));

if (!hasJobOverride)
{
    config.AddJob(Job.ShortRun
        .WithToolchain(InProcessEmitToolchain.Instance)
        .WithEnvironmentVariable("DOTNET_SYSTEM_THREADING_THREADPOOL_MINWORKERS", "4"));
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
