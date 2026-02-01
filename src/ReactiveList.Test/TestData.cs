// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace ReactiveList.Test
{
    /// <summary>
    /// Test data class for unit tests.
    /// </summary>
    [Serializable]
    internal class TestData(string name, int age)
    {
        /// <summary>
        /// Gets the age.
        /// </summary>
        public int Age { get; } = age;

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; } = name;
    }
}
