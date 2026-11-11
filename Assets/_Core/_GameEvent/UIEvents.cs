using Core.UI;

namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        public class UIEvents
        {
            public static readonly GameEvent<ScreenType> ShowByType = new();
        }
    }
}
