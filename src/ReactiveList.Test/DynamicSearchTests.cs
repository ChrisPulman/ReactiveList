// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER || NETFRAMEWORK
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CP.Primitives.Collections;
using FluentAssertions;
using TUnit.Core;

namespace ReactiveList.Test;

/// <summary>Tests for dynamic search functionality with reactive views.</summary>
public class DynamicSearchTests
{
    /// <summary>The search throttle milliseconds.</summary>
    private const int SearchThrottleMilliseconds = 10;

    /// <summary>The expected filtered match count.</summary>
    private const int ExpectedFilteredMatchCount = 2;

    /// <summary>The expected complete result count.</summary>
    private const int ExpectedCompleteResultCount = 3;

    /// <summary>The primary first name.</summary>
    private const string PrimaryFirstName = "User0";

    /// <summary>The primary last name.</summary>
    private const string PrimaryLastName = "Smith0";

    /// <summary>The primary email.</summary>
    private const string PrimaryEmail = "user0@company.com";

    /// <summary>The secondary first name.</summary>
    private const string SecondaryFirstName = "User1";

    /// <summary>The secondary last name.</summary>
    private const string SecondaryLastName = "Smith1";

    /// <summary>The secondary email.</summary>
    private const string SecondaryEmail = "user1@company.com";

    /// <summary>A query that cannot match the deterministic contact data.</summary>
    private const string NonMatchingQuery = "NonExistent";

    /// <summary>The tertiary first name.</summary>
    private const string TertiaryFirstName = "User2";

    /// <summary>The tertiary last name.</summary>
    private const string TertiaryLastName = "Jones";

    /// <summary>The tertiary email.</summary>
    private const string TertiaryEmail = "user2@company.com";

    /// <summary>The extended last name.</summary>
    private const string ExtendedLastName = "Smith10";

    /// <summary>The extended email.</summary>
    private const string ExtendedEmail = "user10@company.com";

    /// <summary>Search should return matching items when query matches LastName.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SearchByLastName_WhenQueryMatchesShouldReturnMatchingItemsAsync()
    {
        // Arrange
        var searchText = new BehaviorSignal<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();
        var processedQuery = CreateCompletion<string>();

        // Setup search pipeline that rebuilds when search text changes
        searchText
            .Throttle(TimeSpan.FromMilliseconds(SearchThrottleMilliseconds))
            .ObserveOn(Sequencer.Immediate)
            .Subscribe(query =>
            {
                searchResults.Clear();
                foreach (var contact in contacts)
                {
                    if (Matches(contact, query))
                    {
                        searchResults.Add(contact);
                    }
                }

                processedQuery.TrySetResult(query);
            });

        // Add test contacts
        contacts.AddRange(
        [
            new TestContact(PrimaryFirstName, PrimaryLastName, PrimaryEmail),
            new TestContact(SecondaryFirstName, SecondaryLastName, SecondaryEmail),
            new TestContact(TertiaryFirstName, TertiaryLastName, TertiaryEmail),
            new TestContact("User10", ExtendedLastName, ExtendedEmail),
        ]);

        // Act - search for SecondaryLastName
        searchText.OnNext(SecondaryLastName);
        await processedQuery.Task;

        // Assert - should find Smith1 and Smith10
        searchResults.Should().HaveCount(ExpectedFilteredMatchCount);
        searchResults.Select(c => c.LastName).Should().Contain(SecondaryLastName);
        searchResults.Select(c => c.LastName).Should().Contain(ExtendedLastName);
    }

    /// <summary>Search should return matching items when query matches Email.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SearchByEmail_WhenQueryMatchesShouldReturnMatchingItemsAsync()
    {
        // Arrange
        var searchText = new BehaviorSignal<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();
        var processedQuery = CreateCompletion<string>();

        searchText
            .Throttle(TimeSpan.FromMilliseconds(SearchThrottleMilliseconds))
            .ObserveOn(Sequencer.Immediate)
            .Subscribe(query =>
            {
                searchResults.Clear();
                foreach (var contact in contacts)
                {
                    if (Matches(contact, query))
                    {
                        searchResults.Add(contact);
                    }
                }

                processedQuery.TrySetResult(query);
            });

        contacts.AddRange(
        [
            new TestContact(PrimaryFirstName, PrimaryLastName, PrimaryEmail),
            new TestContact(SecondaryFirstName, SecondaryLastName, SecondaryEmail),
            new TestContact(TertiaryFirstName, TertiaryLastName, TertiaryEmail),
            new TestContact("User10", ExtendedLastName, ExtendedEmail),
        ]);

        // Act - search for "user1"
        searchText.OnNext("user1");
        await processedQuery.Task;

        // Assert - should find user1@company.com and user10@company.com
        searchResults.Should().HaveCount(ExpectedFilteredMatchCount);
        searchResults.Select(c => c.Email).Should().Contain(SecondaryEmail);
        searchResults.Select(c => c.Email).Should().Contain(ExtendedEmail);
    }

    /// <summary>Empty search query should return all items.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EmptyQuery_ShouldReturnAllItemsAsync()
    {
        // Arrange
        var searchText = new BehaviorSignal<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();
        var processedQuery = CreateCompletion<string>();

        searchText
            .Throttle(TimeSpan.FromMilliseconds(SearchThrottleMilliseconds))
            .ObserveOn(Sequencer.Immediate)
            .Subscribe(query =>
            {
                searchResults.Clear();
                foreach (var contact in contacts)
                {
                    if (Matches(contact, query))
                    {
                        searchResults.Add(contact);
                    }
                }

                processedQuery.TrySetResult(query);
            });

        contacts.AddRange(
        [
            new TestContact(PrimaryFirstName, PrimaryLastName, PrimaryEmail),
            new TestContact(SecondaryFirstName, SecondaryLastName, SecondaryEmail),
            new TestContact(TertiaryFirstName, TertiaryLastName, TertiaryEmail),
        ]);

        // Act - empty search
        searchText.OnNext(string.Empty);
        await processedQuery.Task;

        // Assert - should return all
        searchResults.Should().HaveCount(ExpectedCompleteResultCount);
    }

    /// <summary>Search should be case insensitive.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Search_ShouldBeCaseInsensitiveAsync()
    {
        // Arrange
        var searchText = new BehaviorSignal<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();
        var processedQuery = CreateCompletion<string>();

        searchText
            .Throttle(TimeSpan.FromMilliseconds(SearchThrottleMilliseconds))
            .ObserveOn(Sequencer.Immediate)
            .Subscribe(query =>
            {
                searchResults.Clear();
                foreach (var contact in contacts)
                {
                    if (Matches(contact, query))
                    {
                        searchResults.Add(contact);
                    }
                }

                processedQuery.TrySetResult(query);
            });

        contacts.AddRange(
        [
            new TestContact(PrimaryFirstName, PrimaryLastName, PrimaryEmail),
        ]);

        // Act - search with different cases
        searchText.OnNext("SMITH0");
        await processedQuery.Task;

        // Assert
        searchResults.Should().HaveCount(1);

        // Act - lowercase
        processedQuery = CreateCompletion<string>();
        searchText.OnNext("smith0");
        await processedQuery.Task;

        // Assert
        searchResults.Should().HaveCount(1);
    }

    /// <summary>Search results should update when contacts are added after search.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SearchResults_ShouldUpdateWhenContactsAddedAsync()
    {
        // Arrange
        var searchText = new BehaviorSignal<string>("user1");
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();
        var processedContactChange = CreateCompletion<bool>();

        // Subscribe to search text changes
        searchText
            .Throttle(TimeSpan.FromMilliseconds(SearchThrottleMilliseconds))
            .ObserveOn(Sequencer.Immediate)
            .Subscribe(query =>
            {
                searchResults.Clear();
                foreach (var contact in contacts)
                {
                    if (Matches(contact, query))
                    {
                        searchResults.Add(contact);
                    }
                }
            });

        // Subscribe to contact changes
        contacts.Stream
            .Throttle(TimeSpan.FromMilliseconds(SearchThrottleMilliseconds))
            .ObserveOn(Sequencer.Immediate)
            .Subscribe(_ =>
            {
                var query = searchText.Value;
                searchResults.Clear();
                foreach (var contact in contacts)
                {
                    if (Matches(contact, query))
                    {
                        searchResults.Add(contact);
                    }
                }

                processedContactChange.TrySetResult(true);
            });

        contacts.Add(new TestContact(PrimaryFirstName, PrimaryLastName, PrimaryEmail));
        await processedContactChange.Task;

        // Assert - no matches yet
        searchResults.Should().HaveCount(0);

        // Act - add matching contact
        processedContactChange = CreateCompletion<bool>();
        contacts.Add(new TestContact(SecondaryFirstName, SecondaryLastName, SecondaryEmail));
        await processedContactChange.Task;

        // Assert - should now have match
        searchResults.Should().HaveCount(1);
        searchResults.First().Email.Should().Be(SecondaryEmail);
    }

    /// <summary>Non-matching query should return empty results.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task NonMatchingQuery_ShouldReturnEmptyResultsAsync()
    {
        // Arrange
        var searchText = new BehaviorSignal<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();
        var processedQuery = CreateCompletion<string>();

        searchText
            .Throttle(TimeSpan.FromMilliseconds(SearchThrottleMilliseconds))
            .ObserveOn(Sequencer.Immediate)
            .Subscribe(query =>
            {
                searchResults.Clear();
                foreach (var contact in contacts)
                {
                    if (Matches(contact, query))
                    {
                        searchResults.Add(contact);
                    }
                }

                if (!string.Equals(query, NonMatchingQuery, StringComparison.Ordinal))
                {
                    return;
                }

                processedQuery.TrySetResult(query);
            });

        contacts.AddRange(
        [
            new TestContact(PrimaryFirstName, PrimaryLastName, PrimaryEmail),
            new TestContact(SecondaryFirstName, SecondaryLastName, SecondaryEmail),
        ]);

        // Act - search for non-existent
        searchText.OnNext(NonMatchingQuery);
        await processedQuery.Task;

        // Assert
        await TUnit.Assertions.Assert.That(searchResults.Count).IsEqualTo(0);
    }

    /// <summary>Provides Matches.</summary>
    /// <param name="c">The c value.</param>
    /// <param name="query">The query value.</param>
    /// <returns>The result.</returns>
    private static bool Matches(TestContact? c, string query)
    {
        if (c is null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(query) ? true : c.LastName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               c.Email.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Creates an asynchronously completing pipeline signal.</summary>
    /// <typeparam name="T">The signal value type.</typeparam>
    /// <returns>A new completion source.</returns>
    private static TaskCompletionSource<T> CreateCompletion<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Provides TestContact.</summary>
    /// <param name="FirstName">The FirstName value.</param>
    /// <param name="LastName">The LastName value.</param>
    /// <param name="Email">The Email value.</param>
    private sealed record TestContact(string FirstName, string LastName, string Email);
}
#endif
