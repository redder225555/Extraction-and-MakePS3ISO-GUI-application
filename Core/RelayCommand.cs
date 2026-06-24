using System.Windows.Input;

namespace Ps3IsoTool.Core;

/// Standard ICommand. async handlers are fine via `new RelayCommand(async _ => await ...)`.
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// Force WPF to re-query CanExecute (e.g. when IsRunning flips programmatically).
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
