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

/// <summary>Provides AssertionExtensions.</summary>
internal static class AssertionExtensions
{
    /// <summary>Provides action assertions.</summary>
    /// <param name="subject">The action under test.</param>
    extension(Action subject)
    {
        /// <summary>Creates assertions for the action.</summary>
        /// <returns>The action assertions.</returns>
        public ActionAssertions Should() => new(subject);
    }

    /// <summary>Provides change-set assertions.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="subject">The change set under test.</param>
    extension<TItem>(ChangeSet<TItem> subject)
    {
        /// <summary>Creates assertions for the change set.</summary>
        /// <returns>The enumerable assertions.</returns>
        public EnumerableAssertions<Change<TItem>> Should() => new(subject);
    }

    /// <summary>Provides function assertions.</summary>
    /// <typeparam name="TResult">The function result type.</typeparam>
    /// <param name="subject">The function under test.</param>
    extension<TResult>(Func<TResult> subject)
    {
        /// <summary>Creates assertions for the function.</summary>
        /// <returns>The action assertions.</returns>
        public ActionAssertions Should() => new(() => _ = subject());
    }

    /// <summary>Provides task-producing function assertions.</summary>
    /// <param name="subject">The task-producing function under test.</param>
    extension(Func<Task> subject)
    {
        /// <summary>Creates assertions for the task-producing function.</summary>
        /// <returns>The function assertions.</returns>
        public FuncTaskAssertions Should() => new(subject);
    }

    /// <summary>Provides enumerable assertions.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="subject">The enumerable under test.</param>
    extension<TItem>(IEnumerable<TItem>? subject)
    {
        /// <summary>Creates assertions for the enumerable.</summary>
        /// <returns>The enumerable assertions.</returns>
        public EnumerableAssertions<TItem> Should() => new(subject);
    }

    /// <summary>Provides value-type object assertions.</summary>
    /// <typeparam name="TSubject">The subject type.</typeparam>
    /// <param name="subject">The value under test.</param>
    extension<TSubject>(TSubject subject)
        where TSubject : struct
    {
        /// <summary>Creates assertions for the value.</summary>
        /// <returns>The object assertions.</returns>
        public ObjectAssertions<TSubject> Should() => new(subject);
    }

    /// <summary>Provides Boolean assertions.</summary>
    /// <param name="subject">The Boolean value under test.</param>
    extension(bool subject)
    {
        /// <summary>Creates assertions for the Boolean value.</summary>
        /// <returns>The Boolean assertions.</returns>
        public BooleanAssertions Should() => new(subject);
    }

    /// <summary>Provides object assertions.</summary>
    /// <param name="subject">The object under test.</param>
    extension(object? subject)
    {
        /// <summary>Creates assertions for the object.</summary>
        /// <returns>The object assertions.</returns>
        public ObjectAssertions<object?> Should() => new(subject);
    }

    /// <summary>Provides string assertions.</summary>
    /// <param name="subject">The string under test.</param>
    extension(string? subject)
    {
        /// <summary>Creates assertions for the string.</summary>
        /// <returns>The string assertions.</returns>
        public StringAssertions Should() => new(subject);
    }

    /// <summary>Provides shared assertion helper methods.</summary>
    internal static class AssertionHelpers
    {
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
        internal static string Format(object? value) => value is null ? "<null>" : value.ToString() ?? "<value>";

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

            var unmatched = expected.ToList();
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

            items = enumerable.Cast<object?>().ToArray();
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

            if (actual is not IConvertible actualConvertible ||
                expected is not IConvertible expectedConvertible ||
                !IsNumeric(actualConvertible.GetTypeCode()) ||
                !IsNumeric(expectedConvertible.GetTypeCode()))
            {
                return false;
            }

            return Convert.ToDecimal(actualConvertible, CultureInfo.InvariantCulture) ==
                Convert.ToDecimal(expectedConvertible, CultureInfo.InvariantCulture);
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
                throw new InvalidOperationException("The assertion failure did not throw.");
            }

            if (actual is IComparable comparable)
            {
                return comparable.CompareTo(expected);
            }

            AssertionHelpers.Fail($"Type {actual.GetType().FullName} is not comparable.");
            throw new InvalidOperationException("The assertion failure did not throw.");
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

            return " because " + (args.Length == 0 ? because : string.Format(because, args));
        }

        /// <summary>Determines whether the type code represents a numeric value.</summary>
        /// <param name="typeCode">The type code.</param>
        /// <returns>true when the type code is numeric; otherwise, false.</returns>
        private static bool IsNumeric(TypeCode typeCode) =>
            typeCode is TypeCode.Byte or TypeCode.SByte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64
                or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    }

    /// <summary>Provides EquivalencyAssertionOptions.</summary>
    internal sealed class EquivalencyAssertionOptions
    {
        /// <summary>Gets StrictOrdering.</summary>
        public bool StrictOrdering { get; private set; }

        /// <summary>Provides WithStrictOrdering.</summary>
        /// <returns>The result.</returns>
        public EquivalencyAssertionOptions WithStrictOrdering()
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
        public AndWhichConstraint(T which) => Which = which;

        /// <summary>Gets Which.</summary>
        public T Which { get; }

        /// <summary>Gets And.</summary>
        public AndWhichConstraint<T> And => this;
    }

    /// <summary>Provides ObjectAssertions.</summary>
    /// <typeparam name="TSubject">The TSubject type.</typeparam>
    internal sealed class ObjectAssertions<TSubject>
    {
        private readonly TSubject _subject;

        /// <summary>Initializes a new instance of the ObjectAssertions class.</summary>
        /// <param name="subject">The subject value.</param>
        public ObjectAssertions(TSubject subject) => _subject = subject;

        /// <summary>Provides Be.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="because">The because value.</param>
        /// <returns>The result.</returns>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        public ObjectAssertions<TSubject> Be<TExpected>(TExpected expected, string because = "", params object[] becauseArgs)
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
        public ObjectAssertions<TSubject> NotBe<TExpected>(TExpected expected)
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
        public ObjectAssertions<TSubject> BeSameAs(object? expected)
        {
            if (!ReferenceEquals(_subject, expected))
            {
                AssertionHelpers.Fail("Expected both references to point to the same object.");
            }

            return this;
        }

        /// <summary>Provides BeNull.</summary>
        /// <returns>The result.</returns>
        public ObjectAssertions<TSubject> BeNull()
        {
            if (_subject is not null)
            {
                AssertionHelpers.Fail($"Expected null, but found {AssertionHelpers.Format(_subject)}.");
            }

            return this;
        }

        /// <summary>Provides NotBeNull.</summary>
        /// <returns>The result.</returns>
        public ObjectAssertions<TSubject> NotBeNull()
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
        public AndWhichConstraint<TExpected> BeOfType<TExpected>()
        {
            if (_subject is TExpected expected)
            {
                return new AndWhichConstraint<TExpected>(expected);
            }

            AssertionHelpers.Fail($"Expected value to be of type {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? "<null>"}.");
            throw new InvalidOperationException("The assertion failure did not throw.");
        }

        /// <summary>Provides BeAssignableTo.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        public AndWhichConstraint<TExpected> BeAssignableTo<TExpected>()
        {
            if (_subject is TExpected expected)
            {
                return new AndWhichConstraint<TExpected>(expected);
            }

            AssertionHelpers.Fail($"Expected value to be assignable to {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? "<null>"}.");
            throw new InvalidOperationException("The assertion failure did not throw.");
        }

        /// <summary>Provides BeEquivalentTo.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        /// <param name="configure">The configure value.</param>
        public ObjectAssertions<TSubject> BeEquivalentTo<TExpected>(
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
        public ObjectAssertions<TSubject> HaveCount(int expected)
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
        public AndWhichConstraint<object?> ContainSingle()
        {
            var actual = SnapshotEnumerable();
            if (actual.Count != 1)
            {
                AssertionHelpers.Fail($"Expected collection to contain a single item, but found {actual.Count}.");
            }

            return new AndWhichConstraint<object?>(actual[0]);
        }

        /// <summary>Provides BeGreaterThan.</summary>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        /// <returns>The result.</returns>
        /// <param name="expected">The expected value.</param>
        public ObjectAssertions<TSubject> BeGreaterThan<TExpected>(TExpected expected)
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
        public ObjectAssertions<TSubject> BeGreaterThanOrEqualTo<TExpected>(TExpected expected)
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
        public ObjectAssertions<TSubject> BeLessThanOrEqualTo<TExpected>(TExpected expected)
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
        public ObjectAssertions<TSubject> BeInRange<TExpected>(TExpected minimum, TExpected maximum)
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
        private readonly bool _subject;

        /// <summary>Initializes a new instance of the <see cref="BooleanAssertions"/> class.</summary>
        /// <param name="subject">The subject value.</param>
        public BooleanAssertions(bool subject) => _subject = subject;

        /// <summary>Provides Be.</summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="because">The because value.</param>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        /// <returns>The result.</returns>
        public BooleanAssertions Be(bool expected, string because = "", params object[] becauseArgs)
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
        public BooleanAssertions BeTrue(string because = "", params object[] becauseArgs) => Be(true, because, becauseArgs);

        /// <summary>Provides BeFalse.</summary>
        /// <param name="because">The because value.</param>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        /// <returns>The result.</returns>
        public BooleanAssertions BeFalse(string because = "", params object[] becauseArgs) => Be(false, because, becauseArgs);

        /// <summary>Provides Because.</summary>
        /// <param name="because">The because value.</param>
        /// <param name="args">The args value.</param>
        /// <returns>The result.</returns>
        private static string Because(string because, object[] args)
        {
            if (string.IsNullOrWhiteSpace(because))
            {
                return string.Empty;
            }

            return " because " + (args.Length == 0 ? because : string.Format(because, args));
        }
    }

    /// <summary>Provides StringAssertions.</summary>
    internal sealed class StringAssertions
    {
        private readonly string? _subject;

        /// <summary>Initializes a new instance of the <see cref="StringAssertions"/> class.</summary>
        /// <param name="subject">The subject value.</param>
        public StringAssertions(string? subject) => _subject = subject;

        /// <summary>Gets And.</summary>
        public StringAssertions And => this;

        /// <summary>Provides Be.</summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="because">The because value.</param>
        /// <param name="becauseArgs">The becauseArgs value.</param>
        /// <returns>The result.</returns>
        public StringAssertions Be(string? expected, string because = "", params object[] becauseArgs)
        {
            if (!string.Equals(_subject, expected, StringComparison.Ordinal))
            {
                AssertionHelpers.Fail($"Expected {AssertionHelpers.Format(expected)}, but found {AssertionHelpers.Format(_subject)}.");
            }

            return this;
        }

        /// <summary>Provides NotBeNullOrEmpty.</summary>
        /// <returns>The result.</returns>
        public StringAssertions NotBeNullOrEmpty()
        {
            if (string.IsNullOrEmpty(_subject))
            {
                AssertionHelpers.Fail("Expected string not to be null or empty.");
            }

            return this;
        }

        /// <summary>Provides BeNull.</summary>
        /// <returns>The result.</returns>
        public StringAssertions BeNull()
        {
            if (_subject is not null)
            {
                AssertionHelpers.Fail($"Expected null, but found {_subject}.");
            }

            return this;
        }

        /// <summary>Provides NotBeNull.</summary>
        /// <returns>The result.</returns>
        public StringAssertions NotBeNull()
        {
            if (_subject is null)
            {
                AssertionHelpers.Fail("Expected string not to be null.");
            }

            return this;
        }

        /// <summary>Provides BeEmpty.</summary>
        /// <returns>The result.</returns>
        public StringAssertions BeEmpty()
        {
            if (_subject?.Length > 0)
            {
                AssertionHelpers.Fail("Expected string to be empty.");
            }

            return this;
        }

        /// <summary>Provides NotBeEmpty.</summary>
        /// <returns>The result.</returns>
        public StringAssertions NotBeEmpty()
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
        public StringAssertions Contain(string expected)
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
        public StringAssertions NotContain(string expected)
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
        public StringAssertions StartWith(string expected)
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
        public StringAssertions EndWith(string expected)
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
        private readonly IEnumerable<TItem>? _subject;

        /// <summary>Initializes a new instance of the EnumerableAssertions class.</summary>
        /// <param name="subject">The subject value.</param>
        public EnumerableAssertions(IEnumerable<TItem>? subject) => _subject = subject;

        /// <summary>Gets And.</summary>
        public EnumerableAssertions<TItem> And => this;

        /// <summary>Provides BeSameAs.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> BeSameAs(object? expected)
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
        public EnumerableAssertions<TItem> BeEquivalentTo(
            IEnumerable<TItem> expected,
            Func<EquivalencyAssertionOptions, EquivalencyAssertionOptions>? configure = null)
        {
            var options = AssertionHelpers.ApplyOptions(configure);
            AssertionHelpers.AssertEquivalentSequence(SnapshotObjects(), expected.Cast<object?>().ToArray(), options.StrictOrdering);
            return this;
        }

        /// <summary>Provides Equal.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> Equal(params TItem[] expected) => Equal((IEnumerable<TItem>)expected);

        /// <summary>Provides Equal.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> Equal(IEnumerable<TItem> expected)
        {
            var actual = Snapshot();
            var expectedItems = expected.ToArray();
            if (actual.Count != expectedItems.Length)
            {
                AssertionHelpers.Fail($"Expected {expectedItems.Length} item(s), but found {actual.Count} item(s).");
            }

            for (var i = 0; i < actual.Count; i++)
            {
                if (!Equals(actual[i], expectedItems[i]))
                {
                    AssertionHelpers.Fail($"Expected item at index {i} to be {AssertionHelpers.Format(expectedItems[i])}, but found {AssertionHelpers.Format(actual[i])}.");
                }
            }

            return this;
        }

        /// <summary>Provides AllBeEquivalentTo.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> AllBeEquivalentTo(TItem expected)
        {
            foreach (var item in Snapshot())
            {
                if (!Equals(item, expected))
                {
                    AssertionHelpers.Fail($"Expected all items to be {AssertionHelpers.Format(expected)}, but found {AssertionHelpers.Format(item)}.");
                }
            }

            return this;
        }

        /// <summary>Provides BeEmpty.</summary>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> BeEmpty()
        {
            if (Snapshot().Count != 0)
            {
                AssertionHelpers.Fail("Expected collection to be empty.");
            }

            return this;
        }

        /// <summary>Provides NotBeEmpty.</summary>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> NotBeEmpty()
        {
            if (Snapshot().Count == 0)
            {
                AssertionHelpers.Fail("Expected collection not to be empty.");
            }

            return this;
        }

        /// <summary>Provides NotBeNull.</summary>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> NotBeNull()
        {
            if (_subject is null)
            {
                AssertionHelpers.Fail("Expected collection not to be null.");
                throw new InvalidOperationException("The assertion failure did not throw.");
            }

            return this;
        }

        /// <summary>Provides HaveCount.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> HaveCount(int expected)
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
        public EnumerableAssertions<TItem> HaveCountGreaterThan(int expected)
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
        public EnumerableAssertions<TItem> HaveCountGreaterThanOrEqualTo(int expected)
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
        public EnumerableAssertions<TItem> Contain(TItem expected)
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
        public EnumerableAssertions<TItem> Contain(IEnumerable<TItem> expected)
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
        public EnumerableAssertions<TItem> Contain(Func<TItem, bool> predicate)
        {
            if (!Snapshot().Any(predicate))
            {
                AssertionHelpers.Fail("Expected collection to contain a matching item.");
            }

            return this;
        }

        /// <summary>Provides NotContain.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> NotContain(TItem expected)
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
        public EnumerableAssertions<TItem> NotContain(Func<TItem, bool> predicate)
        {
            if (Snapshot().Any(predicate))
            {
                AssertionHelpers.Fail("Expected collection not to contain a matching item.");
            }

            return this;
        }

        /// <summary>Provides ContainSingle.</summary>
        /// <returns>The result.</returns>
        public AndWhichConstraint<TItem> ContainSingle()
        {
            var actual = Snapshot();
            if (actual.Count != 1)
            {
                AssertionHelpers.Fail($"Expected collection to contain a single item, but found {actual.Count}.");
            }

            return new AndWhichConstraint<TItem>(actual[0]);
        }

        /// <summary>Provides ContainInOrder.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
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
                    AssertionHelpers.Fail($"Expected collection to contain {AssertionHelpers.Format(expectedItem)} in order.");
                }
            }

            return this;
        }

        /// <summary>Provides ContainKey.</summary>
        /// <typeparam name="TKey">The TKey type.</typeparam>
        /// <returns>The result.</returns>
        /// <param name="expected">The expected value.</param>
        public EnumerableAssertions<TItem> ContainKey<TKey>(TKey expected)
        {
            if (_subject is IDictionary dictionary)
            {
                if (!dictionary.Contains(expected!))
                {
                    AssertionHelpers.Fail($"Expected dictionary to contain key {AssertionHelpers.Format(expected)}.");
                }

                return this;
            }

            var keyProperty = typeof(TItem).GetProperty("Key");
            if (keyProperty is null || !Snapshot().Any(item => Equals(keyProperty.GetValue(item), expected)))
            {
                AssertionHelpers.Fail($"Expected dictionary to contain key {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides BeInAscendingOrder.</summary>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> BeInAscendingOrder()
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
        public EnumerableAssertions<TItem> StartWith(TItem expected)
        {
            var actual = Snapshot();
            if (actual.Count == 0 || !Equals(actual[0], expected))
            {
                AssertionHelpers.Fail($"Expected collection to start with {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides EndWith.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> EndWith(TItem expected)
        {
            var actual = Snapshot();
            if (actual.Count == 0 || !Equals(actual[actual.Count - 1], expected))
            {
                AssertionHelpers.Fail($"Expected collection to end with {AssertionHelpers.Format(expected)}.");
            }

            return this;
        }

        /// <summary>Provides BeOfType.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        public AndWhichConstraint<TExpected> BeOfType<TExpected>()
        {
            if (_subject is TExpected expected)
            {
                return new AndWhichConstraint<TExpected>(expected);
            }

            AssertionHelpers.Fail($"Expected value to be of type {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? "<null>"}.");
            throw new InvalidOperationException("The assertion failure did not throw.");
        }

        /// <summary>Provides BeAssignableTo.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TExpected">The TExpected type.</typeparam>
        public AndWhichConstraint<TExpected> BeAssignableTo<TExpected>()
        {
            if (_subject is TExpected expected)
            {
                return new AndWhichConstraint<TExpected>(expected);
            }

            AssertionHelpers.Fail($"Expected value to be assignable to {typeof(TExpected).FullName}, but found {_subject?.GetType().FullName ?? "<null>"}.");
            throw new InvalidOperationException("The assertion failure did not throw.");
        }

        /// <summary>Provides Be.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public EnumerableAssertions<TItem> Be(TItem expected)
        {
            var actual = Snapshot();
            if (actual.Count != 1 || !Equals(actual[0], expected))
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
                AssertionHelpers.Fail("Expected collection not to be null.");
                throw new InvalidOperationException("The assertion failure did not throw.");
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

        /// <summary>Provides Snapshot.</summary>
        /// <returns>The result.</returns>
        private List<TItem> Snapshot()
        {
            if (_subject is null)
            {
                AssertionHelpers.Fail("Expected collection not to be null.");
                throw new InvalidOperationException("The assertion failure did not throw.");
            }

            return _subject.ToList();
        }

        /// <summary>Provides SnapshotObjects.</summary>
        /// <returns>The result.</returns>
        private object?[] SnapshotObjects() => Snapshot().Cast<object?>().ToArray();
    }

    /// <summary>Provides ActionAssertions.</summary>
    internal sealed class ActionAssertions
    {
        private readonly Action _subject;

        /// <summary>Initializes a new instance of the <see cref="ActionAssertions"/> class.</summary>
        /// <param name="subject">The subject value.</param>
        public ActionAssertions(Action subject) => _subject = subject;

        /// <summary>Provides Throw.</summary>
        /// <typeparam name="TException">The TException type.</typeparam>
        /// <returns>The result.</returns>
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
                AssertionHelpers.Fail($"Expected exception {typeof(TException).FullName}, but found {exception.GetType().FullName}.");
            }

            AssertionHelpers.Fail($"Expected exception {typeof(TException).FullName}, but no exception was thrown.");
            throw new InvalidOperationException("The assertion failure did not throw.");
        }

        /// <summary>Provides NotThrow.</summary>
        /// <returns>The result.</returns>
        public ActionAssertions NotThrow()
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
        private readonly Func<Task> _subject;

        /// <summary>Initializes a new instance of the <see cref="FuncTaskAssertions"/> class.</summary>
        /// <param name="subject">The subject value.</param>
        public FuncTaskAssertions(Func<Task> subject) => _subject = subject;

        /// <summary>Provides NotThrowAsync.</summary>
        /// <returns>The result.</returns>
        public async Task<FuncTaskAssertions> NotThrowAsync()
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
        public ExceptionAssertions(TException exception) => Which = exception;

        /// <summary>Gets Which.</summary>
        public TException Which { get; }

        /// <summary>Provides WithParameterName.</summary>
        /// <param name="expected">The expected value.</param>
        /// <returns>The result.</returns>
        public ExceptionAssertions<TException> WithParameterName(string expected)
        {
            if (Which is ArgumentException argumentException)
            {
                if (!string.Equals(argumentException.ParamName, expected, StringComparison.Ordinal))
                {
                    AssertionHelpers.Fail($"Expected parameter name {expected}, but found {argumentException.ParamName ?? "<null>"}.");
                }

                return this;
            }

            AssertionHelpers.Fail($"Expected an ArgumentException, but found {Which.GetType().FullName}.");
            throw new InvalidOperationException("The assertion failure did not throw.");
        }

        /// <summary>Provides WithInnerException.</summary>
        /// <returns>The result.</returns>
        /// <typeparam name="TInnerException">The TInnerException type.</typeparam>
        public ExceptionAssertions<TException> WithInnerException<TInnerException>()
            where TInnerException : Exception
        {
            if (Which.InnerException is not TInnerException)
            {
                AssertionHelpers.Fail($"Expected inner exception {typeof(TInnerException).FullName}, but found {Which.InnerException?.GetType().FullName ?? "<null>"}.");
            }

            return this;
        }
    }
}
