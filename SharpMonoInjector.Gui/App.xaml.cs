using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;

namespace SharpMonoInjector.Gui;

public partial class App : Application
{
    public App()
    {
        Trace.Listeners.Add(new TextWriterTraceListener(new StreamWriter(Path.Combine(Environment.CurrentDirectory, "trace.log"), false, Encoding.ASCII)));
        Timer timer = new(_ => Trace.Flush(), null, 0, 5000);

        Exit += (_, _) =>
        {
            timer.Dispose();
            Trace.Close();
        };
    }
}