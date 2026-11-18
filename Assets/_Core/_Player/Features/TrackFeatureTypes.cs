namespace Game.Player.Features
{
    // Values are saved as numbers in prefabs and assets: only ever add new entries at the end.

    /// <summary>Body parts that enhancer / regulator gates scale.</summary>
    public enum BodyPart
    {
        Head = 0,
        Neck = 1,
        Torso = 2,
        Shoulders = 3,
        Arms = 4,
        Legs = 5
    }

    /// <summary>What a colour gate, paint pool or spray can repaints.</summary>
    public enum PaintTarget
    {
        Body = 0,
        Clothes = 1,
        Pet = 2
    }

    /// <summary>Material slots on the doll that gates and pickups can swap (eyes gate, new body material).</summary>
    public enum AppearanceSlot
    {
        Body = 0,
        Eyes = 1
    }

    /// <summary>States a track element can put the doll in.</summary>
    public enum StatusType
    {
        OnFire = 0,
        Dirty = 1,
        Shielded = 2
    }

    /// <summary>The ground she is running on. Terrain patches switch it mid-run.</summary>
    public enum TerrainType
    {
        Land = 0,
        Water = 1,
        Underwater = 2
    }
}
