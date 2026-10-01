// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveListTestApp.Infrastructure;

namespace ReactiveListTestApp.Tests;

/// <summary>Verifies forwarding and error propagation at the observable boundary.</summary>
public sealed class ActionObserverTests
{
    /// <summary>The second value forwarded to the delegate.</summary>
    private const int SecondValue = 2;

    /// <summary>The values expected in callback order.</summary>
    private static readonly int[] ExpectedValues = [1, SecondValue];

    /// <summary>Forwards values unchanged and completion does not invoke the value callback.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnNext_Values_InvokesDelegateInOrder()
    {
        List<int> values = [];
        var observer = new ActionObserver<int>(values.Add);
        observer.OnNext(1);
        observer.OnNext(SecondValue);
        observer.OnCompleted();
        await Assert.That(values).IsEquivalentTo(ExpectedValues);
    }

    /// <summary>Propagates the original exception instance to the caller.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnError_Exception_PropagatesSameInstance()
    {
        var observer = new ActionObserver<int>(static _ => { });
        var error = new InvalidOperationException("upstream failed");
        var thrown = await Assert.That(() => observer.OnError(error)).Throws<InvalidOperationException>();
        await Assert.That(thrown).IsSameReferenceAs(error);
    }
}
