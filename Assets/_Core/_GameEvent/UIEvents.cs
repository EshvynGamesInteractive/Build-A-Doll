using _Core._UI;

namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        public class UIEvents
        {
            public static readonly GameEvent<ScreenType> ShowByType = new();

            public static readonly GameEvent CloseSettings = new();

            /// <summary>Boot loading progress, 0..1. For the splash screen's progress bar.</summary>
            public static readonly GameEvent<float> LoadingProgress = new();

            /// <summary>Closes the overlay on top (No Internet / Privacy Policy) and shows the screen it was opened over.</summary>
            public static readonly GameEvent CloseOverlay = new();

            /// <summary>Privacy policy accepted (saved in PlayerPrefs before this is raised).</summary>
            public static readonly GameEvent PrivacyAccepted = new();

            /// <summary>No Internet panel OK pressed: check the connection again now.</summary>
            public static readonly GameEvent RetryInternet = new();
        }
    }
}
