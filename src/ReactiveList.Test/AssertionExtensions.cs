// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CP.Primitives.Core;
using TUnit.Assertions.Exceptions;

namespace FluentAssertions;

/// <summary>Provides AssertionExtensions.</summary>
internal static class AssertionExtensions
{
    /// <summary>Provides action assertions.</summary>
    /// <param name="subject">The action under test.</param>
    extension(Action subject)
    {
        /// <summary>Creates assertions for the action.</summary>
        /// <returns>The action assertions.</returns>
        internal ActionAssertions Should() => new(subject);
    }

    /// <summary>Provides change-set assertions.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="subject">The change set under test.</param>
    extension<TItem>(ChangeSet<TItem> subject)
    {
        /// <summary>Creates assertions for the change set.</summary>
        /// <returns>The enumerable assertions.</returns>
        internal EnumerableAssertions<Change<TItem>> Should() => new(subject);
    }

    /// <summary>Provides function assertions.</summary>
    /// <typeparam name="TResult">The function result type.</typeparam>
    /// <param name="subject">The function under test.</param>
    extension<TResult>(Func<TResult> subject)
    {
        /// <summary>Creates assertions for the function.</summary>
        /// <returns>The action assertions.</returns>
        internal ActionAssertions Should() => new(() => _ = subject());
    }

    /// <summary>Provides task-producing function assertions.</summary>
    /// <param name="subject">The task-producing function under test.</param>
    extension(Func<Task> subject)
    {
        /// <summary>Creates assertions for the task-producing function.</summary>
        /// <returns>The function assertions.</returns>
        internal FuncTaskAssertions Should() => new(subject);
    }

    /// <summary>Provides enumerable assertions.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="subject">The enumerable under test.</param>
    extension<TItem>(IEnumerable<TItem>? subject)
    {
        /// <summary>Creates assertions for the enumerable.</summary>
        /// <returns>The enumerable assertions.</returns>
        internal EnumerableAssertions<TItem> Should() => new(subject);
    }

    /// <summary>Provides value-type object assertions.</summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    /// <param name="subject">The value under test.</param>
    extension<TSubject>(TSubject subject)
        where TSubject : struct
    {
        /// <summary>Creates assertions for the value.</summary>
        /// <returns>The object assertions.</returns>
        internal ObjectAssertions<TSubject> Should() => new(subject);
    }

    /// <summary>Provides Boolean assertions.</summary>
    /// <param name="subject">The Boolean value under test.</param>
    extension(bool subject)
    {
        /// <summary>Creates assertions for the Boolean value.</summary>
        /// <returns>The Boolean assertions.</returns>
        internal BooleanAssertions Should() => new(subject);
    }

    /// <summary>Provides object assertions.</summary>
    /// <param name="subject">The object under test.</param>
    extension(object? subject)
    {
        /// <summary>Creates assertions for the object.</summary>
        /// <returns>The object assertions.</returns>
        internal ObjectAssertions<object?> Should() => new(subject);
    }

    /// <summary>Provides string assertions.</summary>
    /// <param name="subject">The string under test.</param>
    extension(string? subject)
    {
        /// <summary>Creates assertions for the string.</summary>
        /// <returns>The string assertions.</returns>
        internal StringAssertions Should() => new(subject);
    }

    /// <summary>Provides shared assertion helper methods.</summary>
    internal static class AssertionHelpers
    {
        /// <summary>The assertion failure did not throw.</summary>
        internal const string AssertionFailureDidNotThrow = "The assertion failure did not throw.";

        /// <summary>The collection not null expectation.</summary>
        internal const string CollectionNotNullExpectation = "Expected collection not to be null.";

        /// <summary>The null display value.</summary>
        internal const string NullDisplayValue = "<null>";

        /// <summary>Applies equivalency options.</summary>
        /// <param name="configure">The options configuration.</param>
        /// <returns>The configured options.</returns>
        internal static EquivalencyAssertionOptions ApplyOptions(
            Func<EquivalencyAssertionOptions, EquivalencyAssertionOptions>? configure)
        {
            var options = new EquivalencyAssertionOptions();
            return configure?.Invoke(options) ?? options;
        }

        /// <summary>Throws an assertion failure.</summary>
        /// <param name="message">The failure message.</param>
        internal static void Fail(string message) => throw new AssertionException(message);

        /// <summary>Formats a value for assertion output.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The formatted value.</returns>
        internal static string Format(object? value) => value is null ? NullDisplayValue : value.ToString() ?? "<value>";

        /// <summary>Asserts sequence equivalency.</summary>
        /// <param name="actual">The actual sequence.</param>
        /// <param name="expected">The expected sequence.</param>
        /// <param name="strictOrdering">Whether ordering must match.</param>
        internal static void AssertEquivalentSequence(IReadOnlyList<object?> actual, IReadOnlyList<object?> expected, bool strictOrdering)
        {
            if (actual.Count != expected.Count)
            {
                AssertionHelpers.Fail($"Expected {expected.Count} item(s), but found {actual.Count} item(s).");
            }

            if (strictOrdering)
            {
                for (var i = 0; i < actual.Count; i++)
                {
                    if (!Equals(actual[i], expected[i]))
                    {
                        AssertionHelpers.Fail($"Expected item at index {i} to be {Format(expected[i])}, but found {Format(actual[i])}.");
                    }
                }

                return;
            }

            var unmatched = new List<object?>(expected);
            foreach (var item in actual)
            {
                var index = unmatched.FindIndex(expectedItem => Equals(item, expectedItem));
                if (index < 0)
                {
                    AssertionHelpers.Fail($"Did not expect item {Format(item)}.");
                }

                unmatched.RemoveAt(index);
            }
        }

        /// <summary>Attempts to snapshot an enumerable value.</summary>
        /// <param name="value">The value.</param>
        /// <param name="items">The snapshot items.</param>
        /// <returns>true when the value was enumerable; otherwise, false.</returns>
        internal static bool TryGetEnumerable(object? value, out IReadOnlyList<object?> items)
        {
            if (value is null || value is string || value is not IEnumerable enumerable)
            {
                items = [];
                return false;
            }

            var snapshot = new List<object?>();
            foreach (var item in enumerable)
            {
                snapshot.Add(item);
            }

            items = snapshot;
            return true;
        }

        /// <summary>Compares two values with numeric coercion when needed.</summary>
        /// <param name="actual">The actual value.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns>true when the values are equal; otherwise, false.</returns>
        internal static bool AreEqual(object? actual, object? expected)
        {
            if (Equals(actual, expected))
            {
                return true;
            }

            return actual is not IConvertible actualConvertible
                || expected is not IConvertible expectedConvertible
                || !IsNumeric(actualConvertible.GetTypeCode())
                || !IsNumeric(expectedConvertible.GetTypeCode())
                ? false
                : Convert.ToDecimal(actualConvertible, CultureInfo.InvariantCulture)
                    == Convert.ToDecimal(expectedConvertible, CultureInfo.InvariantCulture);
        }

        /// <summary>Compares two values.</summary>
        /// <param name="actual">The actual value.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns>The comparison result.</returns>
        internal static int Compare(object? actual, object? expected)
        {
            if (actual is null || expected is null)
            {
                AssertionHelpers.Fail("Cannot compare null values.");
                throw new InvalidOperationException(AssertionFailureDidNotThrow);
            }

            if (actual is IComparable comparable)
            {
                return comparable.CompareTo(expected);
            }

            AssertionHelpers.Fail($"Type {actual.GetType().FullName} is not comparable.");
            throw new InvalidOperationException(AssertionFailureDidNotThrow);
        }

        /// <summary>Formats a because clause.</summary>
        /// <param name="because">The because text.</param>
        /// <param name="args">The format arguments.</param>
        /// <returns>The formatted because clause.</returns>
        internal static string Because(string because, object[] args)
        {
            if (string.IsNullOrWhiteSpace(because))
            {
                return string.Empty;
            }

            return $" because {(args.Length == 0 ? because : string.Format(because, args))}";
        }

        /// <summary>Determines whether the type code represents a numeric value.</summary>
        /// <param name="typeCode">The type code.</param>
        /// <returns>true when the type code is numeric; otherwise, false.</returns>
        private static bool IsNumeric(TypeCode typeCode) => typeCode is >= TypeCode.SByte and <= TypeCode.Decimal;
    }

    /// <summary>Provides EquivalencyAssertionOptions.</summary>
    internal sealed class EquivalencyAssertionOptions
    {
        /// <summary>Gets StrictOrdering.</summary>
        internal bool StrictOrdering { get; private set; }

        /// <summary>Provides WithStrictOrdering.</summary>
        /// <returns>The result.</returns>
        internal EquivalencyAssertionOptions WithStrictOrdering()
        {
            StrictOrdering = true;
            return this;
        }
    }

    /// <summary>Provides AndWhichConstraint.</summary>
    /// <typeparam name="T">The T type.</typeparam>
    internal sealed class AndWhichConstraint<T>
    {
        /// <summary>Initializes a new instance of the AndWhichConstraint class.</summary>
        /// <param name="which">The which value.</param>
        internal AndWhichConstraint(T which) => Which = which;

        /// <summary>Gets Which.</summary>
        internal T Which { get; }

        /// <summary>Gets And.</summary>
        internal AndWhichConstraint<T> And => this;
    }

    /// <summary>Provides ObjectAssertions.</summary>
    /// <typeparam name="TSubject">The TSubject type.</typeparam>
    internal sealed class ObjectAssertions<TSubject>
    {
        /// <summary>The assertion subject.</summary>
        private readonly TSubject _subject;

        /// <summary>Initializes a new instance of the ObjectAssertions class.</summary>
        /// <param name="subject">The subject value.</param>
        internal ObjectAssertions(TSubject subject) => _subject = subject;

        /// <summary>Provides Be.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="because">The because value.</param>
        /// <returns>The result.</returns>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        internal ObjectAssertions<TSubject> Be<TExpected>(TExpected expected, string because = "", params object[] becauseArgs)
        {
            if (!AssertionHelpers.AreEqual(_subject, expected))
            {
                AssertionHelpers.Fail($"Expected {AssertionHelpers.Format(expected)}, but found {AssertionHelpers.Format(_subject)}{AssertionHelpers.Because(because, becauseArgs)}.");
            }

            return this;
        }

        /// <summary>Provides NotBe.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal ObjectAssertions<TSubject> NotBe<TExpected>(TExpected expected)
        {
            if (Equals(_subject, expected))
            {
                AssertionHelpers.Fail($"Expected value not to be {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides BeSameAs.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal ObjectAssertions<TSubject> BeSameAs(object? expected)
        {
            if (!ReferenceEquals(_subject, expected))
            {
                AssertionHelpers.Fail("Expected both references to point to the same object.");
            }

            return this;
        }

        /// <summary>Provides BeNull.</summary>
        /// <returns>The result.</returns>
        internal ObjectAssertions<TSubject> BeNull()
        {
            if (_subject is not null)
            {
                AssertionHelpers.Fail($"Expected null, but found {AssertionHelpers.Format(_subject)}.");
            }

            return this;
        }

        /// <summary>Provides NotBeNull.</summary>
        /// <returns>The result.</returns>
        internal ObjectAssertions<TSubject> NotBeNull()
        {
            if (_subject is null)
            {
                AssertionHelpers.Fail("Expected value not to be null.");
            }

            return this;
        }

        /// <summary>Provides BeOfType.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        internal AndWhichConstraint<TExpected> BeOfType<TExpected>()
        {
            if (_subject is TExpected expected)
            {
                return new(expected);
            }

            AssertionHelpers.Fail($"Expected value to be of type {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? AssertionHelpers.NullDisplayValue}.");
            throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
        }

        /// <summary>Provides BeAssignableTo.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        internal AndWhichConstraint<TExpected> BeAssignableTo<TExpected>()
        {
            if (_subject is TExpected expected)
            {
                return new(expected);
            }

            AssertionHelpers.Fail($"Expected value to be assignable to {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? AssertionHelpers.NullDisplayValue}.");
            throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
        }

        /// <summary>Provides BeEquivalentTo.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        /// <param name="configure">The configure value.</param>
        internal ObjectAssertions<TSubject> BeEquivalentTo<TExpected>(
            TExpected expected,
            Func<EquivalencyAssertionOptions, EquivalencyAssertionOptions>? configure = null)
        {
            var options = AssertionHelpers.ApplyOptions(configure);
            if (AssertionHelpers.TryGetEnumerable(_subject, out var actualItems) && AssertionHelpers.TryGetEnumerable(expected, out var expectedItems))
            {
                AssertionHelpers.AssertEquivalentSequence(actualItems, expectedItems, options.StrictOrdering);
                return this;
            }

            if (!Equals(_subject, expected))
            {
                AssertionHelpers.Fail($"Expected {AssertionHelpers.Format(expected)}, but found {AssertionHelpers.Format(_subject)}.");
            }

            return this;
        }

        /// <summary>Provides HaveCount.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal ObjectAssertions<TSubject> HaveCount(int expected)
        {
            var actual = SnapshotEnumerable().Count;
            if (actual != expected)
            {
                AssertionHelpers.Fail($"Expected collection to contain {expected} item(s), but found {actual}.");
            }

            return this;
        }

        /// <summary>Provides ContainSingle.</summary>
        /// <returns>The result.</returns>
        internal AndWhichConstraint<object?> ContainSingle()
        {
            var actual = SnapshotEnumerable();
            if (actual.Count != 1)
            {
                AssertionHelpers.Fail($"Expected collection to contain a single item, but found {actual.Count}.");
            }

            return new(actual[0]);
        }

        /// <summary>Provides BeGreaterThan.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <returns>The result.</returns>
        /// <param name="expected">The expected value.</param>
        internal ObjectAssertions<TSubject> BeGreaterThan<TExpected>(TExpected expected)
        {
            if (AssertionHelpers.Compare(_subject, expected) <= 0)
            {
                AssertionHelpers.Fail($"Expected {AssertionHelpers.Format(_subject)} to be greater than {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides BeGreaterThanOrEqualTo.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <returns>The result.</returns>
        /// <param name="expected">The expected value.</param>
        internal ObjectAssertions<TSubject> BeGreaterThanOrEqualTo<TExpected>(TExpected expected)
        {
            if (AssertionHelpers.Compare(_subject, expected) < 0)
            {
                AssertionHelpers.Fail($"Expected {AssertionHelpers.Format(_subject)} to be greater than or equal to {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides BeLessThanOrEqualTo.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <returns>The result.</returns>
        /// <param name="expected">The expected value.</param>
        internal ObjectAssertions<TSubject> BeLessThanOrEqualTo<TExpected>(TExpected expected)
        {
            if (AssertionHelpers.Compare(_subject, expected) > 0)
            {
                AssertionHelpers.Fail($"Expected {AssertionHelpers.Format(_subject)} to be less than or equal to {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides BeInRange.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <param name="minimum">The minimum value.</param>
        /// <param name="maximum">The maximum value.</param>
        /// <returns>The result.</returns>
        internal ObjectAssertions<TSubject> BeInRange<TExpected>(TExpected minimum, TExpected maximum)
        {
            if (AssertionHelpers.Compare(_subject, minimum) < 0 || AssertionHelpers.Compare(_subject, maximum) > 0)
            {
                AssertionHelpers.Fail($"Expected {AssertionHelpers.Format(_subject)} to be in range {AssertionHelpers.Format(minimum)}..{AssertionHelpers.Format(maximum)}.");
            }

            return this;
        }

        /// <summary>Provides SnapshotEnumerable.</summary>
        /// <returns>The result.</returns>
        private IReadOnlyList<object?> SnapshotEnumerable()
        {
            if (!AssertionHelpers.TryGetEnumerable(_subject, out var items))
            {
                AssertionHelpers.Fail($"Expected {typeof(TSubject).FullName} to be enumerable.");
            }

            return items;
        }
    }

    /// <summary>Provides BooleanAssertions.</summary>
    internal sealed class BooleanAssertions
    {
        /// <summary>The assertion subject.</summary>
        private readonly bool _subject;

        /// <summary>Initializes a new instance of the <see cref="BooleanAssertions"/> class.</summary>
        /// <param name="subject">The subject value.</param>
        internal BooleanAssertions(bool subject) => _subject = subject;

        /// <summary>Provides Be.</summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="because">The because value.</param>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        /// <returns>The result.</returns>
        internal BooleanAssertions Be(bool expected, string because = "", params object[] becauseArgs)
        {
            if (_subject != expected)
            {
                AssertionHelpers.Fail($"Expected {expected}, but found {_subject}{AssertionHelpers.Because(because, becauseArgs)}.");
            }

            return this;
        }

        /// <summary>Provides BeTrue.</summary>
        /// <param name="because">The because value.</param>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        /// <returns>The result.</returns>
        internal BooleanAssertions BeTrue(string because = "", params object[] becauseArgs) => Be(true, because, becauseArgs);

        /// <summary>Provides BeFalse.</summary>
        /// <param name="because">The because value.</param>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        /// <returns>The result.</returns>
        internal BooleanAssertions BeFalse(string because = "", params object[] becauseArgs) => Be(false, because, becauseArgs);
    }

    /// <summary>Provides StringAssertions.</summary>
    internal sealed class StringAssertions
    {
        /// <summary>The assertion subject.</summary>
        private readonly string? _subject;

        /// <summary>Initializes a new instance of the <see cref="StringAssertions"/> class.</summary>
        /// <param name="subject">The subject value.</param>
        internal StringAssertions(string? subject) => _subject = subject;

        /// <summary>Gets And.</summary>
        internal StringAssertions And => this;

        /// <summary>Provides Be.</summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="because">The because value.</param>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        /// <returns>The result.</returns>
        internal StringAssertions Be(string? expected, string because = "", params object[] becauseArgs)
        {
            if (!string.Equals(_subject, expected, StringComparison.Ordinal))
            {
                AssertionHelpers.Fail($"Expected {AssertionHelpers.Format(expected)}, but found {AssertionHelpers.Format(_subject)}.");
            }

            return this;
        }

        /// <summary>Provides NotBeNullOrEmpty.</summary>
        /// <returns>The result.</returns>
        internal StringAssertions NotBeNullOrEmpty()
        {
            if (string.IsNullOrEmpty(_subject))
            {
                AssertionHelpers.Fail("Expected string not to be null or empty.");
            }

            return this;
        }

        /// <summary>Provides BeNull.</summary>
        /// <returns>The result.</returns>
        internal StringAssertions BeNull()
        {
            if (_subject is not null)
            {
                AssertionHelpers.Fail($"Expected null, but found {_subject}.");
            }

            return this;
        }

        /// <summary>Provides NotBeNull.</summary>
        /// <returns>The result.</returns>
        internal StringAssertions NotBeNull()
        {
            if (_subject is null)
            {
                AssertionHelpers.Fail("Expected string not to be null.");
            }

            return this;
        }

        /// <summary>Provides BeEmpty.</summary>
        /// <returns>The result.</returns>
        internal StringAssertions BeEmpty()
        {
            if (_subject?.Length > 0)
            {
                AssertionHelpers.Fail("Expected string to be empty.");
            }

            return this;
        }

        /// <summary>Provides NotBeEmpty.</summary>
        /// <returns>The result.</returns>
        internal StringAssertions NotBeEmpty()
        {
            if (_subject?.Length == 0)
            {
                AssertionHelpers.Fail("Expected string not to be empty.");
            }

            return this;
        }

        /// <summary>Provides Contain.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal StringAssertions Contain(string expected)
        {
            if (_subject?.Contains(expected, StringComparison.Ordinal) != true)
            {
                AssertionHelpers.Fail($"Expected string to contain {expected}.");
            }

            return this;
        }

        /// <summary>Provides NotContain.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal StringAssertions NotContain(string expected)
        {
            if (_subject?.Contains(expected, StringComparison.Ordinal) == true)
            {
                AssertionHelpers.Fail($"Expected string not to contain {expected}.");
            }

            return this;
        }

        /// <summary>Provides StartWith.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal StringAssertions StartWith(string expected)
        {
            if (_subject?.StartsWith(expected, StringComparison.Ordinal) != true)
            {
                AssertionHelpers.Fail($"Expected string to start with {expected}.");
            }

            return this;
        }

        /// <summary>Provides EndWith.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal StringAssertions EndWith(string expected)
        {
            if (_subject?.EndsWith(expected, StringComparison.Ordinal) != true)
            {
                AssertionHelpers.Fail($"Expected string to end with {expected}.");
            }

            return this;
        }
    }

    /// <summary>Provides EnumerableAssertions.</summary>
    /// <typeparam name="TItem">The TItem type.</typeparam>
    internal sealed class EnumerableAssertions<TItem>
    {
        /// <summary>The assertion subject.</summary>
        private readonly IEnumerable<TItem>? _subject;

        /// <summary>Initializes a new instance of the EnumerableAssertions class.</summary>
        /// <param name="subject">The subject value.</param>
        internal EnumerableAssertions(IEnumerable<TItem>? subject) => _subject = subject;

        /// <summary>Gets And.</summary>
        internal EnumerableAssertions<TItem> And => this;

        /// <summary>Provides BeSameAs.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> BeSameAs(object? expected)
        {
            if (!ReferenceEquals(_subject, expected))
            {
                AssertionHelpers.Fail("Expected both references to point to the same object.");
            }

            return this;
        }

        /// <summary>Provides BeEquivalentTo.</summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="configure">The configure value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> BeEquivalentTo(
            IEnumerable<TItem> expected,
            Func<EquivalencyAssertionOptions, EquivalencyAssertionOptions>? configure = null)
        {
            var options = AssertionHelpers.ApplyOptions(configure);
            var expectedItems = new List<object?>();
            foreach (var item in expected)
            {
                expectedItems.Add(item);
            }

            AssertionHelpers.AssertEquivalentSequence(SnapshotObjects(), expectedItems, options.StrictOrdering);
            return this;
        }

        /// <summary>Provides Equal.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> Equal(params TItem[] expected) => Equal((IEnumerable<TItem>)expected);

        /// <summary>Provides Equal.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> Equal(IEnumerable<TItem> expected)
        {
            var actual = Snapshot();
            var expectedItems = new List<TItem>(expected);
            if (actual.Count != expectedItems.Count)
            {
                AssertionHelpers.Fail($"Expected {expectedItems.Count} item(s), but found {actual.Count} item(s).");
            }

            for (var i = 0; i < actual.Count; i++)
            {
                if (!EqualityComparer<TItem>.Default.Equals(actual[i], expectedItems[i]))
                {
                    AssertionHelpers.Fail($"Expected item at index {i} to be {AssertionHelpers.Format(expectedItems[i])}, but found {AssertionHelpers.Format(actual[i])}.");
                }
            }

            return this;
        }

        /// <summary>Provides AllBeEquivalentTo.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> AllBeEquivalentTo(TItem expected)
        {
            foreach (var item in Snapshot())
            {
                if (!EqualityComparer<TItem>.Default.Equals(item, expected))
                {
                    AssertionHelpers.Fail($"Expected all items to be {AssertionHelpers.Format(expected)}, but found {AssertionHelpers.Format(item)}.");
                }
            }

            return this;
        }

        /// <summary>Provides BeEmpty.</summary>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> BeEmpty()
        {
            if (Snapshot().Count != 0)
            {
                AssertionHelpers.Fail("Expected collection to be empty.");
            }

            return this;
        }

        /// <summary>Provides NotBeEmpty.</summary>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> NotBeEmpty()
        {
            if (Snapshot().Count == 0)
            {
                AssertionHelpers.Fail("Expected collection not to be empty.");
            }

            return this;
        }

        /// <summary>Provides NotBeNull.</summary>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> NotBeNull()
        {
            if (_subject is null)
            {
                AssertionHelpers.Fail(AssertionHelpers.CollectionNotNullExpectation);
                throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
            }

            return this;
        }

        /// <summary>Provides HaveCount.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> HaveCount(int expected)
        {
            var actual = Count();
            if (actual != expected)
            {
                AssertionHelpers.Fail($"Expected collection to contain {expected} item(s), but found {actual}.");
            }

            return this;
        }

        /// <summary>Provides HaveCountGreaterThan.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> HaveCountGreaterThan(int expected)
        {
            var actual = Count();
            if (actual <= expected)
            {
                AssertionHelpers.Fail($"Expected collection count to be greater than {expected}, but found {actual}.");
            }

            return this;
        }

        /// <summary>Provides HaveCountGreaterThanOrEqualTo.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> HaveCountGreaterThanOrEqualTo(int expected)
        {
            var actual = Count();
            if (actual < expected)
            {
                AssertionHelpers.Fail($"Expected collection count to be greater than or equal to {expected}, but found {actual}.");
            }

            return this;
        }

        /// <summary>Provides Contain.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> Contain(TItem expected)
        {
            if (!Snapshot().Contains(expected))
            {
                AssertionHelpers.Fail($"Expected collection to contain {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides Contain.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> Contain(IEnumerable<TItem> expected)
        {
            var actual = Snapshot();
            foreach (var expectedItem in expected)
            {
                if (!actual.Contains(expectedItem))
                {
                    AssertionHelpers.Fail($"Expected collection to contain {AssertionHelpers.Format(expectedItem)}.");
                }
            }

            return this;
        }

        /// <summary>Provides Contain.</summary>
        /// <param name="predicate">The predicate value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> Contain(Func<TItem, bool> predicate)
        {
            if (!Snapshot().Exists(item => predicate(item)))
            {
                AssertionHelpers.Fail("Expected collection to contain a matching item.");
            }

            return this;
        }

        /// <summary>Provides NotContain.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> NotContain(TItem expected)
        {
            if (Snapshot().Contains(expected))
            {
                AssertionHelpers.Fail($"Expected collection not to contain {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides NotContain.</summary>
        /// <param name="predicate">The predicate value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> NotContain(Func<TItem, bool> predicate)
        {
            if (Snapshot().Exists(item => predicate(item)))
            {
                AssertionHelpers.Fail("Expected collection not to contain a matching item.");
            }

            return this;
        }

        /// <summary>Provides ContainSingle.</summary>
        /// <returns>The result.</returns>
        internal AndWhichConstraint<TItem> ContainSingle()
        {
            var actual = Snapshot();
            if (actual.Count != 1)
            {
                AssertionHelpers.Fail($"Expected collection to contain a single item, but found {actual.Count}.");
            }

            return new(actual[0]);
        }

        /// <summary>Provides ContainInOrder.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> ContainInOrder(params TItem[] expected)
        {
            var actual = Snapshot();
            var searchIndex = 0;
            foreach (var expectedItem in expected)
            {
                var found = false;
                while (searchIndex < actual.Count)
                {
                    var actualItem = actual[searchIndex];
                    searchIndex++;
                    if (EqualityComparer<TItem>.Default.Equals(actualItem, expectedItem))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    AssertionHelpers.Fail($"Expected collection to contain {AssertionHelpers.Format(expectedItem)} in order.");
                }
            }

            return this;
        }

        /// <summary>Provides ContainKey.</summary>
        /// <typeparam name="TKey">The TKey type.</typeparam>
        /// <returns>The result.</returns>
        /// <param name="expected">The expected value.</param>
        internal EnumerableAssertions<TItem> ContainKey<TKey>(TKey expected)
        {
            if (_subject is IDictionary dictionary)
            {
                if (expected is null || !dictionary.Contains(expected))
                {
                    AssertionHelpers.Fail($"Expected dictionary to contain key {AssertionHelpers.Format(expected)}.");
                }

                return this;
            }

            var keyProperty = typeof(TItem).GetProperty("Key");
            var containsKey = false;
            if (keyProperty is not null)
            {
                foreach (var item in Snapshot())
                {
                    if (Equals(keyProperty.GetValue(item), expected))
                    {
                        containsKey = true;
                        break;
                    }
                }
            }

            if (!containsKey)
            {
                AssertionHelpers.Fail($"Expected dictionary to contain key {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides BeInAscendingOrder.</summary>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> BeInAscendingOrder()
        {
            var actual = Snapshot();
            var comparer = Comparer<TItem>.Default;
            for (var i = 1; i < actual.Count; i++)
            {
                if (comparer.Compare(actual[i - 1], actual[i]) > 0)
                {
                    AssertionHelpers.Fail("Expected collection to be in ascending order.");
                }
            }

            return this;
        }

        /// <summary>Provides StartWith.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> StartWith(TItem expected)
        {
            var actual = Snapshot();
            if (actual.Count == 0 || !EqualityComparer<TItem>.Default.Equals(actual[0], expected))
            {
                AssertionHelpers.Fail($"Expected collection to start with {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides EndWith.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> EndWith(TItem expected)
        {
            var actual = Snapshot();
            if (actual.Count == 0 || !EqualityComparer<TItem>.Default.Equals(actual[actual.Count - 1], expected))
            {
                AssertionHelpers.Fail($"Expected collection to end with {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides BeOfType.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        internal AndWhichConstraint<TExpected> BeOfType<TExpected>()
        {
            if (_subject is TExpected expected)
            {
                return new(expected);
            }

            AssertionHelpers.Fail($"Expected value to be of type {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? AssertionHelpers.NullDisplayValue}.");
            throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
        }

        /// <summary>Provides BeAssignableTo.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        internal AndWhichConstraint<TExpected> BeAssignableTo<TExpected>()
        {
            if (_subject is TExpected expected)
            {
                return new(expected);
            }

            AssertionHelpers.Fail($"Expected value to be assignable to {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? AssertionHelpers.NullDisplayValue}.");
            throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
        }

        /// <summary>Provides Be.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal EnumerableAssertions<TItem> Be(TItem expected)
        {
            var actual = Snapshot();
            if (actual.Count != 1 || !EqualityComparer<TItem>.Default.Equals(actual[0], expected))
            {
                AssertionHelpers.Fail($"Expected single collection item {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides Count.</summary>
        /// <returns>The result.</returns>
        private int Count()
        {
            if (_subject is null)
            {
                AssertionHelpers.Fail(AssertionHelpers.CollectionNotNullExpectation);
                throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
            }

            if (_subject is ICollection<TItem> collection)
            {
                return collection.Count;
            }

            if (_subject is IReadOnlyCollection<TItem> readOnlyCollection)
            {
                return readOnlyCollection.Count;
            }

            var count = 0;
            foreach (var _ in _subject)
            {
                count++;
            }

            return count;
        }

        /// <summary>Provides Snapshot.</summary>
        /// <returns>The result.</returns>
        private List<TItem> Snapshot()
        {
            if (_subject is null)
            {
                AssertionHelpers.Fail(AssertionHelpers.CollectionNotNullExpectation);
                throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
            }

            return new(_subject);
        }

        /// <summary>Provides SnapshotObjects.</summary>
        /// <returns>The result.</returns>
        private object?[] SnapshotObjects()
        {
            var snapshot = Snapshot();
            var objects = new object?[snapshot.Count];
            for (var i = 0; i < snapshot.Count; i++)
            {
                objects[i] = snapshot[i];
            }

            return objects;
        }
    }

    /// <summary>Provides ActionAssertions.</summary>
    internal sealed class ActionAssertions
    {
        /// <summary>The assertion subject.</summary>
        private readonly Action _subject;

        /// <summary>Initializes a new instance of the <see cref="ActionAssertions"/> class.</summary>
        /// <param name="subject">The subject value.</param>
        internal ActionAssertions(Action subject) => _subject = subject;

        /// <summary>Provides Throw.</summary>
        /// <typeparam name="TException">The TException type.</typeparam>
        /// <returns>The result.</returns>
        internal ExceptionAssertions<TException> Throw<TException>()
            where TException : Exception
        {
            try
            {
                _subject();
            }
            catch (Exception exception) when (exception is TException typed)
            {
                return new(typed);
            }
            catch (Exception exception)
            {
                AssertionHelpers.Fail($"Expected exception {typeof(TException).FullName}, but found {exception.GetType().FullName}.");
            }

            AssertionHelpers.Fail($"Expected exception {typeof(TException).FullName}, but no exception was thrown.");
            throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
        }

        /// <summary>Provides NotThrow.</summary>
        /// <returns>The result.</returns>
        internal ActionAssertions NotThrow()
        {
            try
            {
                _subject();
            }
            catch (Exception exception)
            {
                AssertionHelpers.Fail($"Expected no exception, but found {exception.GetType().FullName}: {exception.Message}");
            }

            return this;
        }
    }

    /// <summary>Provides FuncTaskAssertions.</summary>
    internal sealed class FuncTaskAssertions
    {
        /// <summary>The assertion subject.</summary>
        private readonly Func<Task> _subject;

        /// <summary>Initializes a new instance of the <see cref="FuncTaskAssertions"/> class.</summary>
        /// <param name="subject">The subject value.</param>
        internal FuncTaskAssertions(Func<Task> subject) => _subject = subject;

        /// <summary>Provides NotThrowAsync.</summary>
        /// <returns>The result.</returns>
        internal async Task<FuncTaskAssertions> NotThrowAsync()
        {
            try
            {
                await _subject().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AssertionHelpers.Fail($"Expected no exception, but found {exception.GetType().FullName}: {exception.Message}");
            }

            return this;
        }
    }

    /// <summary>Provides ExceptionAssertions.</summary>
    /// <typeparam name="TException">The TException type.</typeparam>
    internal sealed class ExceptionAssertions<TException>
        where TException : Exception
    {
        /// <summary>Initializes a new instance of the ExceptionAssertions class.</summary>
        /// <param name="exception">The exception value.</param>
        internal ExceptionAssertions(TException exception) => Which = exception;

        /// <summary>Gets Which.</summary>
        internal TException Which { get; }

        /// <summary>Provides WithParameterName.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        internal ExceptionAssertions<TException> WithParameterName(string expected)
        {
            if (Which is ArgumentException argumentException)
            {
                if (!string.Equals(argumentException.ParamName, expected, StringComparison.Ordinal))
                {
                    AssertionHelpers.Fail($"Expected parameter name {expected}, but found {argumentException.ParamName ?? AssertionHelpers.NullDisplayValue}.");
                }

                return this;
            }

            AssertionHelpers.Fail($"Expected an ArgumentException, but found {Which.GetType().FullName}.");
            throw new InvalidOperationException(AssertionHelpers.AssertionFailureDidNotThrow);
        }

        /// <summary>Provides WithInnerException.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TInnerException">The TInnerException type.</typeparam>
        internal ExceptionAssertions<TException> WithInnerException<TInnerException>()
            where TInnerException : Exception
        {
            if (Which.InnerException is not TInnerException)
            {
                AssertionHelpers.Fail($"Expected inner exception {typeof(TInnerException).FullName}, but found {Which.InnerException?.GetType().FullName ?? AssertionHelpers.NullDisplayValue}.");
            }

            return this;
        }
    }
}
