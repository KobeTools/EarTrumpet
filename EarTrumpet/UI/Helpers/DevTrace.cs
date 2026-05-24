using System;
using System.Diagnostics;
using System.IO;

namespace EarTrumpet.UI.Helpers
{
    public static class DevTrace
    {
        public static string LogFilePath { get; private set; }

        public static void Initialize()
        {
#if DEVBUILD
            LogFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EarTrumpet-Dev",
                "eartrumpet-dev.log");

            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));

            var listener = new TextWriterTraceListener(LogFilePath)
            {
                TraceOutputOptions = TraceOptions.DateTime,
            };

            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
            Write("DevTrace initialized");
#endif
        }

        [Conditional("DEVBUILD")]
        public static void Write(string message)
        {
#if DEVBUILD
            Trace.WriteLine($"[EarTrumpet Dev] {message}");
#endif
        }
    }
}
