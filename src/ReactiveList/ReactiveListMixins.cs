// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using ReactiveUI;
using Splat;

namespace CP.Reactive;

/// <summary>
/// RxList Mixins.
/// </summary>
public static class ReactiveListMixins
{
    /// <summary>
    /// Observes the on UI when not testing.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <param name="this">The this.</param>
    /// <returns>An Observable{TSource}.</returns>
    public static IObservable<TSource> ObserveOnUIWhenNotTesting<TSource>(this IObservable<TSource> @this) =>
        ModeDetector.InUnitTestRunner() ? @this : @this.ObserveOn(RxApp.MainThreadScheduler);
}
