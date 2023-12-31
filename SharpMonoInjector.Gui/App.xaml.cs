using System;
using System.IO;
using System.Windows;

namespace SharpMonoInjector.Gui
{
    public partial class App : Application
    {
        public App() => File.Delete(Environment.CurrentDirectory + "\\DebugLog.txt");
    }
}