
[assembly: System.Reflection.Metadata.MetadataUpdateHandlerAttribute(typeof(DrawnUi.HotReloadService))]
namespace DrawnUi
{
    public static class HotReloadService
    {
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        public static event Action<Type[]?>? UpdateApplicationEvent;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

        /// <summary>
        /// Avoid bugged HotReload multiple calls by calling our reload just once after the spam
        /// </summary>
        private static uint DelayMs = 1000;

        private static RestartingTimer UpdateOnTimerOnly = new (DelayMs, () =>
        {
            Trace.WriteLine("[HOTRELOAD] Updating Application =>");
            UpdateApplicationEvent?.Invoke(null);
        });

        public static void ClearCache(Type[]? types)
        {
            if (Debugger.IsAttached)
            {
                Debug.WriteLine("[HOTRELOAD] ClearCache: " + (types?.Length ?? 0));
            }
        }

        public static void UpdateApplication(Type[]? types)
        {
            if (Debugger.IsAttached)
            {
                Debug.WriteLine("[HOTRELOAD] UpdateApplication: " + (types?.Length ?? 0));
                UpdateOnTimerOnly.Kick();
            }
        }

    }
}


