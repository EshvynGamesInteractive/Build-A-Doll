namespace _Core._UI
{
    // Saved as numbers in the prefabs: only add new values at the end.
    public enum ScreenType
    {
        SplashScreen,
        MainMenu,
        Gameplay,
        Settings,
        LoadingScreen,
        LevelCompletePanel,
        LevelFailedPanel,
        PausePanel,
        NoInternetView,
        PrivacyPolicy,

        /// <summary>Level start: "Dress her like this!" card with the target look and PLAY.</summary>
        TargetLook,
        /// <summary>Makeup close-up (paint tool) opened by a makeup station / paint gate.</summary>
        MakeupPanel,
        /// <summary>Finish stage: labels over the dolls + touch blocker.</summary>
        ComparisonPanel
    }
}
