// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
#if REACTIVELIST_REACTIVE
using CP.Reactive.Collections;
using CP.Reactive.Core;
using CP.Reactive.Views;
#else
using CP.Primitives.Collections;
using CP.Primitives.Core;
using CP.Primitives.Views;
#endif

namespace ReactiveList.Test;

/// <summary>Verifies structural buffering, snapshot revisions and missing-index fallbacks.</summary>
public sealed class ViewChangeBufferTests
{
    /// <summary>The first source item.</summary>
    private const string FirstItem = "first";

    /// <summary>The second source item.</summary>
    private const string SecondItem = "second";

    /// <summary>An item not present in the source.</summary>
    private const string MissingItem = "missing";

    /// <summary>The replacement item.</summary>
    private const string ReplacementItem = "replacement";

    /// <summary>An index outside the fixture.</summary>
    private const int InvalidIndex = 99;

    /// <summary>The value installed during the first enumeration.</summary>
    private const int ReplacementValue = 2;

    /// <summary>The source snapshot shared by independent test instances.</summary>
    private static readonly string[] _initialItems = [FirstItem, SecondItem];

    /// <summary>Verifies a missing removal index resolves one item without removing absent values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Apply_RemoveWithoutIndex_UsesMembership()
    {
        using var source = new ReactiveList<string>(_initialItems);
        var buffer = new ViewChangeBuffer<string>(source);
        buffer.Apply(Change<string>.CreateRemove(MissingItem));
        await Assert.That(buffer.Items).IsEquivalentTo(_initialItems, CollectionOrdering.Matching);
        buffer.Apply(Change<string>.CreateRemove(FirstItem));
        await Assert.That(buffer.Items.Count).IsEqualTo(1);
        await Assert.That(buffer.Items[0]).IsEqualTo(SecondItem);
    }

    /// <summary>Verifies updates without usable indexes resolve previous values and ignore missing occurrences.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Apply_UpdateWithoutIndex_UsesPreviousOccurrence()
    {
        using var source = new ReactiveList<string>(_initialItems);
        var buffer = new ViewChangeBuffer<string>(source);
        buffer.Apply(new(ChangeReason.Update, ReplacementItem));
        buffer.Apply(Change<string>.CreateUpdate(ReplacementItem, MissingItem, InvalidIndex));
        await Assert.That(buffer.Items).IsEquivalentTo(_initialItems, CollectionOrdering.Matching);
        buffer.Apply(Change<string>.CreateUpdate(ReplacementItem, SecondItem));
        await Assert.That(buffer.Items[0]).IsEqualTo(FirstItem);
        await Assert.That(buffer.Items[1]).IsEqualTo(ReplacementItem);
    }

    /// <summary>Verifies moves outside the mirror leave its order unchanged.</summary>
    /// <param name="currentIndex">The requested destination.</param>
    /// <param name="previousIndex">The requested origin.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(-1, 0)]
    [Arguments(0, -1)]
    [Arguments(InvalidIndex, 0)]
    [Arguments(0, InvalidIndex)]
    public async Task Apply_MoveOutsideBounds_PreservesSnapshot(int currentIndex, int previousIndex)
    {
        using var source = new ReactiveList<string>(_initialItems);
        var buffer = new ViewChangeBuffer<string>(source);
        buffer.Apply(Change<string>.CreateMove(FirstItem, currentIndex, previousIndex));
        await Assert.That(buffer.Items).IsEquivalentTo(_initialItems, CollectionOrdering.Matching);
    }

    /// <summary>Verifies refresh preserves membership while clear removes the complete mirror.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Apply_RefreshAndClear_PreserveThenRemoveItems()
    {
        using var source = new ReactiveList<string>(_initialItems);
        var buffer = new ViewChangeBuffer<string>(source);
        buffer.Apply(Change<string>.CreateRefresh(FirstItem));
        await Assert.That(buffer.Items).IsEquivalentTo(_initialItems, CollectionOrdering.Matching);
        buffer.Apply(new(ChangeReason.Clear, string.Empty));
        await Assert.That(buffer.Items).IsEmpty();
    }

    /// <summary>Verifies explicit snapshots supersede older queued changes but retain subsequent revisions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reset_QueuedChanges_DiscardsCoveredRevisions()
    {
        using var source = new ReactiveList<string>(_initialItems);
        var buffer = new ViewChangeBuffer<string>(source);
        source.Add(MissingItem);
        using var covered = new ChangeSet<string>(new[] { Change<string>.CreateAdd(MissingItem) });
        await Assert.That(buffer.Capture(covered, source.Version)).IsTrue();
        buffer.Reset(source);
        source.Add(ReplacementItem);
        using var subsequent = new ChangeSet<string>(new[] { Change<string>.CreateAdd(ReplacementItem) });
        await Assert.That(buffer.Capture(subsequent, source.Version)).IsTrue();
        await Assert.That(buffer.HasPending).IsTrue();
        await Assert.That(buffer.TryTake(out var changes)).IsTrue();
        await Assert.That(changes[0].Current).IsEqualTo(ReplacementItem);
        buffer.Apply(changes[0]);
        await Assert.That(buffer.Items).IsEquivalentTo(source.ToArray(), CollectionOrdering.Matching);
        await Assert.That(buffer.TryTake(out _)).IsFalse();
        await Assert.That(buffer.HasPending).IsFalse();
    }

    /// <summary>Verifies a source change during enumeration causes a fresh version-consistent snapshot.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reset_SourceMutatesDuringEnumeration_RetriesSnapshot()
    {
        using var source = new EnumerationMutatingSource();
        var buffer = new ViewChangeBuffer<int>(source);
        await Assert.That(buffer.Items.Count).IsEqualTo(1);
        await Assert.That(buffer.Items[0]).IsEqualTo(ReplacementValue);
    }

    /// <summary>Mutates a composed source once after capturing its enumerated snapshot.</summary>
    private sealed class EnumerationMutatingSource : IReactiveSource<int>
    {
        /// <summary>The reactive source whose normal version tracking is retained.</summary>
        private readonly ReactiveList<int> _source = new([1]);

        /// <summary>Whether the enumeration-triggered mutation has occurred.</summary>
        private bool _mutated;

        /// <inheritdoc/>
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _source.CollectionChanged += value;
            remove => _source.CollectionChanged -= value;
        }

        /// <inheritdoc/>
        public int Count => _source.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => _source.IsReadOnly;

        /// <inheritdoc/>
        public bool IsDisposed => _source.IsDisposed;

        /// <inheritdoc/>
        public long Version => _source.Version;

        /// <inheritdoc/>
        public IObservable<CacheNotify<int>> Stream => _source.Stream;

        /// <inheritdoc/>
        public IEnumerator<int> GetEnumerator()
        {
            var snapshot = _source.ToArray();
            if (!_mutated)
            {
                _mutated = true;
                _source[0] = ReplacementValue;
            }

            return ((IEnumerable<int>)snapshot).GetEnumerator();
        }

        /// <inheritdoc/>
        public int[] ToArray() => _source.ToArray();

        /// <inheritdoc/>
        public void Dispose() => _source.Dispose();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
