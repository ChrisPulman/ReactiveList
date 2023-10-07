// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ReactiveList.Test
{
    internal class TestData(string name, int age)
    {
        public int Age { get; } = age;

        public string Name { get; } = name;
    }
}
