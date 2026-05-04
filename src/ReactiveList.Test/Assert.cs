// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
            throw new InvalidOperationException($"Expected collection to contain {expected}.");
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        if (!collection.Any(item => predicate(item)))
        {
            throw new InvalidOperationException("Expected collection to contain a matching item.");
        }
    }

    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
    {
        if (collection.Contains(expected))
        {
            throw new InvalidOperationException($"Expected collection not to contain {expected}.");
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Predicate<T> predicate)
    {
        if (collection.Any(item => predicate(item)))
        {
            throw new InvalidOperationException("Expected collection not to contain a matching item.");
        }
    }

    public static void Empty(IEnumerable collection)
    {
        if (collection.GetEnumerator().MoveNext())
        {
            throw new InvalidOperationException("Expected collection to be empty.");
        }
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, but found {actual}.");
        }
    }

    public static void False(bool condition)
    {
        if (condition)
        {
            throw new InvalidOperationException("Expected condition to be false.");
        }
    }

    public static T NotNull<T>(T? value)
    {
        if (value is null)
        {
            throw new InvalidOperationException("Expected value not to be null.");
        }

        return value;
    }

    public static T Single<T>(IEnumerable<T> collection)
    {
        using var enumerator = collection.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new InvalidOperationException("Expected exactly one item, but the collection was empty.");
        }

        var item = enumerator.Current;
        if (enumerator.MoveNext())
        {
            throw new InvalidOperationException("Expected exactly one item, but the collection contained multiple items.");
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

        throw new InvalidOperationException($"Expected exception of type {typeof(T).Name}.");
    }

    public static void True(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Expected condition to be true.");
        }
    }
}
