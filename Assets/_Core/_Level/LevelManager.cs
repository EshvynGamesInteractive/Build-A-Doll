using System.Collections.Generic;
using _Core._Player;
using _Core._UI;
using Core._GameEvent;
using Game.AI;
using Game.Hurdles;
using Game.Level;
using Game.Outfit;
using Game.Player.Input;
using UnityEngine;

namespace _Core._Level
{
    /// <summary>
    /// Lives in Bootstrap (persistent). When the Gameplay scene reports in, it spawns the player
    /// (the level's own doll, or the database default), builds the current level from its LevelData,
    /// handles the finish (score, stars, coins, saving progress) and loads the next level or a retry.
    /// The track starts at the scene's spawn point and runs along its forward axis.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelDatabase database;

        [Tooltip("Seconds after the finish before the next level / retry loads on its own. 0 = wait for the UI to raise RequestNextLevel / RequestRestartLevel.")]
        [SerializeField, Min(0f)] private float autoContinueDelay;

        [Header("Testing")]
        [Tooltip("Play this level number (1-based) instead of the saved one. 0 = use saved progress.")]
        [SerializeField, Min(0)] private int testLevelNumber;

        private Transform levelRoot;
        private Vector3 origin;
        private Quaternion _rotation;
        private LevelData current;
        private int currentIndex;
        private bool finished;
        private RunnerController player;
        private GameObject playerSource;
        private GameplayScene scene;
        private GameObject demo;
        private OutfitMatch match;

        // AI opponent on its own ramp next to the player's.
        private Transform aiRamp;
        private List<GameObject> aiHurdles = new();
        private RunnerController ai;
        private AiInput aiInput;
        private OutfitMatch aiMatch;

        // Where the demo stands at the start: this far down the track, facing the player.
        private const float DemoDistance = 4f;
        // How far to the side of the player the demo stands at the finish.
        private const float ComparisonSideOffset = 1.6f;

        public LevelData CurrentLevel => current;
        public int CurrentLevelNumber => currentIndex + 1;
        
        
        private void OnEnable()
        {
            GameEvents.GameplayEvents.FinishReached.Register(OnFinishReached);
            GameEvents.GameplayEvents.RequestNextLevel.Register(LoadNext);
            GameEvents.GameplayEvents.RequestPlaySavedLevel.Register(LoadCurrent);
            GameEvents.GameplayEvents.RequestRestartLevel.Register(RestartCurrent);
            GameEvents.GameplayEvents.GameplaySceneReady.Register(OnGameplaySceneReady);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.FinishReached.Unregister(OnFinishReached);
            GameEvents.GameplayEvents.RequestNextLevel.Unregister(LoadNext);
            GameEvents.GameplayEvents.RequestPlaySavedLevel.Unregister(LoadCurrent);
            GameEvents.GameplayEvents.RequestRestartLevel.Unregister(RestartCurrent);
            GameEvents.GameplayEvents.GameplaySceneReady.Unregister(OnGameplaySceneReady);
        }

        // Cache scene references so the Home screen can start the saved level on demand.
        private void OnGameplaySceneReady(GameplayScene scene)
        {
            if (database == null)
            {
                Debug.LogError($"{nameof(LevelManager)} needs a {nameof(LevelDatabase)}.", this);
                return;
            }

            this.scene = scene;
            player = null;
            playerSource = null;

            var spawn = scene.PlayerSpawnPoint;
            origin = spawn.position;
            _rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(spawn.forward, Vector3.up), Vector3.up);
        }

        /// <summary>Starts the saved level from Home, or the configured test level.</summary>
        public void LoadCurrent()
        {
            var index = testLevelNumber > 0 ? testLevelNumber - 1 : LevelProgress.CurrentLevelIndex;
            LoadLevel(index);
        }

        private void RestartCurrent() => LoadLevel(currentIndex);

        private void LoadNext() => LoadLevel(currentIndex + 1);

        public void LoadLevel(int index)
        {
            // Drop any pending demo / result / auto-continue timers from the previous level.
            CancelInvoke();

            var level = database.GetLevel(index);
            if (level == null)
            {
                Debug.LogError("Level database is empty.", database);
                return;
            }

            if (scene == null)
            {
                Debug.LogWarning($"{nameof(LevelManager)}: the Gameplay scene hasn't loaded yet.", this);
                return;
            }

            current = level;
            currentIndex = Mathf.Max(0, index);
            finished = false;

            Build(level);
            SpawnPlayer(level);

            if (player == null)
            {
                Debug.LogError($"{nameof(LevelManager)}: no player. Set a Default Player Prefab on the {nameof(LevelDatabase)} or a Scene Player on {nameof(GameplayScene)}.", this);
                return;
            }
            var outfit = player.DollOutfit;
            if (outfit != null)
                outfit.SetStartingItems(level.StartingOutfit);

            SpawnAi(level);

            // Puts the player, her outfit, score and any hurdles back to their start state.
            GameEvents.GameplayEvents.ResetRun.Raise();
            GameEvents.GameplayEvents.LevelLoaded.Raise(level, CurrentLevelNumber);

            GameEvents.UIEvents.ShowByType.Raise(ScreenType.Gameplay);
            ShowTargetLook(level);
        }

        // Uses the level's doll if it has one, else the default. Keeps the current player when it's
        // already the right one, so moving between levels doesn't rebuild her every time.
        private void SpawnPlayer(LevelData level)
        {
            var prefab = level.PlayerPrefab != null ? level.PlayerPrefab : database.DefaultPlayerPrefab;
            var source = prefab != null ? prefab : (scene.ScenePlayer != null ? scene.ScenePlayer.gameObject : null);

            if (player != null && playerSource == source)
                return;

            if (player != null && player != scene.ScenePlayer)
                Destroy(player.gameObject);

            if (scene.ScenePlayer != null)
                scene.ScenePlayer.gameObject.SetActive(prefab == null);

            playerSource = source;

            if (prefab == null)
            {
                player = scene.ScenePlayer;
            }
            else
            {
                // Spawn straight at the start pose: the controller remembers where it started in Awake.
                var instance = Instantiate(prefab, origin, _rotation);
                instance.name = prefab.name;

                // A prefab saved inactive would spawn hidden; the player must always be on.
                if (!instance.activeSelf)
                    instance.SetActive(true);
                player = instance.GetComponent<RunnerController>();

                if (player == null)
                {
                    Debug.LogError($"Player prefab '{prefab.name}' has no {nameof(RunnerController)}.", prefab);
                    Destroy(instance);
                    return;
                }
            }

            if (ReferenceManager.Instance != null)
                ReferenceManager.Instance.player = player;

            if (scene.CameraFollow != null && player != null)
                scene.CameraFollow.SetTarget(player.transform);

            if (player != null)
                GameEvents.GameplayEvents.PlayerSpawned.Raise(player);
        }

        private void Build(LevelData level)
        {
            if (levelRoot != null)
                Destroy(levelRoot.gameObject);

            levelRoot = new GameObject($"Level {CurrentLevelNumber} - {level.DisplayName}").transform;
            levelRoot.SetPositionAndRotation(origin, _rotation);

            if (level.EnvironmentPrefab != null)
                Instantiate(level.EnvironmentPrefab, levelRoot, false);

            // Player's ramp is the level root itself.
            BuildRamp(level, levelRoot, database.SpawnPlayerRamp, true);

            // AI's ramp: the same hurdles, off to the side. Its hurdles are kept for the AI's plan.
            aiRamp = null;
            aiHurdles = new List<GameObject>();
            if (GetAiProfile(level) == null)
                return;

            aiRamp = new GameObject("AI Ramp").transform;
            aiRamp.SetParent(levelRoot, false);
            aiRamp.localPosition = new Vector3(database.AiRampOffset, 0f, 0f);
            aiHurdles = BuildRamp(level, aiRamp, true, false);
        }

        // Spawns one ramp's ground, hurdles and finish line under 'root'. Returns the hurdles in entry order.
        private List<GameObject> BuildRamp(LevelData level, Transform root, bool spawnGround, bool warnMissing)
        {
            if (spawnGround && database.RampPrefab != null)
            {
                // 1 unit long from z = 0 → stretch from a bit behind the start to past the finish.
                var zAxisScale = level.FinishDistance / 4;
                const float behindStart = 5f;
                const float pastFinish = 15f;
                var ground = Instantiate(database.RampPrefab, root, false);
                ground.transform.localPosition = new Vector3(0f, 0f, zAxisScale + zAxisScale);
                ground.transform.localScale = new Vector3(1f, 1f, zAxisScale);
                // ground.transform.localScale = new Vector3(1f, 1f, level.FinishDistance + behindStart + pastFinish);
            }

            var hurdles = new List<GameObject>();
            var missing = 0;
            foreach (var entry in level.Entries)
            {
                if (entry.prefab == null)
                {
                    missing++;
                    continue;
                }

                var instance = Spawn(root, entry.prefab, entry.distance, entry.lateral);
                ConfigureGates(instance, entry.gateItems, level.TargetOutfit);
                hurdles.Add(instance);
            }

            if (missing > 0 && warnMissing)
                Debug.LogWarning($"'{level.name}': {missing} of {level.Entries.Count} entries have no prefab (deleted or never set), so they were skipped.", level);

            var finish = level.FinishLinePrefab != null ? level.FinishLinePrefab : database.DefaultFinishLine;
            if (finish != null)
                Spawn(root, finish, level.FinishDistance, 0f);
            else if (warnMissing)
                Debug.LogWarning($"No finish line prefab for '{level.name}'. Set one on the level or the database.", level);

            return hurdles;
        }

        private static GameObject Spawn(Transform root, GameObject prefab, float distance, float lateral)
        {
            // Spawn straight at the final pose: movers remember where they were in Awake.
            var local = new Vector3(lateral, 0f, distance) + prefab.transform.localPosition;
            var position = root.TransformPoint(local);
            var rotation = root.rotation * prefab.transform.localRotation;
            return Instantiate(prefab, position, rotation, root);
        }

        // ---------- AI ----------

        private AiProfile GetAiProfile(LevelData level)
        {
            if (!level.HasAiOpponent)
                return null;
            return level.AiProfile != null ? level.AiProfile : database.DefaultAiProfile;
        }

        // The AI is a copy of the player's doll (or the AI prefab) driven by AiInput instead of touch.
        private void SpawnAi(LevelData level)
        {
            ai = null; // the previous one went with the old level root
            aiInput = null;

            var profile = GetAiProfile(level);
            if (profile == null || aiRamp == null)
                return;

            var prefab = database.AiPrefab != null ? database.AiPrefab
                : level.PlayerPrefab != null ? level.PlayerPrefab
                : database.DefaultPlayerPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"No doll for the AI. Set an AI Prefab or Default Player Prefab on the {nameof(LevelDatabase)}.", database);
                return;
            }

            var instance = Instantiate(prefab, aiRamp.position, aiRamp.rotation, aiRamp);
            instance.name = "AI";
            if (!instance.activeSelf)
                instance.SetActive(true);

            if (!instance.TryGetComponent(out ai))
            {
                Debug.LogError($"AI doll '{prefab.name}' has no {nameof(RunnerController)}.", prefab);
                Destroy(instance);
                return;
            }

            // No touch input on the AI.
            if (instance.TryGetComponent<PointerDragInput>(out var touch))
            {
                touch.enabled = false;
                Destroy(touch);
            }

            if (!instance.TryGetComponent(out aiInput))
                aiInput = instance.AddComponent<AiInput>();

            ai.SetAI(true);
            ai.SetInput(aiInput, aiInput);
            ai.SetConfig(profile.MovementConfig);

            if (ai.DollOutfit != null)
                ai.DollOutfit.SetStartingItems(level.StartingOutfit);

            var trackWidth = player.Config != null ? player.Config.TrackHalfWidth * 2f : 4.4f;
            aiInput.Init(ai, aiRamp, profile, AiPlanner.Plan(aiHurdles, aiRamp, level.TargetLook, profile, trackWidth));
        }

        private void StartAi()
        {
            if (aiInput != null)
                aiInput.Go();
        }

        private static void ConfigureGates(GameObject instance, OutfitItem[] items, IReadOnlyList<OutfitItem> targetItems)
        {
            var gates = instance.GetComponentsInChildren<ItemGate>();
            for (var i = 0; i < gates.Length; i++)
            {
                var itemOverride = items != null && i < items.Length ? items[i] : null;
                gates[i].Configure(itemOverride, targetItems);
            }
        }

        // ---------- Target look ----------

        // Level start: the demo doll shows the look to copy; the run can't start until it's hidden.
        private void ShowTargetLook(LevelData level)
        {
            demo = null; // the old one went with the previous level root

            var prefab = level.DemoPrefab != null ? level.DemoPrefab : database.DefaultDemoPrefab;
            if (!level.HasTarget || prefab == null)
            {
                if (level.HasTarget)
                    Debug.LogWarning($"'{level.name}' has a target look but no demo doll prefab (set Default Demo Prefab on the {nameof(LevelDatabase)}).", level);

                player.StartLocked = false;
                StartAi();
                return;
            }

            var point = scene.DemoPoint;
            var position = point != null ? point.position : levelRoot.TransformPoint(new Vector3(0f, 0f, DemoDistance));
            var rotation = point != null ? point.rotation : _rotation * Quaternion.Euler(0f, 180f, 0f);

            // Child of the level root, so it's cleaned up with the level.
            demo = Instantiate(prefab, position, rotation, levelRoot);
            demo.name = "TargetLook";
            if (!demo.activeSelf)
                demo.SetActive(true);

            var demoOutfit = demo.GetComponentInChildren<DollOutfit>();
            if (demoOutfit != null)
                demoOutfit.SetStartingItems(level.TargetOutfit); // worn in its Start
            else
                Debug.LogWarning($"Demo prefab '{prefab.name}' has no {nameof(DollOutfit)}, so it can't show the look.", prefab);

            player.StartLocked = true;
            GameEvents.GameplayEvents.TargetLookShown.Raise(level);
            Invoke(nameof(HideTargetLook), database.DemoShowTime);
        }

        private void HideTargetLook()
        {
            if (demo != null)
                demo.SetActive(false);

            if (player != null)
                player.StartLocked = false;

            StartAi();
            GameEvents.GameplayEvents.TargetLookHidden.Raise();
        }

        // ---------- Finish ----------

        // Finish: stop, put the demo next to the player, compare both dolls, then show the result.
        private void OnFinishReached(RunnerController runner)
        {
            // The AI crossing its own finish line just stops the AI.
            if (runner != null && runner.IsAI)
            {
                runner.Stop();
                return;
            }

            if (finished || current == null)
                return;

            finished = true;
            GameEvents.GameplayEvents.StopRun.Raise();

            if (player == null)
            {
                Debug.LogError($"{nameof(LevelManager)}: no player assigned on ReferenceManager.", this);
                return;
            }

            match = OutfitComparer.Compare(current.TargetOutfit, player.DollOutfit);
            aiMatch = ai != null ? OutfitComparer.Compare(current.TargetOutfit, ai.DollOutfit) : default;

            if (!current.HasTarget || demo == null)
            {
                ShowResult();
                return;
            }

            // Stand the demo just past the finish, on the side of the track the player isn't on.
            var playerLocal = levelRoot.InverseTransformPoint(player.transform.position);
            var side = playerLocal.x > 0f ? -1f : 1f;
            var demoLocal = new Vector3(playerLocal.x + side * ComparisonSideOffset, 0f, current.FinishDistance + 1f);
            demo.transform.SetPositionAndRotation(levelRoot.TransformPoint(demoLocal), _rotation * Quaternion.Euler(0f, 180f, 0f));
            demo.SetActive(true);

            GameEvents.GameplayEvents.ComparisonStarted.Raise(match, aiMatch);
            Invoke(nameof(ShowResult), database.ComparisonTime);
        }

        private void ShowResult()
        {
            if (player == null || current == null)
                return;

            var runScore = player.Score;
            var coins = runScore != null ? runScore.Coins : 0;

            // With a target look the score is how well she copied it (0..100); otherwise the run score.
            var score = current.HasTarget ? match.Percent : (runScore != null ? runScore.Score : 0);

            // The level pass mark applies in every race. Against AI, ties also fail.
            var hasAi = ai != null;
            var aiScore = hasAi ? GetAiScore() : 0;
            var passed = current.IsPass(score) && (!hasAi || score >= aiScore);

            if (passed)
            {
                var result = new LevelResult(current, CurrentLevelNumber, true, score, current.GetStars(score), coins, current.CompletionCoins, match, hasAi, aiScore, aiMatch);
                if (testLevelNumber > 0 || currentIndex >= LevelProgress.CurrentLevelIndex)
                {
                    LevelProgress.AddCoins(result.TotalCoins);
                    if (testLevelNumber == 0)
                        LevelProgress.CurrentLevelIndex = currentIndex + 1;
                }

                GameEvents.GameplayEvents.LevelCompleted.Raise(result);
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.LevelCompletePanel);
            }
            else
            {
                var result = new LevelResult(current, CurrentLevelNumber, false, score, 0, coins, 0, match, hasAi, aiScore, aiMatch);
                GameEvents.GameplayEvents.LevelFailed.Raise(result);
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.LevelFailedPanel);
            }

            if (autoContinueDelay > 0f)
                Invoke(passed ? nameof(LoadNext) : nameof(RestartCurrent), autoContinueDelay);
        }

        private int GetAiScore()
        {
            if (current.HasTarget)
                return aiMatch.Percent;
            return ai.Score != null ? ai.Score.Score : 0;
        }

        [ContextMenu("Reset Saved Progress")]
        private void ResetProgress() => LevelProgress.CurrentLevelIndex = 0;
    }
}
