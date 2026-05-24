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
        private static readonly object Gate = new object();
        private static bool _initialized;

        public static string LogFilePath { get; private set; }

        public static void Initialize()
        {
#if DEVBUILD
            lock (Gate)
            {
                if (_initialized)
                {
                    return;
                }

                var installDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                LogFilePath = Path.Combine(installDir, "eartrumpet-dev.log");
                Directory.CreateDirectory(installDir);

                var version = Assembly.GetExecutingAssembly().GetName().Version;
                WriteCore($"--- session {DateTime.Now:yyyy-MM-dd HH:mm:ss} v{version} ---");
                WriteCore($"DevTrace initialized -> {LogFilePath}");

                _initialized = true;
            }

            RegisterGlobalHandlers();
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

        private static void WriteCore(string message)
        {
            if (string.IsNullOrEmpty(LogFilePath))
            {
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [EarTrumpet Dev] {message}{Environment.NewLine}";
            File.AppendAllText(LogFilePath, line);
            Debug.Write(line);
        }
#endif

        public static void Write(string message)
        {
#if DEVBUILD
            lock (Gate)
            {
                if (!_initialized)
                {
                    Initialize();
                }

                WriteCore(message);
            }
#endif
        }

        public static void LogException(string context, Exception ex)
        {
#if DEVBUILD
            lock (Gate)
            {
                if (!_initialized)
                {
                    Initialize();
                }

                if (ex == null)
                {
                    WriteCore($"{context}: (null exception)");
                }
                else
                {
                    WriteCore($"EXCEPTION ({context}): {ex}");
                }
            }
#endif
        }
    }
}
