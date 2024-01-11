using System;
using System.Windows.Input;

namespace SharpMonoInjector.Gui.ViewModels;

internal class RelayCommand(Action execute, Func<bool> canExecute = null) : ICommand
{
    public event EventHandler CanExecuteChanged;

    public bool CanExecute(object parameter) => canExecute is null || canExecute();
    public void Execute(object parameter) => execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}