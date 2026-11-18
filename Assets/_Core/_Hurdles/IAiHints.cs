using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>Something on the track the AI tries to get past (jump, slide or steer around).</summary>
    public interface IAiObstacle
    {
        /// <summary>False = the AI ignores it.</summary>
        bool AvoidByAi { get; }

        PenaltyItem.AiAvoidance AiAvoid { get; }

        GameObject gameObject { get; }
    }

    /// <summary>One option of a choice (e.g. one gate of a left/right pair) the AI can steer into.</summary>
    public interface IAiChoice
    {
        /// <summary>False = not a choice (e.g. a hazard); the AI doesn't steer to it.</summary>
        bool OffersChoice { get; }

        bool IsGoodChoice { get; }

        Transform transform { get; }
    }
}
