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

            var coinGates = hurdle.GetComponentsInChildren<CoinGate>();
            if (coinGates.Length > 0)
            {
                var gate = ChooseCoinGate(coinGates, profile);
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
                    var dodge = FindOpenLane(penalty, ramp, trackWidth * 0.5f, bounds);
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

        // The gate that leaves the most coins (from a sample of 10), with 'GateAccuracy' chance; otherwise another one.
        private static CoinGate ChooseCoinGate(CoinGate[] gates, AiProfile profile)
        {
            var best = gates[0];
            foreach (var gate in gates)
            {
                if (gate.Apply(10) > best.Apply(10))
                    best = gate;
            }

            if (gates.Length == 1 || Random.value <= profile.GateAccuracy)
                return best;

            var others = System.Array.FindAll(gates, g => g != best);
            return others[Random.Range(0, others.Length)];
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

        // Centre of the widest stretch of track (ramp space x) that none of the penalty's boxes cover.
        // Falls back to the old fixed dodge when nothing is wide enough for the doll.
        private static float FindOpenLane(PenaltyItem penalty, Transform ramp, float halfWidth, Bounds bounds)
        {
            const float minOpening = 1f;
            var blocked = new List<Vector2>();
            foreach (var col in penalty.GetComponents<Collider>())
            {
                var b = ToRamp(col.bounds, ramp);
                blocked.Add(new Vector2(b.min.x, b.max.x));
            }

            blocked.Sort((a, b) => a.x.CompareTo(b.x));
            var bestWidth = 0f;
            var bestCentre = 0f;
            var cursor = -halfWidth;
            foreach (var range in blocked)
            {
                var from = cursor;
                var to = Mathf.Min(range.x, halfWidth);
                if (to - from > bestWidth)
                {
                    bestWidth = to - from;
                    bestCentre = (from + to) * 0.5f;
                }

                cursor = Mathf.Max(cursor, range.y);
            }

            if (halfWidth - cursor > bestWidth)
            {
                bestWidth = halfWidth - cursor;
                bestCentre = (cursor + halfWidth) * 0.5f;
            }

            if (bestWidth >= minOpening)
                return bestCentre;

            return bounds.center.x > 0f ? -DodgeLateral : DodgeLateral;
        }

        private static Bounds ToRamp(Bounds world, Transform ramp)
        {
            var min = ramp.InverseTransformPoint(world.min);
            var max = ramp.InverseTransformPoint(world.max);
            var b = new Bounds();
            b.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
            return b;
        }

        // All of the penalty's collider boxes together, in ramp space (x = sideways, y = up, z = along the ramp).
        private static Bounds GetRampBounds(PenaltyItem penalty, Transform ramp)
        {
            var colliders = penalty.GetComponents<Collider>();
            var world = colliders.Length > 0 ? colliders[0].bounds : new Bounds(penalty.transform.position, Vector3.one);
            for (var i = 1; i < colliders.Length; i++)
                world.Encapsulate(colliders[i].bounds);

            var min = ramp.InverseTransformPoint(world.min);
            var max = ramp.InverseTransformPoint(world.max);
            var bounds = new Bounds();
            bounds.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
            return bounds;
        }
    }
}
