namespace _Core._UI
{
    // Saved as NUMBERS in the prefabs (UIScreen.screenType): never insert or reorder,
    // only add new values at the END, or every panel after the change points at the wrong screen.
    public enum ScreenType
    {
        SplashScreen,       // 0
        MainMenu,           // 1
        Gameplay,           // 2
        Settings,           // 3
        LoadingScreen,      // 4
        LevelCompletePanel, // 5
        LevelFailedPanel,   // 6
        PausePanel,         // 7

        /// <summary>Level start: "Dress her like this!" card with the target look and PLAY.</summary>
        TargetLook,         // 8
        /// <summary>Makeup close-up (paint tool) opened by a makeup station / paint gate.</summary>
        MakeupPanel,        // 9
        /// <summary>Finish stage: labels over the dolls + touch blocker.</summary>
        ComparisonPanel,    // 10

        /// <summary>No internet overlay (InternetMonitor).</summary>
        NoInternetView,     // 11
        /// <summary>Privacy policy overlay (first launch + Settings).</summary>
        PrivacyPolicy       // 12
    }
}
