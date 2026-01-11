// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReactiveListTestApp;

/// <summary>
/// Represents a contact with identifying information, email, department, favorite status, and home address details.
/// </summary>
/// <param name="Id">The unique identifier for the contact.</param>
/// <param name="FirstName">The first name of the contact.</param>
/// <param name="LastName">The last name of the contact.</param>
/// <param name="Email">The email address associated with the contact. Cannot be null.</param>
/// <param name="Department">The department to which the contact belongs. May be null or empty if not specified.</param>
/// <param name="IsFavorite">A value indicating whether the contact is marked as a favorite. Set to <see langword="true"/> to indicate a
/// favorite contact; otherwise, <see langword="false"/>.</param>
/// <param name="HomeAddress">The home address of the contact. Cannot be null.</param>
public record Contact(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Department,
    bool IsFavorite,
    Address HomeAddress);
