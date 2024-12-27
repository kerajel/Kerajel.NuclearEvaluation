namespace NuclearEvaluation.Library.Helpers;

public class Debouncer<TResult>
{
    readonly TimeSpan _debounceInterval;
    readonly object _syncRoot = new();
    Timer? _debounceTimer;
    CancellationTokenSource? _cancellationTokenSource;
    List<TaskCompletionSource<TResult>> _pendingTasks = new();
    Func<Task<TResult>>? _executeAction;

    public Debouncer(TimeSpan interval)
    {
        _debounceInterval = interval;
    }

    public Task<TResult> ExecuteAsync(Func<Task<TResult>> action)
    {
        TaskCompletionSource<TResult> taskCompletionSource = new();
        lock (_syncRoot)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _executeAction = action;
            _pendingTasks.Add(taskCompletionSource);

            if (_debounceTimer == null)
            {
                _debounceTimer = new Timer(OnTimerElapsed, null, _debounceInterval, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _debounceTimer.Change(_debounceInterval, Timeout.InfiniteTimeSpan);
            }
        }
        return taskCompletionSource.Task;
    }

    public void Cancel()
    {
        List<TaskCompletionSource<TResult>> pendingTasksCopy;
        lock (_syncRoot)
        {
            _cancellationTokenSource?.Cancel();

            pendingTasksCopy = new List<TaskCompletionSource<TResult>>(_pendingTasks);
            _pendingTasks.Clear();

            _executeAction = null;

            if (_debounceTimer != null)
            {
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }
        }

        foreach (TaskCompletionSource<TResult> taskCompletionSource in pendingTasksCopy)
        {
            taskCompletionSource.TrySetCanceled();
        }
    }

    async void OnTimerElapsed(object? state)
    {
        List<TaskCompletionSource<TResult>> tasksToComplete;
        Func<Task<TResult>>? actionToExecute;
        CancellationToken cancellationToken;

        lock (_syncRoot)
        {
            tasksToComplete = new List<TaskCompletionSource<TResult>>(_pendingTasks);
            _pendingTasks.Clear();
            actionToExecute = _executeAction;
            _executeAction = null;
            cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
        }

        try
        {
            if (actionToExecute != null)
            {
                TResult result = await actionToExecute();
                foreach (TaskCompletionSource<TResult> taskCompletionSource in tasksToComplete)
                {
                    taskCompletionSource.TrySetResult(result);
                }
            }
        }
        catch (OperationCanceledException)
        {
            foreach (TaskCompletionSource<TResult> taskCompletionSource in tasksToComplete)
            {
                taskCompletionSource.TrySetCanceled();
            }
        }
        catch (Exception ex)
        {
            foreach (TaskCompletionSource<TResult> taskCompletionSource in tasksToComplete)
            {
                taskCompletionSource.TrySetException(ex);
            }
        }
    }
}