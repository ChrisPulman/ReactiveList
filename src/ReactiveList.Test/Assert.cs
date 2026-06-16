// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TUnit.Assertions.Exceptions;

namespace ReactiveList.Test;

/// <summary>Provides Assert.</summary>
internal static class Assert
{
    /// <summary>Provides All.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="collection">The collection value.</param>
    /// <param name="assertion">The assertion value.</param>
    public static void All<T>(IEnumerable<T> collection, Action<T> assertion)
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
    public static void Contains<T>(T expected, IEnumerable<T> collection)
    {
        if (collection.Contains(expected))
        {
            return;
        }

        Fail($"Expected collection to contain {expected}.");
    }

    /// <summary>Provides Contains.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="collection">The collection value.</param>
    /// <param name="predicate">The predicate value.</param>
    public static void Contains<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        if (collection.Any(item => predicate(item)))
        {
            return;
        }

        Fail("Expected collection to contain a matching item.");
    }

    /// <summary>Provides DoesNotContain.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="collection">The collection value.</param>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
    {
        if (!collection.Contains(expected))
        {
            return;
        }

        Fail($"Expected collection not to contain {expected}.");
    }

    /// <summary>Provides DoesNotContain.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <param name="collection">The collection value.</param>
    /// <param name="predicate">The predicate value.</param>
    public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        if (!collection.Any(item => predicate(item)))
        {
            return;
        }

        Fail("Expected collection not to contain a matching item.");
    }

    /// <summary>Provides Empty.</summary>
    /// <param name="collection">The collection value.</param>
    public static void Empty(IEnumerable collection)
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
    public static void Equal<T>(T expected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
        {
            return;
        }

        Fail($"Expected {expected}, but found {actual}.");
    }

    /// <summary>Provides False.</summary>
    /// <param name="condition">The condition value.</param>
    public static void False(bool condition)
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
    public static T NotNull<T>(T? value)
    {
        if (value is null)
        {
            Fail("Expected value not to be null.");
            throw new InvalidOperationException("The assertion failure did not throw.");
        }

        return value;
    }

    /// <summary>Provides Single.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    /// <returns>The result.</returns>
    /// <param name="collection">The collection value.</param>
    public static T Single<T>(IEnumerable<T> collection)
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
    public static T Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T exception)
        {
            return exception;
        }

        Fail($"Expected exception of type {typeof(T).Name}.");
        throw new InvalidOperationException("The assertion failure did not throw.");
    }

    /// <summary>Provides True.</summary>
    /// <param name="condition">The condition value.</param>
    public static void True(bool condition)
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
