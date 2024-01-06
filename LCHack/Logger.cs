// Decompiled with JetBrains decompiler
// Type: LCHack.Logger
// Assembly: LCHack, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C7AC11A0-4EC3-496F-8ED7-8B521360C155
// Assembly location: D:\KeyAsio.Net-v3.0.0-win64\LCHack.dll

using System;
using System.IO;

namespace LCHack;

internal class Logger
{
    internal static void Log(string message) => File.AppendAllText("hackErr.log", message + Environment.NewLine);
}