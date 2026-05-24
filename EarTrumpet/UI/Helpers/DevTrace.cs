using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EarTrumpet.UI.Helpers
{
    public static class DevTrace
    {
        public static string LogFilePath { get; private set; }

        public static void Initialize()
        {
#if DEVBUILD
            var installDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            LogFilePath = Path.Combine(installDir, "eartrumpet-dev.log");

            Directory.CreateDirectory(installDir);

            var listener = new TextWriterTraceListener(LogFilePath)
            {
                TraceOutputOptions = TraceOptions.DateTime,
            };

            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;

            RegisterGlobalHandlers();
            Write($"DevTrace initialized -> {LogFilePath}");
#endif
        }

#if DEVBUILD
        private static void RegisterGlobalHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                LogException("AppDomain unhandled", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
            };

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.DispatcherUnhandledException += (_, e) =>
                {
                    LogException("Dispatcher unhandled", e.Exception);
                    e.Handled = true;
                };

                TaskScheduler.UnobservedTaskException += (_, e) =>
                {
                    LogException("Task unobserved", e.Exception);
                    e.SetObserved();
                };
            }
        }
#endif

        [Conditional("DEVBUILD")]
        public static void Write(string message)
        {
#if DEVBUILD
            Trace.WriteLine($"[EarTrumpet Dev] {message}");
#endif
        }

        [Conditional("DEVBUILD")]
        public static void LogException(string context, Exception ex)
        {
#if DEVBUILD
            if (ex == null)
            {
                Write($"{context}: (null exception)");
                return;
            }

            Trace.WriteLine($"[EarTrumpet Dev] EXCEPTION ({context}): {ex}");
            Trace.Flush();
#endif
        }
    }
}
