// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

global using System.Collections.Generic;
global using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
global using CP.Reactive.Internal;
global using ReactiveUI.Primitives.Signals;
#else
global using CP.Primitives.Internal;
global using ReactiveUI.Primitives;
global using ReactiveUI.Primitives.Concurrency;
global using ReactiveUI.Primitives.Signals;
#endif
global using TUnit.Assertions;
global using TUnit.Assertions.Enums;
#if REACTIVELIST_REACTIVE
global using Sequencer = System.Reactive.Concurrency.Scheduler;
#endif

namespace ReactiveList.Test;

/// <summary>Provides shared expected sequences for collection assertions.</summary>
internal static class ExpectedSequences
{
    /// <summary>The two items enumerated by a non-generic wrapper.</summary>
    internal static readonly IReadOnlyList<object?> WrapperItems = System.Array.AsReadOnly<object?>(["one", "two"]);

    /// <summary>The items copied by a non-generic list.</summary>
    internal static readonly IReadOnlyList<object> CopiedItems = System.Array.AsReadOnly<object>(["zero", "two", TestData.ThreeText]);

    /// <summary>The indexer property notification name.</summary>
    internal static readonly IReadOnlyList<string?> IndexerPropertyNames = System.Array.AsReadOnly<string?>([TestData.IndexerPropertyName]);
}
