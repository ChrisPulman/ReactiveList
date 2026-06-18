// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using CP.Reactive;
using TUnit.Assertions;
using ReactiveCacheAction = CP.Reactive.Core.CacheAction;
using ReactiveListOfInt = CP.Reactive.Collections.ReactiveList<int>;

namespace ReactiveList.Reactive.Test;

/// <summary>Smoke tests for the System.Reactive-flavoured ReactiveList assembly.</summary>
public sealed class ReactiveListReactiveSmokeTests
{
    /// <summary>Ensures the reactive variant publishes collection changes and accepts a System.Reactive scheduler.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReactiveVariantUsesSystemReactiveScheduler()
    {
        var list = new ReactiveListOfInt();
        var actions = new List<ReactiveCacheAction>();

        using var subscription = list.Stream.Subscribe(notification => actions.Add(notification.Action));

        list.Add(1);
        using var view = list.CreateView(ImmediateScheduler.Instance, throttleMs: 0);

        await Assert.That(actions.Contains(ReactiveCacheAction.Added)).IsTrue();
        await Assert.That(view.Count).IsEqualTo(1);
    }
}
