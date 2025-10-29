using System.Windows.Input;

namespace EtherNetIPTool.ViewModels;

/// <summary>
/// A command whose sole purpose is to relay its functionality to other objects by invoking delegates
/// Implements ICommand for use in MVVM pattern
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// Initializes a new instance that can always execute
    /// </summary>
    /// <param name="execute">The execution logic</param>
    public RelayCommand(Action<object?> execute)
        : this(execute, null)
    {
    }

    /// <summary>
    /// Initializes a new instance
    /// </summary>
    /// <param name="execute">The execution logic</param>
    /// <param name="canExecute">The execution status logic</param>
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <summary>
    /// Determines whether the command can execute in its current state
    /// </summary>
    /// <param name="parameter">Data used by the command</param>
    /// <returns>True if this command can be executed; otherwise, false</returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute(parameter);
    }

    /// <summary>
    /// Executes the command
    /// </summary>
    /// <param name="parameter">Data used by the command</param>
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <summary>
    /// Raises the CanExecuteChanged event manually
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>
/// Generic version of RelayCommand with strongly-typed parameter
/// </summary>
/// <typeparam name="T">Type of the command parameter</typeparam>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;

    /// <summary>
    /// Initializes a new instance that can always execute
    /// </summary>
    /// <param name="execute">The execution logic</param>
    public RelayCommand(Action<T?> execute)
        : this(execute, null)
    {
    }

    /// <summary>
    /// Initializes a new instance
    /// </summary>
    /// <param name="execute">The execution logic</param>
    /// <param name="canExecute">The execution status logic</param>
    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <summary>
    /// Determines whether the command can execute in its current state
    /// </summary>
    /// <param name="parameter">Data used by the command</param>
    /// <returns>True if this command can be executed; otherwise, false</returns>
    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null)
            return true;

        if (parameter is T typedParameter)
            return _canExecute(typedParameter);

        if (parameter == null && typeof(T).IsValueType)
            return _canExecute(default);

        return _canExecute((T?)parameter);
    }

    /// <summary>
    /// Executes the command
    /// </summary>
    /// <param name="parameter">Data used by the command</param>
    public void Execute(object? parameter)
    {
        if (parameter is T typedParameter)
        {
            _execute(typedParameter);
        }
        else if (parameter == null)
        {
            _execute(default);
        }
    }

    /// <summary>
    /// Raises the CanExecuteChanged event manually
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>
/// Async version of RelayCommand for async operations
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// Initializes a new instance that can always execute
    /// </summary>
    /// <param name="execute">The async execution logic</param>
    public AsyncRelayCommand(Func<object?, Task> execute)
        : this(execute, null)
    {
    }

    /// <summary>
    /// Initializes a new instance
    /// </summary>
    /// <param name="execute">The async execution logic</param>
    /// <param name="canExecute">The execution status logic</param>
    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when changes occur that affect whether the command should execute
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <summary>
    /// Determines whether the command can execute in its current state
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute == null || _canExecute(parameter));
    }

    /// <summary>
    /// Executes the command asynchronously
    /// </summary>
    public async void Execute(object? parameter)
    {
        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Raises the CanExecuteChanged event manually
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
