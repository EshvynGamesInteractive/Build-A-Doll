#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Game.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Build A Doll > Animation > Build Doll Animator.
    /// Rebuilds DollAnimator.controller in place (same GUID, so Doll_Base, Player and DemoDoll stay
    /// linked) from the Mixamo clips in the doll's _Animation folder, matching the parameters that
    /// RunnerAnimator sends:
    ///   Idle (Dwarf Idle) -> Locomotion (Walking..Running by Speed) while IsRunning
    ///   Any -> Jump on Jump, back when IsGrounded
    ///   Any -> Victory on Win, then Dance;  Any -> Defeat on Lose;  Any -> Loser on LoseAlt
    ///   Any -> Dance on Dance (the target-look demo doll)
    /// Also turns on looping for idle, walk, run and dance, and puts a DollAnimatorTrigger on the
    /// DemoDoll prefab so it dances. Safe to run again.
    /// </summary>
    public static class DollAnimatorBuilder
    {
        private const string ControllerName = "DollAnimator";
        private const string DollFolder = "Assets/_ART/_3D/_Doll";

        // Clip names (as in the Mixamo files). Change here if you swap animations.
        private const string IdleClip = "Dwarf Idle";
        private const string FallbackIdleClip = "Breathing Idle";
        private const string WalkClip = "Walking";
        private const string RunClip = "Running";
        private const string JumpClip = "Jump";
        private const string WinClip = "Victory";
        private const string LoseClip = "Defeat";
        private const string AiLoseClip = "Loser";
        private const string DanceClip = "Snake Hip Hop Dance";

        [MenuItem("Build A Doll/Animation/Build Doll Animator")]
        public static void Build()
        {
            var controller = FindController();
            if (controller == null)
            {
                var path = DollFolder + "/" + ControllerName + ".controller";
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            var idle = FindClip(IdleClip) ?? FindClip(FallbackIdleClip);
            var walk = FindClip(WalkClip);
            var run = FindClip(RunClip);
            var jump = FindClip(JumpClip);
            var win = FindClip(WinClip);
            var lose = FindClip(LoseClip);
            var aiLose = FindClip(AiLoseClip) ?? lose;
            var dance = FindClip(DanceClip);

            if (idle == null || run == null)
            {
                EditorUtility.DisplayDialog("Build Doll Animator", $"Need at least an idle ('{IdleClip}') and a '{RunClip}' clip under {DollFolder}.", "OK");
                return;
            }

            SetLoop(idle, true);
            SetLoop(walk, true);
            SetLoop(run, true);
            SetLoop(dance, true);
            SetLoop(jump, false);
            SetLoop(win, false);
            SetLoop(lose, false);
            SetLoop(aiLose, false);

            Clear(controller);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = "IsGrounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            controller.AddParameter("IsSliding", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = "RunAnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            foreach (var trigger in new[] { "Jump", "Slide", "Stumble", "Win", "Lose", "LoseAlt", "Dance" })
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            sm.entryPosition = new Vector3(0f, 0f);
            sm.anyStatePosition = new Vector3(0f, 200f);
            sm.exitPosition = new Vector3(800f, 0f);

            // Idle <-> Locomotion
            var idleState = sm.AddState("Idle", new Vector3(250f, 0f));
            idleState.motion = idle;
            sm.defaultState = idleState;

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            var children = sm.states;
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].state == locomotion)
                    children[i].position = new Vector3(500f, 0f);
            }
            sm.states = children;
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (walk != null)
                tree.AddChild(walk, 0f);
            tree.AddChild(run, walk != null ? 0.6f : 0f);

            // RunnerAnimator sets RunAnimSpeed so the stride matches her real speed and size.
            locomotion.speedParameterActive = true;
            locomotion.speedParameter = "RunAnimSpeed";

            var toRun = Transition(idleState, locomotion, 0.15f);
            toRun.AddCondition(AnimatorConditionMode.If, 0f, "IsRunning");
            var toIdle = Transition(locomotion, idleState, 0.2f);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsRunning");

            // Jump: in from anywhere on take-off, out when she lands.
            if (jump != null)
            {
                var jumpState = sm.AddState("Jump", new Vector3(500f, 150f));
                jumpState.motion = jump;

                // Play the whole jump within her time in the air.
                var movement = EditorPaths.FindAsset<Game.Player.Config.RunnerMovementConfig>();
                if (movement != null && movement.JumpAirTime > 0f)
                    jumpState.speed = Mathf.Clamp(jump.length / movement.JumpAirTime, 0.5f, 3f);
                AnyTransition(sm, jumpState, "Jump", 0.05f);

                var land = Transition(jumpState, locomotion, 0.1f);
                land.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                land.AddCondition(AnimatorConditionMode.If, 0f, "IsRunning");
                var landIdle = Transition(jumpState, idleState, 0.1f);
                landIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                landIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsRunning");
            }

            // Results and dance.
            AnimatorState danceState = null;
            if (dance != null)
            {
                danceState = sm.AddState("Dance", new Vector3(500f, 450f));
                danceState.motion = dance;
                AnyTransition(sm, danceState, "Dance", 0.2f);
            }

            if (win != null)
            {
                var winState = sm.AddState("Victory", new Vector3(250f, 350f));
                winState.motion = win;
                AnyTransition(sm, winState, "Win", 0.2f);

                if (danceState != null)
                {
                    var thenDance = Transition(winState, danceState, 0.3f);
                    thenDance.hasExitTime = true;
                    thenDance.exitTime = 0.95f;
                }
            }

            if (lose != null)
            {
                var loseState = sm.AddState("Defeat", new Vector3(250f, 450f));
                loseState.motion = lose;
                AnyTransition(sm, loseState, "Lose", 0.2f);
            }

            if (aiLose != null)
            {
                var aiLoseState = sm.AddState("Loser", new Vector3(250f, 550f));
                aiLoseState.motion = aiLose;
                AnyTransition(sm, aiLoseState, "LoseAlt", 0.2f);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var demo = SetupDemoDoll(controller);
            var report = $"{AssetDatabase.GetAssetPath(controller)} rebuilt.\n" +
                         $"Idle: {Name(idle)}\nWalk/Run: {Name(walk)} / {Name(run)}\nJump: {Name(jump)}\n" +
                         $"Win: {Name(win)} → {Name(dance)}\nLose: {Name(lose)}   AI lose: {Name(aiLose)}\n\n{demo}";
            Debug.Log(report, controller);
            EditorUtility.DisplayDialog("Build Doll Animator", report, "OK");
            EditorGUIUtility.PingObject(controller);
        }

        // Empties the controller but keeps the asset (and its GUID).
        private static void Clear(AnimatorController controller)
        {
            if (controller.layers.Length == 0)
                controller.AddLayer("Base Layer");

            var sm = controller.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions.ToArray())
                sm.RemoveAnyStateTransition(t);
            foreach (var t in sm.entryTransitions.ToArray())
                sm.RemoveEntryTransition(t);
            foreach (var child in sm.stateMachines.ToArray())
                sm.RemoveStateMachine(child.stateMachine);
            foreach (var child in sm.states.ToArray())
                sm.RemoveState(child.state);

            controller.parameters = new AnimatorControllerParameter[0];

            // Old blend trees stay behind as hidden sub-assets; remove them.
            var path = AssetDatabase.GetAssetPath(controller);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().ToArray())
                Object.DestroyImmediate(asset, true);
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            return t;
        }

        private static void AnyTransition(AnimatorStateMachine sm, AnimatorState to, string trigger, float duration)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            if (clip == null)
                return;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime == loop && settings.loopBlend == loop)
                return;

            // Clips inside an FBX are read-only; only .anim files can be changed here.
            if (!AssetDatabase.GetAssetPath(clip).EndsWith(".anim"))
            {
                Debug.LogWarning($"'{clip.name}' is inside a model file; set Loop Time on its FBX import settings by hand.", clip);
                return;
            }

            settings.loopTime = loop;
            settings.loopBlend = loop; // "Loop Pose": smooths the seam
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        // Prefers the extracted .anim over the clip inside the FBX.
        private static AnimationClip FindClip(string clipName)
        {
            AnimationClip fromModel = null;
            foreach (var guid in AssetDatabase.FindAssets($"\"{clipName}\" t:AnimationClip", new[] { DollFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (clip.name != clipName || clip.name.StartsWith("__preview__"))
                        continue;
                    if (path.EndsWith(".anim"))
                        return clip;
                    fromModel ??= clip;
                }
            }

            return fromModel;
        }

        private static AnimatorController FindController()
        {
            foreach (var guid in AssetDatabase.FindAssets($"{ControllerName} t:AnimatorController"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == ControllerName)
                    return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            }

            return null;
        }

        // The demo doll shows the target look at level start: make her dance, with this controller.
        private static string SetupDemoDoll(AnimatorController controller)
        {
            string path = null;
            foreach (var guid in AssetDatabase.FindAssets("DemoDoll t:Prefab", new[] { EditorPaths.Prefabs }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == "DemoDoll")
                    path = p;
            }

            if (path == null)
                return "DemoDoll.prefab not found, skipped.";

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    return "DemoDoll has no Animator (run Set Up Doll As Player first).";

                if (animator.runtimeAnimatorController == null)
                    animator.runtimeAnimatorController = controller;

                var trigger = root.GetComponent<DollAnimatorTrigger>();
                if (trigger == null)
                    trigger = root.AddComponent<DollAnimatorTrigger>();

                var so = new SerializedObject(trigger);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.FindProperty("trigger").stringValue = "Dance";
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return "DemoDoll will dance while showing the target look.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string Name(Object o) => o != null ? o.name : "(none)";
    }
}
#endif
