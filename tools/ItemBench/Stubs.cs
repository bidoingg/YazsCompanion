// Stand-ins for the BepInEx bits the shared files touch (Plugin.Logger only).
using System;

namespace YazsCompanion
{
    internal static class Plugin
    {
        internal static readonly StubLog Logger = new StubLog();
    }

    internal sealed class StubLog
    {
        public void LogInfo(object m) { Console.Error.WriteLine("[info] " + m); }
        public void LogWarning(object m) { Console.Error.WriteLine("[warn] " + m); }
        public void LogError(object m) { Console.Error.WriteLine("[error] " + m); }
    }
}
