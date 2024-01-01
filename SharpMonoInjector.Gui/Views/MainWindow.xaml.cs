﻿using System.Windows;
using System.Windows.Input;

#if RELEASE
using System.Security.Principal;
using System;
using System.Diagnostics;
#endif

namespace SharpMonoInjector.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
#if RELEASE
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath)
            {
                Verb = "runas",
                UseShellExecute = true
            });
            Environment.Exit(0);
        }
#endif
        InitializeComponent();
    }

    #region Window Events

    void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    void Window_Exit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    void Window_Minimize(object sender, RoutedEventArgs e) => Application.Current.MainWindow.WindowState = WindowState.Minimized;
    void Window_Maximize(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow.WindowState is WindowState.Maximized) Application.Current.MainWindow.WindowState = WindowState.Normal;
        else Application.Current.MainWindow.WindowState = WindowState.Maximized;
    }

    #endregion
}