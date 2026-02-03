// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CP.Reactive.Quaternary;
using FluentAssertions;
using Xunit;

namespace ReactiveList.Test;

/// <summary>
/// Tests for dynamic search functionality with reactive views.
/// </summary>
public class DynamicSearchTests
{
    /// <summary>
    /// Search should return matching items when query matches LastName.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task SearchByLastName_WhenQueryMatchesShouldReturnMatchingItemsAsync()
    {
        // Arrange
        var searchText = new BehaviorSubject<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();

        // Setup search pipeline that rebuilds when search text changes
        searchText
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(ImmediateScheduler.Instance)
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

        // Add test contacts
        contacts.AddRange(
        [
            new TestContact("User0", "Smith0", "user0@company.com"),
            new TestContact("User1", "Smith1", "user1@company.com"),
            new TestContact("User2", "Jones", "user2@company.com"),
            new TestContact("User10", "Smith10", "user10@company.com"),
        ]);

        // Act - search for "Smith1"
        searchText.OnNext("Smith1");
        await Task.Delay(50);

        // Assert - should find Smith1 and Smith10
        searchResults.Should().HaveCount(2);
        searchResults.Select(c => c.LastName).Should().Contain("Smith1");
        searchResults.Select(c => c.LastName).Should().Contain("Smith10");
    }

    /// <summary>
    /// Search should return matching items when query matches Email.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task SearchByEmail_WhenQueryMatchesShouldReturnMatchingItemsAsync()
    {
        // Arrange
        var searchText = new BehaviorSubject<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();

        searchText
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(ImmediateScheduler.Instance)
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

        contacts.AddRange(
        [
            new TestContact("User0", "Smith0", "user0@company.com"),
            new TestContact("User1", "Smith1", "user1@company.com"),
            new TestContact("User2", "Jones", "user2@company.com"),
            new TestContact("User10", "Smith10", "user10@company.com"),
        ]);

        // Act - search for "user1"
        searchText.OnNext("user1");
        await Task.Delay(50);

        // Assert - should find user1@company.com and user10@company.com
        searchResults.Should().HaveCount(2);
        searchResults.Select(c => c.Email).Should().Contain("user1@company.com");
        searchResults.Select(c => c.Email).Should().Contain("user10@company.com");
    }

    /// <summary>
    /// Empty search query should return all items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task EmptyQuery_ShouldReturnAllItemsAsync()
    {
        // Arrange
        var searchText = new BehaviorSubject<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();

        searchText
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(ImmediateScheduler.Instance)
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

        contacts.AddRange(
        [
            new TestContact("User0", "Smith0", "user0@company.com"),
            new TestContact("User1", "Smith1", "user1@company.com"),
            new TestContact("User2", "Jones", "user2@company.com"),
        ]);

        // Act - empty search
        searchText.OnNext(string.Empty);
        await Task.Delay(50);

        // Assert - should return all
        searchResults.Should().HaveCount(3);
    }

    /// <summary>
    /// Search should be case insensitive.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task Search_ShouldBeCaseInsensitiveAsync()
    {
        // Arrange
        var searchText = new BehaviorSubject<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();

        searchText
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(ImmediateScheduler.Instance)
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

        contacts.AddRange(
        [
            new TestContact("User0", "Smith0", "user0@company.com"),
        ]);

        // Act - search with different cases
        searchText.OnNext("SMITH0");
        await Task.Delay(50);

        // Assert
        searchResults.Should().HaveCount(1);

        // Act - lowercase
        searchText.OnNext("smith0");
        await Task.Delay(50);

        // Assert
        searchResults.Should().HaveCount(1);
    }

    /// <summary>
    /// Search results should update when contacts are added after search.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task SearchResults_ShouldUpdateWhenContactsAddedAsync()
    {
        // Arrange
        var searchText = new BehaviorSubject<string>("user1");
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();

        // Subscribe to search text changes
        searchText
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(ImmediateScheduler.Instance)
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
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(ImmediateScheduler.Instance)
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
            });

        contacts.Add(new TestContact("User0", "Smith0", "user0@company.com"));
        await Task.Delay(50);

        // Assert - no matches yet
        searchResults.Should().HaveCount(0);

        // Act - add matching contact
        contacts.Add(new TestContact("User1", "Smith1", "user1@company.com"));
        await Task.Delay(50);

        // Assert - should now have match
        searchResults.Should().HaveCount(1);
        searchResults.First().Email.Should().Be("user1@company.com");
    }

    /// <summary>
    /// Non-matching query should return empty results.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task NonMatchingQuery_ShouldReturnEmptyResultsAsync()
    {
        // Arrange
        var searchText = new BehaviorSubject<string>(string.Empty);
        var contacts = new QuaternaryList<TestContact>();
        var searchResults = new ObservableCollection<TestContact>();

        searchText
            .Throttle(TimeSpan.FromMilliseconds(10))
            .ObserveOn(ImmediateScheduler.Instance)
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

        contacts.AddRange(
        [
            new TestContact("User0", "Smith0", "user0@company.com"),
            new TestContact("User1", "Smith1", "user1@company.com"),
        ]);

        // Act - search for non-existent
        searchText.OnNext("NonExistent");
        await Task.Delay(50);

        // Assert
        searchResults.Should().BeEmpty();
    }

    private static bool Matches(TestContact? c, string query)
    {
        if (c == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return c.LastName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               c.Email.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TestContact(string FirstName, string LastName, string Email);
}
#endif
