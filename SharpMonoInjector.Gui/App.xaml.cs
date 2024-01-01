using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace SharpMonoInjector.Gui;

public partial class App : Application
{
    public App()
    {
        var stream = File.CreateText(Environment.CurrentDirectory + "\\DebugLog.txt");
        TextWriterTraceListener listener = new(stream);

        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;

        Exit += (_, _) =>
        {
            listener.Dispose();
            stream.Dispose();
        };
    }
}