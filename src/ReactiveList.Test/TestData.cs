// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace ReactiveList.Test
{
    /// <summary>Test data class for unit tests.</summary>
    /// <param name="name">The name value.</param>
    /// <param name="age">The age value.</param>
    [Serializable]
    internal sealed class TestData(string name, int age)
    {
        /// <summary>Gets the age.</summary>
        public int Age { get; } = age;

        /// <summary>Gets the name.</summary>
        public string Name { get; } = name;
    }
}
