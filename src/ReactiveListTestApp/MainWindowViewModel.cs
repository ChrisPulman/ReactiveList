// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using CP.Reactive;
using ReactiveUI;

namespace ReactiveListTestApp;

internal class MainWindowViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    public MainWindowViewModel()
    {
        Items.AddRange(["Lets", "Count", "To", "Fifty"]);
        var i = 0;
        Observable.Interval(TimeSpan.FromMilliseconds(500))
            .Select(_ => i.ToString())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                if (i > 50)
                {
                    Items.Clear();
                    i = 0;
                }
                else
                {
                    Items.AddRange(["Lets", "Count", "To", "Fifty"]);
                    Items.Remove("Lets");
                    Items.Remove("Count");
                    Items.RemoveAt(0);
                    Items.RemoveAt(0);
                    Items.Add(x);
                    i++;
                }
            });
    }

    public ReactiveList<string> Items { get; } = [];
}
