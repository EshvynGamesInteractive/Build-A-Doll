namespace _Core._Audio
{
    /// <summary>One-shot sounds that gameplay / UI code asks for by name (GameEvents.AudioEvents.PlayCue).
    /// GameAudio maps each to a clip. Only add new values at the end.</summary>
    public enum SoundCue
    {
        PanelOpen,
        Confetti,
        OutfitUnlocked,
        CoinReward
    }
}
