using System.Runtime.ExceptionServices;

namespace ReactiveList.Benchmarks;

internal static class BenchmarkObservableExtensions
{
    public static IDisposable SubscribeObserver<T>(this IObservable<T> source, Action<T> onNext)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNext);

        return source.Subscribe(new ActionObserver<T>(onNext));
    }

    private sealed class ActionObserver<T>(Action<T> onNext) : IObserver<T>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error) => ExceptionDispatchInfo.Capture(error).Throw();

        public void OnNext(T value) => onNext(value);
    }
}
