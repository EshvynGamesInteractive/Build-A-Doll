namespace Game.Level
{
    /// <summary>
    /// A track element that adjusts to the level it's spawned in (e.g. a kitchen item that is or isn't
    /// on this level's list, a wearable that is or isn't in the target look). LevelManager calls
    /// Configure right after spawning each hurdle.
    /// </summary>
    public interface ILevelConfigurable
    {
        void Configure(LevelData level);
    }
}
