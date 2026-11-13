using UnityEngine;

namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        public class AudioEvents
        {
            public static readonly GameEvent ButtonClicked = new();
            public static readonly GameEvent<AudioClip> PlaySfx = new();
            public static readonly GameEvent<AudioClip, float> PlayLoopingSfx = new();
            public static readonly GameEvent<AudioClip> PlayVoiceOver = new();
            public static readonly GameEvent<AudioClip> StopLoopingSfx = new();
            public static readonly GameEvent StopAllLoopingSfx = new();
            public static readonly GameEvent<float> SetMusicVolume = new();
            public static readonly GameEvent<float> SetSoundVolume = new();
        }
    }
}
