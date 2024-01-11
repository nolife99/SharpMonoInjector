using System.Windows;
using System.Windows.Input;

#if RELEASE
using System.Security.Principal;
using System;
using System.Diagnostics;
#endif

namespace SharpMonoInjector.Gui.Views;

partial class MainWindow : Window
{
    MainWindow()
    {
#if RELEASE
        using (var id = WindowsIdentity.GetCurrent()) if (!new WindowsPrincipal(id).IsInRole((int)WindowsBuiltInRole.Administrator) && MessageBox.Show(
            "It is recommended that you run this tool as Administrator in order to improve injection.\nRun as Administrator?", "SharpMonoInjector", 
            MessageBoxButton.YesNo) is MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath)
            {
                Verb = "runas",
                UseShellExecute = true
            });
            return;
        }
#endif
        InitializeComponent();
    }

    #region Window Events

    void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    void Window_Exit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    void Window_Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    #endregion
}