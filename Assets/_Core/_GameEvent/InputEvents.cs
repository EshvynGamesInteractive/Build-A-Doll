namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        public static class InputEvents
        {
            public static readonly GameEvent PointerDown = new();
            public static readonly GameEvent PointerUp = new();
        }
    }
}
