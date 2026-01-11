// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReactiveListTestApp;

/// <summary>
/// Represents a postal address, including street, city, ZIP code, and country information.
/// </summary>
/// <param name="Street">The street address component, such as house number and street name. Cannot be null.</param>
/// <param name="City">The city or locality of the address. Cannot be null.</param>
/// <param name="ZipCode">The postal or ZIP code associated with the address. Cannot be null.</param>
/// <param name="Country">The country in which the address is located. Cannot be null.</param>
public record Address(string Street, string City, string ZipCode, string Country);
