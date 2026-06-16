// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TUnit.Assertions.Exceptions;

namespace ReactiveList.Test;

internal static class Assert
{
    public static void All<T>(IEnumerable<T> collection, Action<T> assertion)
    {
        foreach (var item in collection)
        {
            assertion(item);
        }
    }

    public static void Contains<T>(T expected, IEnumerable<T> collection)
    {
        if (!collection.Contains(expected))
        {
            Fail($"Expected collection to contain {expected}.");
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        if (!collection.Any(item => predicate(item)))
        {
            Fail("Expected collection to contain a matching item.");
        }
    }

    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
    {
        if (collection.Contains(expected))
        {
            Fail($"Expected collection not to contain {expected}.");
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        if (collection.Any(item => predicate(item)))
        {
            Fail("Expected collection not to contain a matching item.");
        }
    }

    public static void Empty(IEnumerable collection)
    {
        if (collection.GetEnumerator().MoveNext())
        {
            Fail("Expected collection to be empty.");
        }
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            Fail($"Expected {expected}, but found {actual}.");
        }
    }

    public static void False(bool condition)
    {
        if (condition)
        {
            Fail("Expected condition to be false.");
        }
    }

    public static T NotNull<T>(T? value)
    {
        if (value is null)
        {
            Fail("Expected value not to be null.");
        }

        return value;
    }

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
        throw new UnreachableException();
    }

    public static void True(bool condition)
    {
        if (!condition)
        {
            Fail("Expected condition to be true.");
        }
    }

    private static void Fail(string message) => throw new AssertionException(message);
}

internal sealed class UnreachableException : Exception;
