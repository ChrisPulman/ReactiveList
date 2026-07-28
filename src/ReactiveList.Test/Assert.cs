// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

global using CP.Primitives.Internal;
global using ReactiveUI.Primitives;
global using ReactiveUI.Primitives.Concurrency;
global using ReactiveUI.Primitives.Signals;

using TUnit.Assertions.Exceptions;

namespace ReactiveList.Test;

/// <summary>Provides Assert.</summary>
internal static class Assert
{
    /// <summary>Provides All.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="collection">The collection value.</param>
    /// <param name="assertion">The assertion value.</param>
    internal static void All<T>(
        System.Collections.Generic.IEnumerable<T> collection,
        System.Action<T> assertion)
    {
        foreach (var item in collection)
        {
            assertion(item);
        }
    }

    /// <summary>Provides Contains.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="collection">The collection value.</param>
    internal static void Contains<T>(
        T expected,
        System.Collections.Generic.IEnumerable<T> collection)
    {
        foreach (var item in collection)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(item, expected))
            {
                return;
            }
        }

        Fail($"Expected collection to contain {expected}.");
    }

    /// <summary>Provides Contains.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="collection">The collection value.</param>
    /// <param name="predicate">The predicate value.</param>
    internal static void Contains<T>(
        System.Collections.Generic.IEnumerable<T> collection,
        System.Predicate<T> predicate)
    {
        foreach (var item in collection)
        {
            if (predicate(item))
            {
                return;
            }
        }

        Fail("Expected collection to contain a matching item.");
    }

    /// <summary>Provides DoesNotContain.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="collection">The collection value.</param>
    internal static void DoesNotContain<T>(
        T expected,
        System.Collections.Generic.IEnumerable<T> collection)
    {
        foreach (var item in collection)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(item, expected))
            {
                Fail($"Expected collection not to contain {expected}.");
            }
        }
    }

    /// <summary>Provides DoesNotContain.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="collection">The collection value.</param>
    /// <param name="predicate">The predicate value.</param>
    internal static void DoesNotContain<T>(
        System.Collections.Generic.IEnumerable<T> collection,
        System.Predicate<T> predicate)
    {
        foreach (var item in collection)
        {
            if (predicate(item))
            {
                Fail("Expected collection not to contain a matching item.");
            }
        }
    }

    /// <summary>Provides Empty.</summary>
    /// <param name="collection">The collection value.</param>
    internal static void Empty(System.Collections.IEnumerable collection)
    {
        if (!collection.GetEnumerator().MoveNext())
        {
            return;
        }

        Fail("Expected collection to be empty.");
    }

    /// <summary>Provides Equal.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    internal static void Equal<T>(T expected, T actual)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
        {
            return;
        }

        Fail($"Expected {expected}, but found {actual}.");
    }

    /// <summary>Provides False.</summary>
    /// <param name="condition">The condition value.</param>
    internal static void False(bool condition)
    {
        if (!condition)
        {
            return;
        }

        Fail("Expected condition to be false.");
    }

    /// <summary>Provides NotNull.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <returns>The result.</returns>
    /// <param name="value">The value.</param>
    internal static T NotNull<T>(T? value)
    {
        if (value is null)
        {
            Fail("Expected value not to be null.");
            throw new System.InvalidOperationException("The assertion failure did not throw.");
        }

        return value;
    }

    /// <summary>Provides Single.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <returns>The result.</returns>
    /// <param name="collection">The collection value.</param>
    internal static T Single<T>(System.Collections.Generic.IEnumerable<T> collection)
    {
        using var enumerator = collection.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            Fail("Expected exactly one item, but the collection was empty.");
        }

        var item = enumerator.Current;
        if (enumerator.MoveNext())
        {
            Fail("Expected exactly one item, but the collection contained multiple items.");
        }

        return item;
    }

    /// <summary>Provides Throws.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <returns>The result.</returns>
    /// <param name="action">The action value.</param>
    internal static T Throws<T>(System.Action action)
        where T : System.Exception
    {
        try
        {
            action();
        }
        catch (T exception)
        {
            return exception;
        }

        Fail($"Expected exception of type {typeof(T)}.");
        throw new System.InvalidOperationException("The assertion failure did not throw.");
    }

    /// <summary>Provides True.</summary>
    /// <param name="condition">The condition value.</param>
    internal static void True(bool condition)
    {
        if (condition)
        {
            return;
        }

        Fail("Expected condition to be true.");
    }

    /// <summary>Provides Fail.</summary>
    /// <param name="message">The message value.</param>
    private static void Fail(string message) => throw new AssertionException(message);
}
