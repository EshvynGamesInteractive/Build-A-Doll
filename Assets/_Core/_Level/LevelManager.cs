using _Core;
using _Core._Player;
using _Core.UI;
using Core._GameEvent;
using Game.Hurdles;
using Game.Outfit;
using Game.Score;
using UnityEngine;

namespace Game.Level
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
        [SerializeField, Min(0f)] private float autoContinueDelay = 2.5f;

        [Header("Testing")]
        [Tooltip("Play this level number (1-based) instead of the saved one. 0 = use saved progress.")]
        [SerializeField, Min(0)] private int testLevelNumber;

        private Transform _levelRoot;
        private Vector3 _origin;
        private Quaternion _rotation;
        private LevelData _current;
        private int _currentIndex;
        private bool _finished;
        private RunnerController player;
        private GameObject _playerSource;
        private GameplayScene _scene;
        private GameObject _demo;
        private OutfitMatch _match;

        // Where the demo stands at the start: this far down the track, facing the player.
        private const float DemoDistance = 4f;
        // How far to the side of the player the demo stands at the finish.
        private const float ComparisonSideOffset = 1.6f;

        public LevelData CurrentLevel => _current;
        public int CurrentLevelNumber => _currentIndex + 1;
        
        
        private void OnEnable()
        {
            GameEvents.GameplayEvents.FinishReached.Register(OnFinishReached);
            GameEvents.GameplayEvents.RequestNextLevel.Register(LoadCurrent);
            GameEvents.GameplayEvents.RequestRestartLevel.Register(LoadCurrent);
            GameEvents.GameplayEvents.GameplaySceneReady.Register(OnGameplaySceneReady);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.FinishReached.Unregister(OnFinishReached);
            GameEvents.GameplayEvents.RequestNextLevel.Unregister(LoadCurrent);
            GameEvents.GameplayEvents.RequestRestartLevel.Unregister(LoadCurrent);
            GameEvents.GameplayEvents.GameplaySceneReady.Unregister(OnGameplaySceneReady);
        }

        // The Gameplay scene has loaded: take its references and start the current level.
        private void OnGameplaySceneReady(GameplayScene scene)
        {
            if (database == null)
            {
                Debug.LogError($"{nameof(LevelManager)} needs a {nameof(LevelDatabase)}.", this);
                return;
            }

            _scene = scene;
            player = null;
            _playerSource = null;

            var spawn = scene.PlayerSpawnPoint;
            _origin = spawn.position;
            _rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(spawn.forward, Vector3.up), Vector3.up);

            LoadCurrent();
        }

        /// <summary>Loads the saved level. Progress only moves on when a level is passed,
        /// so this is both "next level" after a win and "retry" after a fail.</summary>
        public void LoadCurrent()
        {
            var index = testLevelNumber > 0 ? testLevelNumber - 1 : LevelProgress.CurrentLevelIndex;
            LoadLevel(index);
        }

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

            if (_scene == null)
            {
                Debug.LogWarning($"{nameof(LevelManager)}: the Gameplay scene hasn't loaded yet.", this);
                return;
            }

            _current = level;
            _currentIndex = Mathf.Max(0, index);
            _finished = false;

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

            // Puts the player, her outfit, score and any hurdles back to their start state.
            GameEvents.GameplayEvents.ResetRun.Raise();
            GameEvents.GameplayEvents.LevelLoaded.Raise(level, CurrentLevelNumber);

            ShowTargetLook(level);
        }

        // Uses the level's doll if it has one, else the default. Keeps the current player when it's
        // already the right one, so moving between levels doesn't rebuild her every time.
        private void SpawnPlayer(LevelData level)
        {
            var prefab = level.PlayerPrefab != null ? level.PlayerPrefab : database.DefaultPlayerPrefab;
            var source = prefab != null ? prefab : (_scene.ScenePlayer != null ? _scene.ScenePlayer.gameObject : null);

            if (player != null && _playerSource == source)
                return;

            if (player != null && player != _scene.ScenePlayer)
                Destroy(player.gameObject);

            if (_scene.ScenePlayer != null)
                _scene.ScenePlayer.gameObject.SetActive(prefab == null);

            _playerSource = source;

            if (prefab == null)
            {
                player = _scene.ScenePlayer;
            }
            else
            {
                // Spawn straight at the start pose: the controller remembers where it started in Awake.
                var instance = Instantiate(prefab, _origin, _rotation);
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

            if (_scene.CameraFollow != null && player != null)
                _scene.CameraFollow.SetTarget(player.transform);

            if (player != null)
                GameEvents.GameplayEvents.PlayerSpawned.Raise(player);
        }

        private void Build(LevelData level)
        {
            if (_levelRoot != null)
                Destroy(_levelRoot.gameObject);

            _levelRoot = new GameObject($"Level {CurrentLevelNumber} - {level.DisplayName}").transform;
            _levelRoot.SetPositionAndRotation(_origin, _rotation);

            if (level.EnvironmentPrefab != null)
                Instantiate(level.EnvironmentPrefab, _levelRoot, false);

            var missing = 0;
            foreach (var entry in level.Entries)
            {
                if (entry.prefab == null)
                {
                    missing++;
                    continue;
                }

                var instance = Spawn(entry.prefab, entry.distance, entry.lateral);
                ApplyGateItems(instance, entry.gateItems);
            }

            if (missing > 0)
                Debug.LogWarning($"'{level.name}': {missing} of {level.Entries.Count} entries have no prefab (deleted or never set), so they were skipped.", level);

            var finish = level.FinishLinePrefab != null ? level.FinishLinePrefab : database.DefaultFinishLine;
            if (finish != null)
                Spawn(finish, level.FinishDistance, 0f);
            else
                Debug.LogWarning($"No finish line prefab for '{level.name}'. Set one on the level or the database.", level);
        }

        private GameObject Spawn(GameObject prefab, float distance, float lateral)
        {
            // Spawn straight at the final pose: movers remember where they were in Awake.
            var local = new Vector3(lateral, 0f, distance) + prefab.transform.localPosition;
            var position = _levelRoot.TransformPoint(local);
            var rotation = _levelRoot.rotation * prefab.transform.localRotation;
            return Instantiate(prefab, position, rotation, _levelRoot);
        }

        private static void ApplyGateItems(GameObject instance, OutfitItem[] items)
        {
            if (items == null || items.Length == 0)
                return;

            var gates = instance.GetComponentsInChildren<ItemGate>();
            for (var i = 0; i < gates.Length && i < items.Length; i++)
            {
                if (items[i] != null)
                    gates[i].SetItem(items[i]);
            }
        }

        // ---------- Target look ----------

        // Level start: the demo doll shows the look to copy; the run can't start until it's hidden.
        private void ShowTargetLook(LevelData level)
        {
            _demo = null; // the old one went with the previous level root

            var prefab = level.DemoPrefab != null ? level.DemoPrefab : database.DefaultDemoPrefab;
            if (!level.HasTarget || prefab == null)
            {
                if (level.HasTarget)
                    Debug.LogWarning($"'{level.name}' has a target look but no demo doll prefab (set Default Demo Prefab on the {nameof(LevelDatabase)}).", level);

                player.StartLocked = false;
                return;
            }

            var point = _scene.DemoPoint;
            var position = point != null ? point.position : _levelRoot.TransformPoint(new Vector3(0f, 0f, DemoDistance));
            var rotation = point != null ? point.rotation : _rotation * Quaternion.Euler(0f, 180f, 0f);

            // Child of the level root, so it's cleaned up with the level.
            _demo = Instantiate(prefab, position, rotation, _levelRoot);
            _demo.name = "TargetLook";
            if (!_demo.activeSelf)
                _demo.SetActive(true);

            var demoOutfit = _demo.GetComponentInChildren<DollOutfit>();
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
            if (_demo != null)
                _demo.SetActive(false);

            if (player != null)
                player.StartLocked = false;

            GameEvents.GameplayEvents.TargetLookHidden.Raise();
        }

        // ---------- Finish ----------

        // Finish: stop, put the demo next to the player, compare, then show the result.
        private void OnFinishReached()
        {
            if (_finished || _current == null)
                return;

            _finished = true;
            GameEvents.GameplayEvents.StopRun.Raise();

            if (player == null)
            {
                Debug.LogError($"{nameof(LevelManager)}: no player assigned on ReferenceManager.", this);
                return;
            }

            _match = OutfitComparer.Compare(_current.TargetOutfit, player.DollOutfit);

            if (!_current.HasTarget || _demo == null)
            {
                ShowResult();
                return;
            }

            // Stand the demo just past the finish, on the side of the track the player isn't on.
            var playerLocal = _levelRoot.InverseTransformPoint(player.transform.position);
            var side = playerLocal.x > 0f ? -1f : 1f;
            var demoLocal = new Vector3(playerLocal.x + side * ComparisonSideOffset, 0f, _current.FinishDistance + 1f);
            _demo.transform.SetPositionAndRotation(_levelRoot.TransformPoint(demoLocal), _rotation * Quaternion.Euler(0f, 180f, 0f));
            _demo.SetActive(true);

            GameEvents.GameplayEvents.ComparisonStarted.Raise(_match);
            Invoke(nameof(ShowResult), database.ComparisonTime);
        }

        private void ShowResult()
        {
            if (player == null || _current == null)
                return;

            var runScore = player.Score;
            var coins = runScore != null ? runScore.Coins : 0;

            // With a target look the score is how well she copied it (0..100); otherwise the run score.
            var score = _current.HasTarget ? _match.Percent : (runScore != null ? runScore.Score : 0);
            var passed = _current.IsPass(score);

            if (passed)
            {
                var result = new LevelResult(_current, CurrentLevelNumber, true, score, _current.GetStars(score), coins, _current.CompletionCoins, _match);
                LevelProgress.AddCoins(result.TotalCoins);
                if (testLevelNumber == 0)
                    LevelProgress.CurrentLevelIndex = _currentIndex + 1;

                GameEvents.GameplayEvents.LevelCompleted.Raise(result);
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.LevelComplete);
            }
            else
            {
                var result = new LevelResult(_current, CurrentLevelNumber, false, score, 0, coins, 0, _match);
                GameEvents.GameplayEvents.LevelFailed.Raise(result);
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.LevelFailed);
            }

            if (autoContinueDelay > 0f)
                Invoke(nameof(LoadCurrent), autoContinueDelay);
        }

        [ContextMenu("Reset Saved Progress")]
        private void ResetProgress() => LevelProgress.CurrentLevelIndex = 0;
    }
}
