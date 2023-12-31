using System;
using System.Windows.Input;

namespace SharpMonoInjector.Gui.ViewModels;

public class RelayCommand(Action<object> execute, Func<object, bool> canExecute = null) : ICommand
{
    public event EventHandler CanExecuteChanged;

    public bool CanExecute(object parameter) => canExecute is null || canExecute(parameter);
    public void Execute(object parameter) => execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}