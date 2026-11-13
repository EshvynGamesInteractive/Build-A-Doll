using _Core._UI;
using _Core.UI;
using Core.UI;

namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        public class UIEvents
        {
            public static readonly GameEvent<ScreenType> ShowByType = new();

            /// <summary>Boot loading progress, 0..1. For the splash screen's progress bar.</summary>
            public static readonly GameEvent<float> LoadingProgress = new();
        }
    }
}
