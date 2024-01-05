using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace SharpMonoInjector.Gui;

public partial class App : Application
{
    public App()
    {
        StreamWriter output = new(Path.Combine(Environment.CurrentDirectory, "trace.log"), false, Encoding.ASCII);
        TextWriterTraceListener listener = new(output);

        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;

        Exit += (_, _) =>
        {
            listener.Dispose();
            output.Dispose();
        };
    }
}