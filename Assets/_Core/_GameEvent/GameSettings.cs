using UnityEngine;

namespace Core._GameEvent
{
    public static class GameSettings
    {
        public static bool IsEnabled(string key) => PlayerPrefs.GetInt(key, 1) != 0;

        public static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (IsEnabled(GameConstants.VibrateKey))
                Handheld.Vibrate();
#endif
        }
    }
}
