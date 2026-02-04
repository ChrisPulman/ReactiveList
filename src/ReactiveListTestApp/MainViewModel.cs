// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using CP.Reactive.Collections;
using CrissCross;
using ReactiveUI;

namespace ReactiveListTestApp;

/// <summary>
/// ViewModel for the MainView containing the reactive list functionality.
/// </summary>
public class MainViewModel : RxObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    public MainViewModel()
    {
        var paused = false;
        Items.AddRange(["Lets", "Count", "To", "Fifty"]);
        var i = 0;
        Observable.Interval(TimeSpan.FromMilliseconds(500))
            .Select(_ => i.ToString())
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(x =>
            {
                if (paused)
                {
                    return;
                }

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

                    var l = Items as IList<string>;
                    l.RemoveAt(0);

                    Items.Add(x);
                    var xx = Items.Last();
                    i++;
                }
            }).DisposeWith(Disposables);

        var ii = 0;
        AddItemCommand = ReactiveCommand.Create<string>(x => Items.Add($"{x}{ii++}")).DisposeWith(Disposables);
        ClearItemsCommand = ReactiveCommand.Create(Items.Clear).DisposeWith(Disposables);
        ReplaceAllCommand = ReactiveCommand.Create(() => Items.ReplaceAll(["One", "Two", "Three", "Four", "One", "Two", "Three", "Four", "One", "Two", "Three", "Four", "One", "Two", "Three", "Four", "One", "Two", "Three", "Four", "One", "Two", "Three", "Four", "One", "Two", "Three", "Four", "One", "Two", "Three", "Four"]))
            .DisposeWith(Disposables);
        PauseCommand = ReactiveCommand.Create(() => paused = !paused).DisposeWith(Disposables);
    }

    /// <summary>
    /// Gets the items collection.
    /// </summary>
    public IReactiveList<string> Items { get; } = new ReactiveList<string>();

    /// <summary>
    /// Gets the command to add an item.
    /// </summary>
    public ReactiveCommand<string, Unit> AddItemCommand { get; }

    /// <summary>
    /// Gets the command to clear items.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ClearItemsCommand { get; }

    /// <summary>
    /// Gets the command to replace all items.
    /// </summary>
    public ReactiveCommand<Unit, Unit> ReplaceAllCommand { get; }

    /// <summary>
    /// Gets the command to pause/resume.
    /// </summary>
    public ReactiveCommand<Unit, bool> PauseCommand { get; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Items.Dispose();
        }

        base.Dispose(disposing);
    }
}
