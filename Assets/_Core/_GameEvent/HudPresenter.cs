namespace Core._GameEvent
{
    /// <summary>A HUD piece that shows a request from the track (rewarded offer, make-up step, cannon button).</summary>
    public interface IHudPresenter<in T>
    {
        void Present(T request);
    }

    /// <summary>
    /// Connects track elements to the HUD piece that shows them, without either knowing the other's class.
    /// The HUD registers itself; a track element calls TryPresent and carries on by itself when nothing
    /// is registered (e.g. a test scene without the HUD).
    /// </summary>
    public static class HudPresenter<T>
    {
        private static IHudPresenter<T> _current;

        public static void Register(IHudPresenter<T> presenter) => _current = presenter;

        public static void Unregister(IHudPresenter<T> presenter)
        {
            if (ReferenceEquals(_current, presenter))
                _current = null;
        }

        /// <summary>False when no HUD is registered; the caller should then finish on its own.</summary>
        public static bool TryPresent(T request)
        {
            if (_current == null || (_current is UnityEngine.Object unityObject && unityObject == null))
                return false;

            _current.Present(request);
            return true;
        }
    }
}
