// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace ReactiveList.Test;

/// <summary>Test data class for unit tests.</summary>
/// <param name="name">The name value.</param>
/// <param name="age">The age value.</param>
[Serializable]
internal sealed class TestData(string name, int age)
{
    /// <summary>Gets the shared alice name test value.</summary>
    internal const string AliceName = "Alice";

    /// <summary>Gets the shared apple text test value.</summary>
    internal const string AppleText = "apple";

    /// <summary>Gets the shared apricot text test value.</summary>
    internal const string ApricotText = "apricot";

    /// <summary>Gets the shared array parameter name test value.</summary>
    internal const string ArrayParameterName = "array";

    /// <summary>Gets the shared banana text test value.</summary>
    internal const string BananaText = "banana";

    /// <summary>Gets the shared category property name test value.</summary>
    internal const string CategoryPropertyName = "Category";

    /// <summary>Gets the shared celine name test value.</summary>
    internal const string CelineName = "Celine";

    /// <summary>Gets the shared charlie name test value.</summary>
    internal const string CharlieName = "Charlie";

    /// <summary>Gets the shared cherry text test value.</summary>
    internal const string CherryText = "cherry";

    /// <summary>Gets the shared clarence name test value.</summary>
    internal const string ClarenceName = "Clarence";

    /// <summary>Gets the shared clifford name test value.</summary>
    internal const string CliffordName = "Clifford";

    /// <summary>Gets the shared count property name test value.</summary>
    internal const string CountPropertyName = "Count";

    /// <summary>Gets the shared engineering department test value.</summary>
    internal const string EngineeringDepartment = "Engineering";

    /// <summary>Gets the shared index parameter name test value.</summary>
    internal const string IndexParameterName = "index";

    /// <summary>Gets the shared indexer property name test value.</summary>
    internal const string IndexerPropertyName = "Item[]";

    /// <summary>Gets the shared inner index parameter name test value.</summary>
    internal const string InnerIndexParameterName = "innerIndex";

    /// <summary>Gets the shared items parameter name test value.</summary>
    internal const string ItemsParameterName = "items";

    /// <summary>Gets the shared missing key test value.</summary>
    internal const string MissingKey = "missing";

    /// <summary>Gets the shared north region test value.</summary>
    internal const string NorthRegion = "north";

    /// <summary>Gets the shared original text test value.</summary>
    internal const string OriginalText = "original";

    /// <summary>Gets the shared outer index parameter name test value.</summary>
    internal const string OuterIndexParameterName = "outerIndex";

    /// <summary>Gets the shared property setter field name test value.</summary>
    internal const string PropertySetterFieldName = "propertySetter";

    /// <summary>Gets the shared region property name test value.</summary>
    internal const string RegionPropertyName = "region";

    /// <summary>Gets the shared sales department test value.</summary>
    internal const string SalesDepartment = "Sales";

    /// <summary>Gets the shared source changed method name test value.</summary>
    internal const string SourceChangedMethodName = "OnSourceChanged";

    /// <summary>Gets the shared south region test value.</summary>
    internal const string SouthRegion = "south";

    /// <summary>Gets the shared three text test value.</summary>
    internal const string ThreeText = "three";

    /// <summary>Gets the shared test value negative five test value.</summary>
    internal const int TestValueNegativeFive = -5;

    /// <summary>Gets the shared test value two test value.</summary>
    internal const int TestValueTwo = 2;

    /// <summary>Gets the shared test value three test value.</summary>
    internal const int TestValueThree = 3;

    /// <summary>Gets the shared test value four test value.</summary>
    internal const int TestValueFour = 4;

    /// <summary>Gets the shared test value five test value.</summary>
    internal const int TestValueFive = 5;

    /// <summary>Gets the shared test value six test value.</summary>
    internal const int TestValueSix = 6;

    /// <summary>Gets the shared test value seven test value.</summary>
    internal const int TestValueSeven = 7;

    /// <summary>Gets the shared test value eight test value.</summary>
    internal const int TestValueEight = 8;

    /// <summary>Gets the shared test value nine test value.</summary>
    internal const int TestValueNine = 9;

    /// <summary>Gets the shared test value ten test value.</summary>
    internal const int TestValueTen = 10;

    /// <summary>Gets the shared test value fifteen test value.</summary>
    internal const int TestValueFifteen = 15;

    /// <summary>Gets the shared test value seventeen test value.</summary>
    internal const int TestValueSeventeen = 17;

    /// <summary>Gets the shared test value twenty test value.</summary>
    internal const int TestValueTwenty = 20;

    /// <summary>Gets the shared test value twenty five test value.</summary>
    internal const int TestValueTwentyFive = 25;

    /// <summary>Gets the shared test value thirty test value.</summary>
    internal const int TestValueThirty = 30;

    /// <summary>Gets the shared test value thirty five test value.</summary>
    internal const int TestValueThirtyFive = 35;

    /// <summary>Gets the shared test value thirty six test value.</summary>
    internal const int TestValueThirtySix = 36;

    /// <summary>Gets the shared test value thirty eight test value.</summary>
    internal const int TestValueThirtyEight = 38;

    /// <summary>Gets the shared test value forty test value.</summary>
    internal const int TestValueForty = 40;

    /// <summary>Gets the shared test value forty two test value.</summary>
    internal const int TestValueFortyTwo = 42;

    /// <summary>Gets the shared test value forty three test value.</summary>
    internal const int TestValueFortyThree = 43;

    /// <summary>Gets the shared test value forty four test value.</summary>
    internal const int TestValueFortyFour = 44;

    /// <summary>Gets the shared test value fifty test value.</summary>
    internal const int TestValueFifty = 50;

    /// <summary>Gets the shared test value ninety nine test value.</summary>
    internal const int TestValueNinetyNine = 99;

    /// <summary>Gets the shared test value one hundred test value.</summary>
    internal const int TestValueOneHundred = 100;

    /// <summary>Gets the shared test value four hundred four test value.</summary>
    internal const int TestValueFourHundredFour = 404;

    /// <summary>Gets the shared test value invalid enum value test value.</summary>
    internal const int TestValueInvalidEnumValue = 999;

    /// <summary>Gets the shared test value one thousand test value.</summary>
    internal const int TestValueOneThousand = 1000;

    /// <summary>Gets the age.</summary>
    public int Age { get; } = age;

    /// <summary>Gets the name.</summary>
    public string Name { get; } = name;
}
