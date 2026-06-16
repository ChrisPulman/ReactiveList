// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CP.Reactive.Core;
using TUnit.Assertions.Exceptions;

namespace FluentAssertions;

internal static class AssertionExtensions
{
    public static ActionAssertions Should(this Action subject) => new(subject);

    public static FuncTaskAssertions Should(this Func<Task> subject) => new(subject);

    public static ActionAssertions Should<TResult>(this Func<TResult> subject) => new(() => _ = subject());

    public static BooleanAssertions Should(this bool subject) => new(subject);

    public static StringAssertions Should(this string? subject) => new(subject);

    public static EnumerableAssertions<TItem> Should<TItem>(this IEnumerable<TItem>? subject) => new(subject);

    public static EnumerableAssertions<Change<TItem>> Should<TItem>(this ChangeSet<TItem> subject) => new(subject);

    public static ObjectAssertions<TSubject> Should<TSubject>(this TSubject subject)
        where TSubject : struct =>
        new(subject);

    public static ObjectAssertions<object?> Should(this object? subject) => new(subject);
}

internal sealed class EquivalencyAssertionOptions
{
    public bool StrictOrdering { get; private set; }

    public EquivalencyAssertionOptions WithStrictOrdering()
    {
        StrictOrdering = true;
        return this;
    }
}

internal sealed class AndWhichConstraint<T>
{
    public AndWhichConstraint(T which) => Which = which;

    public T Which { get; }

    public AndWhichConstraint<T> And => this;
}

internal sealed class ObjectAssertions<TSubject>
{
    private readonly TSubject _subject;

    public ObjectAssertions(TSubject subject) => _subject = subject;

    public ObjectAssertions<TSubject> Be<TExpected>(TExpected expected, string because = "", params object[] becauseArgs)
    {
        if (!AreEqual(_subject, expected))
        {
            Fail($"Expected {Format(expected)}, but found {Format(_subject)}{Because(because, becauseArgs)}.");
        }

        return this;
    }

    public ObjectAssertions<TSubject> NotBe<TExpected>(TExpected expected)
    {
        if (Equals(_subject, expected))
        {
            Fail($"Expected value not to be {Format(expected)}.");
        }

        return this;
    }

    public ObjectAssertions<TSubject> BeSameAs(object? expected)
    {
        if (!ReferenceEquals(_subject, expected))
        {
            Fail("Expected both references to point to the same object.");
        }

        return this;
    }

    public ObjectAssertions<TSubject> BeNull()
    {
        if (_subject is not null)
        {
            Fail($"Expected null, but found {Format(_subject)}.");
        }

        return this;
    }

    public ObjectAssertions<TSubject> NotBeNull()
    {
        if (_subject is null)
        {
            Fail("Expected value not to be null.");
        }

        return this;
    }

    public AndWhichConstraint<TExpected> BeOfType<TExpected>()
    {
        if (_subject is TExpected expected)
        {
            return new AndWhichConstraint<TExpected>(expected);
        }

        Fail($"Expected value to be of type {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? "<null>"}.");
        throw new UnreachableException();
    }

    public AndWhichConstraint<TExpected> BeAssignableTo<TExpected>()
    {
        if (_subject is TExpected expected)
        {
            return new AndWhichConstraint<TExpected>(expected);
        }

        Fail($"Expected value to be assignable to {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? "<null>"}.");
        throw new UnreachableException();
    }

    public ObjectAssertions<TSubject> BeEquivalentTo<TExpected>(
        TExpected expected,
        Func<EquivalencyAssertionOptions, EquivalencyAssertionOptions>? configure = null)
    {
        var options = ApplyOptions(configure);
        if (TryGetEnumerable(_subject, out var actualItems) && TryGetEnumerable(expected, out var expectedItems))
        {
            AssertEquivalentSequence(actualItems, expectedItems, options.StrictOrdering);
            return this;
        }

        if (!Equals(_subject, expected))
        {
            Fail($"Expected {Format(expected)}, but found {Format(_subject)}.");
        }

        return this;
    }

    public ObjectAssertions<TSubject> HaveCount(int expected)
    {
        var actual = SnapshotEnumerable().Count;
        if (actual != expected)
        {
            Fail($"Expected collection to contain {expected} item(s), but found {actual}.");
        }

        return this;
    }

    public AndWhichConstraint<object?> ContainSingle()
    {
        var actual = SnapshotEnumerable();
        if (actual.Count != 1)
        {
            Fail($"Expected collection to contain a single item, but found {actual.Count}.");
        }

        return new AndWhichConstraint<object?>(actual[0]);
    }

    public ObjectAssertions<TSubject> BeGreaterThan<TExpected>(TExpected expected)
    {
        if (Compare(_subject, expected) <= 0)
        {
            Fail($"Expected {Format(_subject)} to be greater than {Format(expected)}.");
        }

        return this;
    }

    public ObjectAssertions<TSubject> BeGreaterThanOrEqualTo<TExpected>(TExpected expected)
    {
        if (Compare(_subject, expected) < 0)
        {
            Fail($"Expected {Format(_subject)} to be greater than or equal to {Format(expected)}.");
        }

        return this;
    }

    public ObjectAssertions<TSubject> BeLessThanOrEqualTo<TExpected>(TExpected expected)
    {
        if (Compare(_subject, expected) > 0)
        {
            Fail($"Expected {Format(_subject)} to be less than or equal to {Format(expected)}.");
        }

        return this;
    }

    public ObjectAssertions<TSubject> BeInRange<TExpected>(TExpected minimum, TExpected maximum)
    {
        if (Compare(_subject, minimum) < 0 || Compare(_subject, maximum) > 0)
        {
            Fail($"Expected {Format(_subject)} to be in range {Format(minimum)}..{Format(maximum)}.");
        }

        return this;
    }

    internal static EquivalencyAssertionOptions ApplyOptions(
        Func<EquivalencyAssertionOptions, EquivalencyAssertionOptions>? configure)
    {
        var options = new EquivalencyAssertionOptions();
        return configure?.Invoke(options) ?? options;
    }

    internal static void Fail(string message) => throw new AssertionException(message);

    internal static string Format(object? value) => value is null ? "<null>" : value.ToString() ?? "<value>";

    internal static void AssertEquivalentSequence(IReadOnlyList<object?> actual, IReadOnlyList<object?> expected, bool strictOrdering)
    {
        if (actual.Count != expected.Count)
        {
            Fail($"Expected {expected.Count} item(s), but found {actual.Count} item(s).");
        }

        if (strictOrdering)
        {
            for (var i = 0; i < actual.Count; i++)
            {
                if (!Equals(actual[i], expected[i]))
                {
                    Fail($"Expected item at index {i} to be {Format(expected[i])}, but found {Format(actual[i])}.");
                }
            }

            return;
        }

        var unmatched = expected.ToList();
        foreach (var item in actual)
        {
            var index = unmatched.FindIndex(expectedItem => Equals(item, expectedItem));
            if (index < 0)
            {
                Fail($"Did not expect item {Format(item)}.");
            }

            unmatched.RemoveAt(index);
        }
    }

    internal static bool TryGetEnumerable(object? value, out IReadOnlyList<object?> items)
    {
        if (value is null || value is string || value is not IEnumerable enumerable)
        {
            items = Array.Empty<object?>();
            return false;
        }

        items = enumerable.Cast<object?>().ToArray();
        return true;
    }

    internal static bool AreEqual(object? actual, object? expected)
    {
        if (Equals(actual, expected))
        {
            return true;
        }

        if (actual is IConvertible actualConvertible &&
            expected is IConvertible expectedConvertible &&
            IsNumeric(actualConvertible.GetTypeCode()) &&
            IsNumeric(expectedConvertible.GetTypeCode()))
        {
            return Convert.ToDecimal(actualConvertible, CultureInfo.InvariantCulture) ==
                Convert.ToDecimal(expectedConvertible, CultureInfo.InvariantCulture);
        }

        return false;
    }

    private static int Compare(object? actual, object? expected)
    {
        if (actual is null || expected is null)
        {
            Fail("Cannot compare null values.");
        }

        if (actual is IComparable comparable)
        {
            return comparable.CompareTo(expected);
        }

        Fail($"Type {actual.GetType().FullName} is not comparable.");
        return 0;
    }

    private static bool IsNumeric(TypeCode typeCode) =>
        typeCode is TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64
            or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    private IReadOnlyList<object?> SnapshotEnumerable()
    {
        if (!TryGetEnumerable(_subject, out var items))
        {
            Fail($"Expected {typeof(TSubject).FullName} to be enumerable.");
        }

        return items;
    }

    private static string Because(string because, object[] args)
    {
        if (string.IsNullOrWhiteSpace(because))
        {
            return string.Empty;
        }

        return " because " + (args.Length == 0 ? because : string.Format(because, args));
    }
}

internal sealed class BooleanAssertions
{
    private readonly bool _subject;

    public BooleanAssertions(bool subject) => _subject = subject;

    public BooleanAssertions Be(bool expected, string because = "", params object[] becauseArgs)
    {
        if (_subject != expected)
        {
            ObjectAssertions<object?>.Fail($"Expected {expected}, but found {_subject}{Because(because, becauseArgs)}.");
        }

        return this;
    }

    public BooleanAssertions BeTrue(string because = "", params object[] becauseArgs) => Be(true, because, becauseArgs);

    public BooleanAssertions BeFalse(string because = "", params object[] becauseArgs) => Be(false, because, becauseArgs);

    private static string Because(string because, object[] args)
    {
        if (string.IsNullOrWhiteSpace(because))
        {
            return string.Empty;
        }

        return " because " + (args.Length == 0 ? because : string.Format(because, args));
    }
}

internal sealed class StringAssertions
{
    private readonly string? _subject;

    public StringAssertions(string? subject) => _subject = subject;

    public StringAssertions And => this;

    public StringAssertions Be(string? expected, string because = "", params object[] becauseArgs)
    {
        if (!string.Equals(_subject, expected, StringComparison.Ordinal))
        {
            ObjectAssertions<object?>.Fail($"Expected {ObjectAssertions<object?>.Format(expected)}, but found {ObjectAssertions<object?>.Format(_subject)}.");
        }

        return this;
    }

    public StringAssertions NotBeNullOrEmpty()
    {
        if (string.IsNullOrEmpty(_subject))
        {
            ObjectAssertions<object?>.Fail("Expected string not to be null or empty.");
        }

        return this;
    }

    public StringAssertions BeNull()
    {
        if (_subject is not null)
        {
            ObjectAssertions<object?>.Fail($"Expected null, but found {_subject}.");
        }

        return this;
    }

    public StringAssertions NotBeNull()
    {
        if (_subject is null)
        {
            ObjectAssertions<object?>.Fail("Expected string not to be null.");
        }

        return this;
    }

    public StringAssertions BeEmpty()
    {
        if (_subject?.Length > 0)
        {
            ObjectAssertions<object?>.Fail("Expected string to be empty.");
        }

        return this;
    }

    public StringAssertions NotBeEmpty()
    {
        if (_subject is not null && _subject.Length == 0)
        {
            ObjectAssertions<object?>.Fail("Expected string not to be empty.");
        }

        return this;
    }

    public StringAssertions Contain(string expected)
    {
        if (_subject?.Contains(expected, StringComparison.Ordinal) != true)
        {
            ObjectAssertions<object?>.Fail($"Expected string to contain {expected}.");
        }

        return this;
    }

    public StringAssertions NotContain(string expected)
    {
        if (_subject?.Contains(expected, StringComparison.Ordinal) == true)
        {
            ObjectAssertions<object?>.Fail($"Expected string not to contain {expected}.");
        }

        return this;
    }

    public StringAssertions StartWith(string expected)
    {
        if (_subject?.StartsWith(expected, StringComparison.Ordinal) != true)
        {
            ObjectAssertions<object?>.Fail($"Expected string to start with {expected}.");
        }

        return this;
    }

    public StringAssertions EndWith(string expected)
    {
        if (_subject?.EndsWith(expected, StringComparison.Ordinal) != true)
        {
            ObjectAssertions<object?>.Fail($"Expected string to end with {expected}.");
        }

        return this;
    }
}

internal sealed class EnumerableAssertions<TItem>
{
    private readonly IEnumerable<TItem>? _subject;

    public EnumerableAssertions(IEnumerable<TItem>? subject) => _subject = subject;

    public EnumerableAssertions<TItem> And => this;

    public EnumerableAssertions<TItem> BeSameAs(object? expected)
    {
        if (!ReferenceEquals(_subject, expected))
        {
            ObjectAssertions<object?>.Fail("Expected both references to point to the same object.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> BeEquivalentTo(
        IEnumerable<TItem> expected,
        Func<EquivalencyAssertionOptions, EquivalencyAssertionOptions>? configure = null)
    {
        var options = ObjectAssertions<object?>.ApplyOptions(configure);
        ObjectAssertions<object?>.AssertEquivalentSequence(SnapshotObjects(), expected.Cast<object?>().ToArray(), options.StrictOrdering);
        return this;
    }

    public EnumerableAssertions<TItem> Equal(params TItem[] expected) => Equal((IEnumerable<TItem>)expected);

    public EnumerableAssertions<TItem> Equal(IEnumerable<TItem> expected)
    {
        var actual = Snapshot();
        var expectedItems = expected.ToArray();
        if (actual.Count != expectedItems.Length)
        {
            ObjectAssertions<object?>.Fail($"Expected {expectedItems.Length} item(s), but found {actual.Count} item(s).");
        }

        for (var i = 0; i < actual.Count; i++)
        {
            if (!Equals(actual[i], expectedItems[i]))
            {
                ObjectAssertions<object?>.Fail($"Expected item at index {i} to be {ObjectAssertions<object?>.Format(expectedItems[i])}, but found {ObjectAssertions<object?>.Format(actual[i])}.");
            }
        }

        return this;
    }

    public EnumerableAssertions<TItem> AllBeEquivalentTo(TItem expected)
    {
        foreach (var item in Snapshot())
        {
            if (!Equals(item, expected))
            {
                ObjectAssertions<object?>.Fail($"Expected all items to be {ObjectAssertions<object?>.Format(expected)}, but found {ObjectAssertions<object?>.Format(item)}.");
            }
        }

        return this;
    }

    public EnumerableAssertions<TItem> BeEmpty()
    {
        if (Snapshot().Count != 0)
        {
            ObjectAssertions<object?>.Fail("Expected collection to be empty.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> NotBeEmpty()
    {
        if (Snapshot().Count == 0)
        {
            ObjectAssertions<object?>.Fail("Expected collection not to be empty.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> NotBeNull()
    {
        if (_subject is null)
        {
            ObjectAssertions<object?>.Fail("Expected collection not to be null.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> HaveCount(int expected)
    {
        var actual = Count();
        if (actual != expected)
        {
            ObjectAssertions<object?>.Fail($"Expected collection to contain {expected} item(s), but found {actual}.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> HaveCountGreaterThan(int expected)
    {
        var actual = Count();
        if (actual <= expected)
        {
            ObjectAssertions<object?>.Fail($"Expected collection count to be greater than {expected}, but found {actual}.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> HaveCountGreaterThanOrEqualTo(int expected)
    {
        var actual = Count();
        if (actual < expected)
        {
            ObjectAssertions<object?>.Fail($"Expected collection count to be greater than or equal to {expected}, but found {actual}.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> Contain(TItem expected)
    {
        if (!Snapshot().Contains(expected))
        {
            ObjectAssertions<object?>.Fail($"Expected collection to contain {ObjectAssertions<object?>.Format(expected)}.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> Contain(IEnumerable<TItem> expected)
    {
        var actual = Snapshot();
        foreach (var expectedItem in expected)
        {
            if (!actual.Contains(expectedItem))
            {
                ObjectAssertions<object?>.Fail($"Expected collection to contain {ObjectAssertions<object?>.Format(expectedItem)}.");
            }
        }

        return this;
    }

    public EnumerableAssertions<TItem> Contain(Func<TItem, bool> predicate)
    {
        if (!Snapshot().Any(predicate))
        {
            ObjectAssertions<object?>.Fail("Expected collection to contain a matching item.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> NotContain(TItem expected)
    {
        if (Snapshot().Contains(expected))
        {
            ObjectAssertions<object?>.Fail($"Expected collection not to contain {ObjectAssertions<object?>.Format(expected)}.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> NotContain(Func<TItem, bool> predicate)
    {
        if (Snapshot().Any(predicate))
        {
            ObjectAssertions<object?>.Fail("Expected collection not to contain a matching item.");
        }

        return this;
    }

    public AndWhichConstraint<TItem> ContainSingle()
    {
        var actual = Snapshot();
        if (actual.Count != 1)
        {
            ObjectAssertions<object?>.Fail($"Expected collection to contain a single item, but found {actual.Count}.");
        }

        return new AndWhichConstraint<TItem>(actual[0]);
    }

    public EnumerableAssertions<TItem> ContainInOrder(params TItem[] expected)
    {
        var actual = Snapshot();
        var searchIndex = 0;
        foreach (var expectedItem in expected)
        {
            var found = false;
            while (searchIndex < actual.Count)
            {
                if (Equals(actual[searchIndex++], expectedItem))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                ObjectAssertions<object?>.Fail($"Expected collection to contain {ObjectAssertions<object?>.Format(expectedItem)} in order.");
            }
        }

        return this;
    }

    public EnumerableAssertions<TItem> ContainKey<TKey>(TKey expected)
    {
        if (_subject is IDictionary dictionary)
        {
            if (!dictionary.Contains(expected!))
            {
                ObjectAssertions<object?>.Fail($"Expected dictionary to contain key {ObjectAssertions<object?>.Format(expected)}.");
            }

            return this;
        }

        var keyProperty = typeof(TItem).GetProperty("Key");
        if (keyProperty is null || !Snapshot().Any(item => Equals(keyProperty.GetValue(item), expected)))
        {
            ObjectAssertions<object?>.Fail($"Expected dictionary to contain key {ObjectAssertions<object?>.Format(expected)}.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> BeInAscendingOrder()
    {
        var actual = Snapshot();
        var comparer = Comparer<TItem>.Default;
        for (var i = 1; i < actual.Count; i++)
        {
            if (comparer.Compare(actual[i - 1], actual[i]) > 0)
            {
                ObjectAssertions<object?>.Fail("Expected collection to be in ascending order.");
            }
        }

        return this;
    }

    public EnumerableAssertions<TItem> StartWith(TItem expected)
    {
        var actual = Snapshot();
        if (actual.Count == 0 || !Equals(actual[0], expected))
        {
            ObjectAssertions<object?>.Fail($"Expected collection to start with {ObjectAssertions<object?>.Format(expected)}.");
        }

        return this;
    }

    public EnumerableAssertions<TItem> EndWith(TItem expected)
    {
        var actual = Snapshot();
        if (actual.Count == 0 || !Equals(actual[actual.Count - 1], expected))
        {
            ObjectAssertions<object?>.Fail($"Expected collection to end with {ObjectAssertions<object?>.Format(expected)}.");
        }

        return this;
    }

    public AndWhichConstraint<TExpected> BeOfType<TExpected>()
    {
        if (_subject is TExpected expected)
        {
            return new AndWhichConstraint<TExpected>(expected);
        }

        ObjectAssertions<object?>.Fail($"Expected value to be of type {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? "<null>"}.");
        throw new UnreachableException();
    }

    public AndWhichConstraint<TExpected> BeAssignableTo<TExpected>()
    {
        if (_subject is TExpected expected)
        {
            return new AndWhichConstraint<TExpected>(expected);
        }

        ObjectAssertions<object?>.Fail($"Expected value to be assignable to {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? "<null>"}.");
        throw new UnreachableException();
    }

    public EnumerableAssertions<TItem> Be(TItem expected)
    {
        var actual = Snapshot();
        if (actual.Count != 1 || !Equals(actual[0], expected))
        {
            ObjectAssertions<object?>.Fail($"Expected single collection item {ObjectAssertions<object?>.Format(expected)}.");
        }

        return this;
    }

    private int Count()
    {
        if (_subject is null)
        {
            ObjectAssertions<object?>.Fail("Expected collection not to be null.");
        }

        if (_subject is ICollection<TItem> collection)
        {
            return collection.Count;
        }

        if (_subject is IReadOnlyCollection<TItem> readOnlyCollection)
        {
            return readOnlyCollection.Count;
        }

        return _subject.Count();
    }

    private List<TItem> Snapshot()
    {
        if (_subject is null)
        {
            ObjectAssertions<object?>.Fail("Expected collection not to be null.");
        }

        return _subject.ToList();
    }

    private IReadOnlyList<object?> SnapshotObjects() => Snapshot().Cast<object?>().ToArray();
}

internal sealed class ActionAssertions
{
    private readonly Action _subject;

    public ActionAssertions(Action subject) => _subject = subject;

    public ExceptionAssertions<TException> Throw<TException>()
        where TException : Exception
    {
        try
        {
            _subject();
        }
        catch (Exception exception) when (exception is TException typed)
        {
            return new ExceptionAssertions<TException>(typed);
        }
        catch (Exception exception)
        {
            ObjectAssertions<object?>.Fail($"Expected exception {typeof(TException).FullName}, but found {exception.GetType().FullName}.");
        }

        ObjectAssertions<object?>.Fail($"Expected exception {typeof(TException).FullName}, but no exception was thrown.");
        throw new UnreachableException();
    }

    public ActionAssertions NotThrow()
    {
        try
        {
            _subject();
        }
        catch (Exception exception)
        {
            ObjectAssertions<object?>.Fail($"Expected no exception, but found {exception.GetType().FullName}: {exception.Message}");
        }

        return this;
    }
}

internal static class FluentActions
{
    public static Action Invoking(Action action) => action;

    public static Func<TResult> Invoking<TResult>(Func<TResult> action) => action;
}

internal sealed class FuncTaskAssertions
{
    private readonly Func<Task> _subject;

    public FuncTaskAssertions(Func<Task> subject) => _subject = subject;

    public async Task<FuncTaskAssertions> NotThrowAsync()
    {
        try
        {
            await _subject().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            ObjectAssertions<object?>.Fail($"Expected no exception, but found {exception.GetType().FullName}: {exception.Message}");
        }

        return this;
    }
}

internal sealed class ExceptionAssertions<TException>
    where TException : Exception
{
    public ExceptionAssertions(TException exception) => Which = exception;

    public TException Which { get; }

    public ExceptionAssertions<TException> WithParameterName(string expected)
    {
        if (Which is ArgumentException argumentException)
        {
            if (!string.Equals(argumentException.ParamName, expected, StringComparison.Ordinal))
            {
                ObjectAssertions<object?>.Fail($"Expected parameter name {expected}, but found {argumentException.ParamName ?? "<null>"}.");
            }

            return this;
        }

        ObjectAssertions<object?>.Fail($"Expected an ArgumentException, but found {Which.GetType().FullName}.");
        throw new UnreachableException();
    }

    public ExceptionAssertions<TException> WithInnerException<TInnerException>()
        where TInnerException : Exception
    {
        if (Which.InnerException is not TInnerException)
        {
            ObjectAssertions<object?>.Fail($"Expected inner exception {typeof(TInnerException).FullName}, but found {Which.InnerException?.GetType().FullName ?? "<null>"}.");
        }

        return this;
    }
}

internal sealed class UnreachableException : Exception;
