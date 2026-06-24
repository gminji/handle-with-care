using System;
using System.Collections.Generic;

namespace SlopCo.Core
{
    /// <summary>
    /// Minimal scene-scoped service registry so UI and gameplay can find shared managers
    /// (NetworkSessionManager, RoundManager, QuotaSystem) without a web of singletons.
    ///
    /// Why this exists alongside NetworkManager.Singleton: NetworkManager is owned by NGO and only
    /// covers networking. These are *our* gameplay services, registered as their NetworkObjects spawn
    /// and cleared on scene unload to avoid stale references between sessions.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            Services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var obj) && obj is T cast)
            {
                service = cast;
                return true;
            }
            service = null;
            return false;
        }

        public static T Get<T>() where T : class
            => TryGet<T>(out var s) ? s : null;

        /// <summary>Call on scene unload / shutdown to prevent cross-session stale references.</summary>
        public static void Clear() => Services.Clear();
    }
}
