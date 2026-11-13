using System.Collections.Generic;
using Game.Hurdles;
using Game.Outfit;
using UnityEngine;

namespace Game.AI
{
    /// <summary>One thing the AI does once it reaches a distance along its ramp.</summary>
    public struct AiStep
    {
        public enum Kind
        {
            SteerTo,
            Jump,
            Slide
        }

        public float Distance;
        public Kind Action;

        /// <summary>For SteerTo: follow this (e.g. a gate, which may be moving). Null = use Lateral.</summary>
        public Transform Target;

        /// <summary>For SteerTo without a target: sideways position from the ramp centre.</summary>
        public float Lateral;
    }

    /// <summary>
    /// Reads the hurdles spawned on the AI's ramp once at level start and turns them into a
    /// list of steps sorted by distance: which gate to steer into, when to jump or slide.
    /// All the thinking happens here, so AiInput's Update only checks the next step.
    /// </summary>
    public static class AiPlanner
    {
        // Start a jump/slide this far before the obstacle; steer around from this far.
        private const float JumpLead = 2f;
        private const float SlideLead = 1.5f;
        private const float SteerAroundLead = 8f;
        private const float DodgeLateral = 1.4f;
        // Higher than this (ramp space) = a bar to slide under.
        private const float SlideBarMinHeight = 0.9f;
        // Wider than this share of the track = can't steer around, jump it.
        private const float TrackWideShare = 0.8f;

        public static List<AiStep> Plan(IReadOnlyList<GameObject> hurdles, Transform ramp, TargetLook look, AiProfile profile, float trackWidth)
        {
            var steps = new List<AiStep>();
            foreach (var hurdle in hurdles)
            {
                if (hurdle != null)
                    PlanHurdle(hurdle, ramp, look, profile, trackWidth, steps);
            }

            steps.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return steps;
        }

        private static void PlanHurdle(GameObject hurdle, Transform ramp, TargetLook look, AiProfile profile, float trackWidth, List<AiStep> steps)
        {
            var gates = hurdle.GetComponentsInChildren<ItemGate>();
            if (gates.Length > 0)
            {
                var gate = ChooseGate(gates, look, profile);
                var distance = ramp.InverseTransformPoint(gate.transform.position).z;
                steps.Add(new AiStep { Distance = distance - profile.GateLookAhead, Action = AiStep.Kind.SteerTo, Target = gate.transform });
                return;
            }

            var penalty = hurdle.GetComponentInChildren<PenaltyItem>();
            if (penalty == null || Random.value > profile.ObstacleSkill)
                return; // coins, or the AI "doesn't see" this one

            var bounds = GetRampBounds(penalty, ramp);
            switch (Classify(penalty, bounds, trackWidth))
            {
                case PenaltyItem.AiAvoidance.Jump:
                    steps.Add(new AiStep { Distance = bounds.min.z - JumpLead, Action = AiStep.Kind.Jump });
                    break;
                case PenaltyItem.AiAvoidance.Slide:
                    steps.Add(new AiStep { Distance = bounds.min.z - SlideLead, Action = AiStep.Kind.Slide });
                    break;
                default:
                    var dodge = bounds.center.x > 0f ? -DodgeLateral : DodgeLateral;
                    steps.Add(new AiStep { Distance = bounds.min.z - SteerAroundLead, Action = AiStep.Kind.SteerTo, Lateral = dodge });
                    break;
            }
        }

        // Picks the gate with a target item with 'GateAccuracy' chance, otherwise a different one.
        private static ItemGate ChooseGate(ItemGate[] gates, TargetLook look, AiProfile profile)
        {
            ItemGate right = null;
            var wrong = new List<ItemGate>();

            foreach (var gate in gates)
            {
                if (right == null && IsInLook(gate.Item, look))
                    right = gate;
                else
                    wrong.Add(gate);
            }

            if (right != null && (wrong.Count == 0 || Random.value <= profile.GateAccuracy))
                return right;

            return wrong.Count > 0 ? wrong[Random.Range(0, wrong.Count)] : gates[0];
        }

        private static bool IsInLook(OutfitItem item, TargetLook look)
        {
            if (item == null || look == null || !look.HasItems)
                return false;

            foreach (var wanted in look.Items)
            {
                if (wanted == item)
                    return true;
            }

            return false;
        }

        private static PenaltyItem.AiAvoidance Classify(PenaltyItem penalty, Bounds bounds, float trackWidth)
        {
            if (penalty.AiAvoid != PenaltyItem.AiAvoidance.Auto)
                return penalty.AiAvoid;

            if (bounds.min.y > SlideBarMinHeight)
                return PenaltyItem.AiAvoidance.Slide;

            // Moving things are hard to steer around; jump them.
            if (bounds.size.x >= trackWidth * TrackWideShare || penalty.GetComponentInParent<HurdleMover>() != null)
                return PenaltyItem.AiAvoidance.Jump;

            return PenaltyItem.AiAvoidance.SteerAround;
        }

        // The penalty's collider box in ramp space (x = sideways, y = up, z = along the ramp).
        private static Bounds GetRampBounds(PenaltyItem penalty, Transform ramp)
        {
            var world = penalty.TryGetComponent<Collider>(out var col)
                ? col.bounds
                : new Bounds(penalty.transform.position, Vector3.one);

            var min = ramp.InverseTransformPoint(world.min);
            var max = ramp.InverseTransformPoint(world.max);
            var bounds = new Bounds();
            bounds.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
            return bounds;
        }
    }
}
