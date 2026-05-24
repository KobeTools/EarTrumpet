namespace EarTrumpet.UI.Helpers
{
    public static class Branding
    {
#if DEVBUILD
        public const string AppDisplayName = "EarTrumpet Dev";
        public const string MutexAppName = "EarTrumpet-Dev";
        public const string ExecutableFileName = "EarTrumpetDev.exe";
#else
        public const string AppDisplayName = "EarTrumpet";
        public const string MutexAppName = "EarTrumpet";
        public const string ExecutableFileName = "EarTrumpet.exe";
#endif

        public static string TrayPrefix => $"{AppDisplayName}: ";
    }
}
