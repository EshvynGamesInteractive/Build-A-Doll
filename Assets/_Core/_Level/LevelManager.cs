using System.Collections;
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
    /// Lives in Bootstrap (persistent). Ramps, start / end points, finish lines, finish stages and the demo
    /// camera + light are placed in the Gameplay scene and handed over by GameplayScene; nothing of that is
    /// created here. Per level it only spawns what changes between levels: the player and AI dolls, the
    /// level's hurdles (from its LevelData) and the demo doll. It also handles the finish (stage, comparison,
    /// score, stars, coins, saving progress) and loads the next level or a retry.
    /// The player's track runs from the scene's Player Start Point along its forward axis to Player End Point.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelDatabase database;

        public LevelDatabase Database => database;

        [Tooltip("Seconds after the finish before the next level / retry loads on its own. 0 = wait for the UI to raise RequestNextLevel / RequestRestartLevel.")]
        [SerializeField, Min(0f)] private float autoContinueDelay;

        [Tooltip("On: the player starts running by herself as soon as the target-look intro ends (demo moved to its corner). Off: she waits for the first touch.")]
        [SerializeField] private bool autoStartRun = true;

        [Header("Testing")]
        [Tooltip("Play this level number (1-based) instead of the saved one. 0 = use saved progress.")]
        [SerializeField, Min(0)] private int testLevelNumber;

        // Player track: start point position + flat forward. Everything "track space" below is x = sideways,
        // y = up, z = along the track from the start point.
        private Vector3 origin;
        private Quaternion _rotation;
        private float _finishDistance;

        private LevelData current;
        private int currentIndex;
        private bool finished;
        private RunnerController player;
        private GameObject playerSource;
        private GameplayScene scene;
        private GameObject demo;
        private Camera demoCamera;
        private bool waitingForTargetLook;
        private OutfitMatch match;

        // Everything spawned for the current level (hurdles, AI, demo doll); destroyed when the next one loads.
        private readonly List<GameObject> _spawned = new();

        // AI opponent on the scene's AI ramp.
        private List<GameObject> aiHurdles = new();
        private RunnerController ai;
        private AiInput aiInput;
        private OutfitMatch aiMatch;

        public LevelData CurrentLevel => current;
        public int CurrentLevelNumber => currentIndex + 1;


        private void OnEnable()
        {
            GameEvents.GameplayEvents.FinishReached.Register(OnFinishReached);
            GameEvents.GameplayEvents.RequestNextLevel.Register(LoadNext);
            GameEvents.GameplayEvents.RequestPlaySavedLevel.Register(LoadCurrent);
            GameEvents.GameplayEvents.RequestPlayLevel.Register(LoadLevel);
            GameEvents.GameplayEvents.RequestRestartLevel.Register(RestartCurrent);
            GameEvents.GameplayEvents.GameplaySceneReady.Register(OnGameplaySceneReady);
            GameEvents.GameplayEvents.TargetLookIntroDone.Register(OnTargetLookIntroDone);
            GameEvents.GameplayEvents.OutfitItemEquipped.Register(OnPlayerEquipped);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.FinishReached.Unregister(OnFinishReached);
            GameEvents.GameplayEvents.RequestNextLevel.Unregister(LoadNext);
            GameEvents.GameplayEvents.RequestPlaySavedLevel.Unregister(LoadCurrent);
            GameEvents.GameplayEvents.RequestPlayLevel.Unregister(LoadLevel);
            GameEvents.GameplayEvents.RequestRestartLevel.Unregister(RestartCurrent);
            GameEvents.GameplayEvents.GameplaySceneReady.Unregister(OnGameplaySceneReady);
            GameEvents.GameplayEvents.TargetLookIntroDone.Unregister(OnTargetLookIntroDone);
            GameEvents.GameplayEvents.OutfitItemEquipped.Unregister(OnPlayerEquipped);
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

            // A freshly loaded scene: everything from the old one is already gone.
            player = null;
            playerSource = null;
            _spawned.Clear();
            ClearLevelState();

            var start = scene.PlayerStartPoint;
            if (start == null)
            {
                Debug.LogError($"{nameof(GameplayScene)} has no Player Start Point.", scene);
                start = scene.transform;
            }

            origin = start.position;
            _rotation = FlatRotation(start);

            // Nothing to film until a level shows its target look.
            SetDemoViewActive(false);
            SetSceneStagesActive(null);
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
            StopFinishSequence();

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

            ClearSpawned();
            Build(level);
            SpawnPlayer(level);

            if (player == null)
            {
                Debug.LogError($"{nameof(LevelManager)}: no player. Set a Default Player Prefab on the {nameof(LevelDatabase)} (or a Player Prefab on the level).", this);
                return;
            }
            var outfit = player.DollOutfit;
            if (outfit != null)
                outfit.SetStartingItems(level.StartingOutfit);

            SpawnAi(level);

            // Scene stages start hidden; the one picked for this level is shown by TryShowStage.
            SetSceneStagesActive(null);

            // Finish stage: always shown at level start, standing past the finish line during the run.
            _stageShown = true;
            _hasStage = TryShowStage(ai != null, out _stagePlayerSpot, out _stageAiSpot, out _stageFrontZ);

            // Puts the player, her outfit, score and any hurdles back to their start state.
            GameEvents.GameplayEvents.ResetRun.Raise();

            // Back to following her (the finish stage view left the camera on the stage).
            if (scene.CameraFollow != null)
                scene.CameraFollow.SetTarget(player.transform);
            GameEvents.GameplayEvents.LevelLoaded.Raise(level, CurrentLevelNumber);

            GameEvents.UIEvents.ShowByType.Raise(ScreenType.Gameplay);
            ShowTargetLook(level);
        }

        // Destroys what the previous level spawned (hurdles, AI, demo doll). Scene objects are left alone.
        private void ClearSpawned()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                    Destroy(go);
            }

            _spawned.Clear();
            ClearLevelState();
        }

        private void ClearLevelState()
        {
            ai = null;
            aiInput = null;
            aiHurdles = new List<GameObject>();
            demo = null;
        }

        // Uses the level's doll if it has one, else the default. Keeps the current player when it's
        // already the right one, so moving between levels doesn't rebuild her every time.
        private void SpawnPlayer(LevelData level)
        {
            var prefab = level.PlayerPrefab != null ? level.PlayerPrefab : database.DefaultPlayerPrefab;
            if (prefab == null)
                return;

            if (player != null && playerSource == prefab)
                return;

            if (player != null)
                Destroy(player.gameObject);
            player = null;
            playerSource = prefab;

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
                playerSource = null;
                return;
            }

            if (ReferenceManager.Instance != null)
                ReferenceManager.Instance.player = player;

            if (scene.CameraFollow != null)
                scene.CameraFollow.SetTarget(player.transform);

            MakeupOf(player);
            GameEvents.GameplayEvents.PlayerSpawned.Raise(player);
        }

        // ---------- Level (scene ramps + spawned hurdles) ----------

        private void Build(LevelData level)
        {
            _freeStyleGates = 0;
            _freeStyleScore = 0;
            _freeStyleOutfitGates.Clear();
            _finishDistance = TrackDistanceTo(scene.PlayerEndPoint, level.FinishDistance, "Player End Point");

            var hasAi = GetAiProfile(level) != null && scene.AiStartPoint != null;
            if (GetAiProfile(level) != null && scene.AiStartPoint == null)
                Debug.LogWarning($"'{level.name}' has an AI opponent but {nameof(GameplayScene)} has no AI Start Point, so the AI is skipped.", scene);

            // The AI ramp is only there on levels with an AI.
            if (scene.AiRamp != null && scene.AiRamp.activeSelf != hasAi)
                scene.AiRamp.SetActive(hasAi);

            // Player's ramp gets a random texture (not the same as last level's); the AI's a different one.
            var playerTexture = PickRampTexture(_lastRampTexture);
            _lastRampTexture = playerTexture;
            if (scene.PlayerRamp != null)
                ApplyRampTexture(scene.PlayerRamp.transform, TextureAt(playerTexture), scene.PlayerStartPoint, scene.PlayerEndPoint);

            var playerParent = scene.PlayerRamp != null ? scene.PlayerRamp.transform : null;
            SpawnHurdles(level, origin, _rotation, _finishDistance, playerParent, true);

            if (!hasAi)
                return;

            if (scene.AiRamp != null)
                ApplyRampTexture(scene.AiRamp.transform, TextureAt(PickRampTexture(playerTexture)), scene.AiStartPoint, scene.AiEndPoint);

            var aiStart = scene.AiStartPoint;
            var aiFinish = TrackDistance(aiStart.position, FlatRotation(aiStart), scene.AiEndPoint, _finishDistance);
            var aiParent = scene.AiRamp != null ? scene.AiRamp.transform : null;
            aiHurdles = SpawnHurdles(level, aiStart.position, FlatRotation(aiStart), aiFinish, aiParent, false);
        }

        // Spawns the level's hurdles along one ramp: 'start' + 'rotation' is the ramp's start pose.
        // Entries at or past the finish are skipped (the finish line is the scene's End Point). Returns them in entry order.
        private List<GameObject> SpawnHurdles(LevelData level, Vector3 start, Quaternion rotation, float finishDistance, Transform parent, bool warn)
        {
            var hurdles = new List<GameObject>();
            int missing = 0, pastFinish = 0;
            foreach (var entry in level.Entries)
            {
                if (entry.prefab == null)
                {
                    missing++;
                    continue;
                }

                if (entry.distance >= finishDistance)
                {
                    pastFinish++;
                    continue;
                }

                // Spawn straight at the final pose: movers remember where they were in Awake.
                var local = new Vector3(entry.lateral, 0f, entry.distance) + entry.prefab.transform.localPosition;
                var instance = Instantiate(entry.prefab, start + rotation * local, rotation * entry.prefab.transform.localRotation, parent);
                _spawned.Add(instance);

                if (ConfigureGates(instance, entry.gateItems, level.TargetOutfit, level.IsFreeStyle) && warn)
                {
                    _freeStyleGates++; // gate choices on the player's ramp (free style: best possible score)
                    if (level.IsFreeStyle)
                        CountFreeStyleOutfits(instance);
                }
                ConfigureCoinGates(instance, entry.coinGates);
                ConfigurePaintGates(instance, entry.paintGates, level.TargetLook);
                ConfigureSkinGates(instance, entry.skinGates, level);
                var colorGates = instance.GetComponentsInChildren<DressColorGate>();
                for (var g = 0; g < colorGates.Length; g++)
                {
                    DressColorGate.Setting? colorSetting = entry.dressColorGates != null && g < entry.dressColorGates.Length ? entry.dressColorGates[g] : null;
                    colorGates[g].Configure(colorSetting, level.HasTargetDressColor, level.TargetDressColor);
                }
                foreach (var station in instance.GetComponentsInChildren<MakeupStation>())
                    station.Configure(entry.paintGates, level.TargetLook);
                hurdles.Add(instance);
            }

            if (warn && missing > 0)
                Debug.LogWarning($"'{level.name}': {missing} of {level.Entries.Count} entries have no prefab (deleted or never set), so they were skipped.", level);
            if (warn && pastFinish > 0)
                Debug.LogWarning($"'{level.name}': {pastFinish} entries are at or past the finish ({finishDistance:0.#} m from the start point to the End Point), so they were skipped. " +
                                 $"Move the End Point further down the ramp (this level needs about {level.FinishDistance:0} m).", level);

            return hurdles;
        }

        // Distance along the player's track from the start point to 'end'; 'fallback' if there's no end point.
        private float TrackDistanceTo(Transform end, float fallback, string label)
        {
            if (end == null)
            {
                Debug.LogWarning($"{nameof(GameplayScene)} has no {label}; using the level's Finish Distance ({fallback} m).", scene);
                return fallback;
            }

            return TrackDistance(origin, _rotation, end, fallback);
        }

        private static float TrackDistance(Vector3 start, Quaternion rotation, Transform end, float fallback)
        {
            if (end == null)
                return fallback;
            var distance = (Quaternion.Inverse(rotation) * (end.position - start)).z;
            return distance > 0.01f ? distance : fallback;
        }

        // The point's facing, flattened onto the ground.
        private static Quaternion FlatRotation(Transform t)
        {
            var forward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            return forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
        }

        // Player track space <-> world.
        private Vector3 ToTrack(Vector3 world) => Quaternion.Inverse(_rotation) * (world - origin);
        private Vector3 FromTrack(Vector3 local) => origin + _rotation * local;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private int _lastRampTexture = -1;

        // Index of a random ramp texture that isn't 'avoid' (when there's a choice), or -1 if there are none.
        private int PickRampTexture(int avoid)
        {
            var textures = database.RampTextures;
            if (textures == null || textures.Length == 0)
                return -1;

            var index = Random.Range(0, textures.Length);
            if (textures.Length > 1 && index == avoid)
                index = (index + Random.Range(1, textures.Length)) % textures.Length;
            return index;
        }

        private Texture2D TextureAt(int index)
        {
            var textures = database.RampTextures;
            return textures != null && index >= 0 && index < textures.Length ? textures[index] : null;
        }

        // Swaps the texture on this ramp's ground only (the material asset itself isn't changed).
        // Skips the finish line and any hurdle (anything with a TrackHurdle), and the start / end points
        // with everything under them, so they keep their own look.
        private static void ApplyRampTexture(Transform ramp, Texture2D texture, Transform startPoint, Transform endPoint)
        {
            if (texture == null)
                return;

            var block = new MaterialPropertyBlock();
            foreach (var renderer in ramp.GetComponentsInChildren<Renderer>(true))
            {
                var t = renderer.transform;
                if (renderer.GetComponentInParent<TrackHurdle>(true) != null
                    || (startPoint != null && t.IsChildOf(startPoint))
                    || (endPoint != null && t.IsChildOf(endPoint)))
                    continue;

                renderer.GetPropertyBlock(block);
                block.SetTexture(BaseMapId, texture); // URP
                block.SetTexture(MainTexId, texture); // Built-in / others
                renderer.SetPropertyBlock(block);
            }
        }

        // Bounds of every renderer under 'target', in player track space.
        private bool TryGetTrackBounds(Transform target, out Vector3 min, out Vector3 max)
        {
            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;
            var found = false;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer or TrailRenderer or LineRenderer)
                    continue;

                var b = renderer.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var local = ToTrack(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                    found = true;
                }
            }

            return found;
        }

        // ---------- AI ----------

        private AiProfile GetAiProfile(LevelData level)
        {
            if (!level.HasAiOpponent)
                return null;
            return level.AiProfile != null ? level.AiProfile : database.DefaultAiProfile;
        }

        // The AI is a copy of the player's doll (or the AI prefab) driven by AiInput instead of touch,
        // spawned at the scene's AI Start Point.
        private void SpawnAi(LevelData level)
        {
            var profile = GetAiProfile(level);
            var start = scene.AiStartPoint;
            if (profile == null || start == null)
                return;

            var prefab = database.AiPrefab != null ? database.AiPrefab
                : level.PlayerPrefab != null ? level.PlayerPrefab
                : database.DefaultPlayerPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"No doll for the AI. Set an AI Prefab or Default Player Prefab on the {nameof(LevelDatabase)}.", database);
                return;
            }

            var instance = Instantiate(prefab, start.position, FlatRotation(start));
            instance.name = "AI";
            _spawned.Add(instance);
            if (!instance.activeSelf)
                instance.SetActive(true);

            if (!instance.TryGetComponent(out ai))
            {
                Debug.LogError($"AI doll '{prefab.name}' has no {nameof(RunnerController)}.", prefab);
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
            MakeupOf(ai);
            ai.SetInput(aiInput, aiInput);
            ai.SetConfig(profile.MovementConfig);

            if (ai.DollOutfit != null)
                ai.DollOutfit.SetStartingItems(level.StartingOutfit);

            // The AI plans and steers in its start point's space (x = sideways, z = along its ramp).
            var trackWidth = player.Config != null ? player.Config.TrackHalfWidth * 2f : 4.4f;
            aiInput.Init(ai, start, profile, AiPlanner.Plan(aiHurdles, start, level.TargetLook, profile, trackWidth));
        }

        private void StartAi()
        {
            if (aiInput != null)
                aiInput.Go();
        }

        // Returns true when the hurdle has item gates (one choice).
        private static bool ConfigureGates(GameObject instance, OutfitItem[] items, IReadOnlyList<OutfitItem> targetItems, bool freeStyle)
        {
            var gates = instance.GetComponentsInChildren<ItemGate>();
            for (var i = 0; i < gates.Length; i++)
            {
                var itemOverride = items != null && i < items.Length ? items[i] : null;
                gates[i].Configure(itemOverride, targetItems, freeStyle);
            }
            return gates.Length > 0;
        }

        // Every paint gate learns the target look (green / red); the entry's settings, if any, replace the prefab's.
        private static void ConfigurePaintGates(GameObject instance, PaintGate.Setting[] settings, TargetLook look)
        {
            var gates = instance.GetComponentsInChildren<PaintGate>();
            for (var i = 0; i < gates.Length; i++)
            {
                if (settings != null && i < settings.Length)
                    gates[i].Configure(settings[i], look);
                else
                    gates[i].Configure(look);
            }
        }

        private static void ConfigureSkinGates(GameObject instance, SkinGate.Setting[] settings, LevelData level)
        {
            var gates = instance.GetComponentsInChildren<SkinGate>();
            for (var i = 0; i < gates.Length; i++)
            {
                SkinGate.Setting? setting = settings != null && i < settings.Length ? settings[i] : null;
                gates[i].Configure(setting, level.HasTargetSkin, level.TargetSkin);
            }
        }

        private static void ConfigureCoinGates(GameObject instance, CoinGate.Setting[] settings)
        {
            if (settings == null || settings.Length == 0)
                return;

            var gates = instance.GetComponentsInChildren<CoinGate>();
            for (var i = 0; i < gates.Length && i < settings.Length; i++)
                gates[i].Configure(settings[i]);
        }

        // ---------- Target look ----------

        // Level start: the demo doll shows the look to copy; the run can't start until the intro is over.
        // The doll is spawned at the scene's Demo Spawn Point; the scene's Demo Camera films it into the demo
        // render texture and the Gameplay UI shows it big, moves it to its corner and then raises TargetLookIntroDone.
        // Without a Demo Camera the doll just stands at its spawn point for Demo Show Time.
        private void ShowTargetLook(LevelData level)
        {
            waitingForTargetLook = false;
            demoCamera = scene.DemoCamera;

            var prefab = level.DemoPrefab != null ? level.DemoPrefab : database.DefaultDemoPrefab;
            var point = scene.DemoSpawnPoint;
            // Free style: the card shows her in her starting clothes, and the corner image follows what she picks (OnPlayerEquipped).
            if ((!level.HasTarget && !level.IsFreeStyle) || prefab == null || point == null)
            {
                if (level.HasTarget && prefab == null)
                    Debug.LogWarning($"'{level.name}' has a target look but no demo doll prefab (set Default Demo Prefab on the {nameof(LevelDatabase)}).", level);
                else if (level.HasTarget)
                    Debug.LogWarning($"'{level.name}' has a target look but {nameof(GameplayScene)} has no Demo Spawn Point.", scene);

                SetDemoViewActive(false);
                ReleaseRunners();
                return;
            }

            demo = Instantiate(prefab, point.position, point.rotation);
            demo.name = "TargetLook";
            _spawned.Add(demo);
            if (!demo.activeSelf)
                demo.SetActive(true);

            var demoOutfit = demo.GetComponentInChildren<DollOutfit>();
            if (demoOutfit != null)
            {
                demoOutfit.RaiseEvents = false;
                demoOutfit.SetStartingItems(level.IsFreeStyle ? level.StartingOutfit : level.TargetOutfit); // worn in its Start
            }
            else
                Debug.LogWarning($"Demo prefab '{prefab.name}' has no {nameof(DollOutfit)}, so it can't show the look.", prefab);

            // The demo's dress has the target dress colour.
            if (level.HasTargetDressColor && demoOutfit != null)
            {
                demoOutfit.SetStartingColor(OutfitSlot.Dress, level.TargetDressColor);
                demoOutfit.SetStartingColor(OutfitSlot.Top, level.TargetDressColor);
                demoOutfit.SetStartingColor(OutfitSlot.Bottom, level.TargetDressColor);
            }

            // The demo also has the target skin colour.
            if (level.HasTargetSkin)
            {
                var demoSkin = demo.GetComponentInChildren<DollSkin>();
                if (demoSkin == null)
                    demoSkin = demo.AddComponent<DollSkin>();
                demoSkin.SetStartColor(level.TargetSkin);
            }

            // The demo also wears the target makeup (painted in its Start).
            if (level.TargetLook != null && level.TargetLook.Makeup.Count > 0)
            {
                var demoMakeup = demo.GetComponentInChildren<DollMakeup>();
                if (demoMakeup != null)
                {
                    demoMakeup.RaiseEvents = false;
                    demoMakeup.SetStartingMakeup(level.TargetLook.Makeup);
                }
            }

            var onStage = demoCamera != null;
            if (onStage)
            {
                // The database's render texture wins, so the UI's demo image and the camera always use the same one.
                if (database.DemoRenderTexture != null)
                    demoCamera.targetTexture = database.DemoRenderTexture;
                else if (demoCamera.targetTexture == null)
                    Debug.LogWarning($"Demo Camera '{demoCamera.name}' has no Target Texture (and the {nameof(LevelDatabase)} has no Demo Render Texture), so it draws over the screen.", demoCamera);
            }
            SetDemoViewActive(onStage);

            player.StartLocked = true;
            waitingForTargetLook = true;
            GameEvents.GameplayEvents.TargetLookShown.Raise(level);

            // With a Demo Camera the Gameplay UI says when its intro is over (the player pressed PLAY), so no timer.
            if (!onStage)
                Invoke(nameof(HideTargetLook), database.DemoShowTime);
        }

        // Free style: the demo doll in the corner image copies every item the player picks.
        private void OnPlayerEquipped(OutfitItem item)
        {
            if (current == null || !current.IsFreeStyle || demo == null || item == null)
                return;
            var demoOutfit = demo.GetComponentInChildren<DollOutfit>();
            if (demoOutfit != null)
                demoOutfit.Equip(item);
        }

        private void OnTargetLookIntroDone()
        {
            if (!waitingForTargetLook)
                return;

            CancelInvoke(nameof(HideTargetLook));
            HideTargetLook();
        }

        // Demo camera + demo light on / off together.
        private void SetDemoViewActive(bool active)
        {
            if (scene == null)
                return;
            if (scene.DemoCamera != null)
                scene.DemoCamera.enabled = active;
        }

        // Bounds of what's actually drawn on the doll. Switched-off or hidden renderers are skipped: Unity reports
        // their bounds at the world origin, which would stretch the shot hundreds of metres and shrink her to a dot.
        // Anything odd (no renderers, or far too big for a doll) falls back to a doll-sized box at her feet.
        private const float MaxDollSize = 12f;

        private static Bounds GetVisibleBounds(GameObject doll)
        {
            var fallback = new Bounds(doll.transform.position + Vector3.up * 1.6f, Vector3.one * 3.3f);
            var found = false;
            var bounds = fallback;

            foreach (var r in doll.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer or TrailRenderer or LineRenderer)
                    continue;

                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-6f || (b.center - doll.transform.position).sqrMagnitude > MaxDollSize * MaxDollSize)
                    continue;

                if (!found)
                {
                    bounds = b;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            return found && bounds.size.magnitude <= MaxDollSize ? bounds : fallback;
        }

        private void HideTargetLook()
        {
            if (!waitingForTargetLook)
                return;
            waitingForTargetLook = false;

            // With a Demo Camera the doll keeps posing for the UI's corner image during the run.
            if (demo != null && demoCamera == null)
                demo.SetActive(false);

            ReleaseRunners();
            GameEvents.GameplayEvents.TargetLookHidden.Raise();
        }

        // Intro over: unlock the player (and start her right away when autoStartRun is on) and let the AI go.
        private void ReleaseRunners()
        {
            if (player != null)
            {
                player.StartLocked = false;
                if (autoStartRun)
                    player.BeginRun();
            }

            StartAi();
        }

        // ---------- Finish ----------

        // Gate order, used for the item-by-item comparison so it matches the order they were picked up.
        private static readonly OutfitSlot[] ComparisonOrder =
        {
            OutfitSlot.Dress, OutfitSlot.Bottom, OutfitSlot.Top, OutfitSlot.Hair, OutfitSlot.Shoes,
            OutfitSlot.Socks, OutfitSlot.Hat, OutfitSlot.Glasses, OutfitSlot.HandItem, OutfitSlot.Bag,
            OutfitSlot.Wings, OutfitSlot.Accessory, OutfitSlot.Makeup
        };

        // Longest the whole walk onto the stage may take before both dolls are put on their spots.
        private const float StageWalkTimeout = 12f;

        private Coroutine finishRoutine;
        private bool aiFinished;

        // Player at her finish line: stop her and run the stage sequence. The AI crossing its own finish line
        // just stops it; the sequence walks it to the stage.
        private void OnFinishReached(RunnerController runner)
        {
            if (runner != null && runner.IsAI)
            {
                aiFinished = true;
                if (runner.IsWalking == false)
                    runner.Stop();
                return;
            }

            if (finished || current == null)
                return;

            if (player == null)
            {
                Debug.LogError($"{nameof(LevelManager)}: no player assigned on ReferenceManager.", this);
                return;
            }

            finished = true;
            player.Stop(); // only her: the AI keeps running to its own finish line
            finishRoutine = StartCoroutine(FinishSequence());
        }

        // Finish line -> walk onto the stage -> item-by-item comparison -> win / lose animation -> result panel.
        // Touches are blocked the whole time.
        private IEnumerator FinishSequence()
        {
            GameEvents.GameplayEvents.InputBlocked.Raise(true);

            var hasAi = ai != null;

            // 1. Stage + walk.
            var playerArrived = false;
            var aiArrived = !hasAi;
            // The stage is either already shown (in LoadLevel) or shown now.
            if (!_stageShown)
            {
                _stageShown = true;
                _hasStage = TryShowStage(hasAi, out _stagePlayerSpot, out _stageAiSpot, out _stageFrontZ);
            }
            var hasStage = _hasStage;
            var playerSpot = _stagePlayerSpot;
            var aiSpot = _stageAiSpot;
            var frontZ = _stageFrontZ;
            var playerFacing = _stagePlayerFacing;
            var aiFacing = _stageAiFacing;

            // Long hand-made walk paths get extra time before the dolls are snapped onto their spots.
            var walkTimeout = StageWalkTimeout;
            if (hasStage && _stageLayout != null)
            {
                var longest = Mathf.Max(PathLength(_stageLayout.PlayerWaypoints), hasAi ? PathLength(_stageLayout.AiWaypoints) : 0f);
                walkTimeout += longest / Mathf.Max(0.1f, database.StageWalkSpeed);
            }

            if (hasStage)
            {
                player.WalkTo(StagePath(player.transform.position, playerSpot, frontZ, _stageLayout != null ? _stageLayout.PlayerWaypoints : null), database.StageWalkSpeed, playerFacing, () => playerArrived = true);
                FrameStage(playerSpot, hasAi ? aiSpot : playerSpot, playerFacing);
                if (scene != null && scene.CameraFollow != null)
                    scene.CameraFollow.FocusStageSpots(hasAi ? new[] { playerSpot, aiSpot } : new[] { playerSpot }, player.transform);
                WatchDolls(hasAi); // both from the start, so the AI on its own ramp stays in shot too

                // The scene's ending camera (EndingCam) shows the finish while the dolls walk to their spots.
                if (scene != null && scene.CameraFollow != null)
                    scene.CameraFollow.BeginEnding();
            }
            else
            {
                playerArrived = true;
            }

            var aiStarted = !hasAi;
            var waitedForAi = 0f;
            for (var elapsed = 0f; !(playerArrived && aiArrived); elapsed += Time.deltaTime)
            {
                if (!aiStarted)
                {
                    if (!hasStage)
                    {
                        aiStarted = aiArrived = true;
                    }
                    else if (aiFinished)
                    {
                        aiStarted = true;
                        ai.WalkTo(StagePath(ai.transform.position, aiSpot, frontZ, _stageLayout != null ? _stageLayout.AiWaypoints : null), database.StageWalkSpeed, aiFacing, () => aiArrived = true);
                        WatchDolls(true);
                    }
                    else if (playerArrived && (waitedForAi += Time.deltaTime) >= database.AiFinishWait)
                    {
                        // The AI is too far behind: put it on its spot.
                        aiStarted = aiArrived = true;
                        ai.PlaceAt(aiSpot, aiFacing);
                        WatchDolls(true);
                    }
                }

                if (elapsed > walkTimeout)
                {
                    if (!playerArrived)
                        player.PlaceAt(playerSpot, playerFacing);
                    if (hasAi && !aiArrived)
                        ai.PlaceAt(aiSpot, aiFacing);
                    break;
                }

                yield return null;
            }

            // Both on their spots: the camera eases into the exact stage view.
            if (hasStage && scene.CameraFollow != null)
            {
                scene.CameraFollow.SettleOnStage();
                scene.CameraFollow.EndEnding(); // ending camera off, stage view on at once
            }

            // 2. Item-by-item comparison.
            match = OutfitComparer.Compare(current.TargetLook, player.DollOutfit, MakeupOf(player));
            aiMatch = hasAi ? OutfitComparer.Compare(current.TargetLook, ai.DollOutfit, MakeupOf(ai)) : default;

            // The demo image goes away for the comparison: nothing left to film.
            SetDemoViewActive(false);

            GameEvents.GameplayEvents.ComparisonStarted.Raise(match, aiMatch);

            // Free style: no comparison (no labels over the dolls). She shows her final look on the stage, then the win animation.
            if (current.IsFreeStyle)
            {
                _freeStyleScore = FreeStyleScore(FreeStylePicks());
                yield return new WaitForSeconds(freeStyleShowTime);
            }
            else
            {
                GameEvents.GameplayEvents.StageComparisonStarted.Raise(player, hasAi ? ai : null);
            }

            if (current.HasTarget)
                yield return CompareItems(hasAi);

            yield return new WaitForSeconds(database.ResultDelay);

            // 3. Result: saves progress and fires LevelCompleted / LevelFailed, which starts the win / lose animations.
            var passed = DecideResult();

            var playerAnimator = player.Animator != null ? player.Animator : player.GetComponentInChildren<Game.Player.RunnerAnimator>();
            if (playerAnimator != null)
                yield return playerAnimator.WaitForResultAnimation(database.ResultAnimMaxTime);
            else
                yield return new WaitForSeconds(1f);

            // 4. Panel.
            finishRoutine = null;
            GameEvents.GameplayEvents.ComparisonEnded.Raise();
            GameEvents.GameplayEvents.InputBlocked.Raise(false);
            GameEvents.UIEvents.ShowByType.Raise(passed ? ScreenType.LevelCompletePanel : ScreenType.LevelFailedPanel);

            if (autoContinueDelay > 0f)
                Invoke(passed ? nameof(LoadNext) : nameof(RestartCurrent), autoContinueDelay);
        }

        // One target item at a time, ComparisonTime split evenly between them.
        private IEnumerator CompareItems(bool hasAi)
        {
            var targets = new List<OutfitItem>();
            if (current.TargetOutfit != null)
            {
                foreach (var item in current.TargetOutfit)
                {
                    if (item != null)
                        targets.Add(item);
                }
            }

            // Same order as the gates: the level's Gate Order first (if set), then the default order.
            var gateOrder = current.GateOrder;
            int Rank(OutfitSlot slot)
            {
                for (var k = 0; k < gateOrder.Count; k++)
                    if (gateOrder[k] == slot) return k;
                return gateOrder.Count + SlotRank(slot);
            }
            targets.Sort((a, b) => Rank(a.Slot).CompareTo(Rank(b.Slot)));
            if (targets.Count == 0)
                yield break;

            var interval = database.ComparisonTime / targets.Count;
            int playerMatched = 0, aiMatched = 0;

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var playerItem = WornIn(player, target.Slot);
                var aiItem = hasAi ? WornIn(ai, target.Slot) : null;
                var playerOk = playerItem == target;
                var aiOk = hasAi && aiItem == target;
                if (playerOk) playerMatched++;
                if (aiOk) aiMatched++;

                // Out of everything judged (items + makeup); the makeup part fills in step by step, so the last step shows the final %.
                var shareDone = (i + 1f) / targets.Count;
                GameEvents.GameplayEvents.ComparisonItemShown.Raise(new ComparisonStep(target, playerItem, aiItem, playerOk, aiOk,
                    RunningPercent(playerMatched, match, shareDone), hasAi ? RunningPercent(aiMatched, aiMatch, shareDone) : 0,
                    i, targets.Count, hasAi));

                yield return new WaitForSeconds(interval);
            }
        }

        // ---------- Free style ----------

        private int _freeStyleGates;   // item-gate choices on the player's ramp this level
        private int _freeStyleScore;   // Style %, worked out at the finish
        private readonly Dictionary<string, int> _freeStyleOutfitGates = new(); // outfit -> how many gate choices offer one of its items

        private void CountFreeStyleOutfits(GameObject instance)
        {
            var offered = new HashSet<string>();
            foreach (var gate in instance.GetComponentsInChildren<ItemGate>())
                if (gate.Item != null)
                    offered.Add(OutfitKey(gate.Item));
            foreach (var key in offered)
            {
                _freeStyleOutfitGates.TryGetValue(key, out var n);
                _freeStyleOutfitGates[key] = n + 1;
            }
        }

        // Items she wears now that she didn't start in (picked from gates), in gate order.
        private List<OutfitItem> FreeStylePicks()
        {
            var picks = new List<OutfitItem>();
            if (player == null || player.DollOutfit == null)
                return picks;

            var start = current.StartingOutfit;
            foreach (var item in player.DollOutfit.Worn.Values)
            {
                if (item == null)
                    continue;
                var wasStart = false;
                if (start != null)
                    foreach (var s in start)
                        if (s == item) { wasStart = true; break; }
                if (!wasStart)
                    picks.Add(item);
            }

            var gateOrder = current.GateOrder;
            int Rank(OutfitSlot slot)
            {
                for (var k = 0; k < gateOrder.Count; k++)
                    if (gateOrder[k] == slot) return k;
                return gateOrder.Count + SlotRank(slot);
            }
            picks.Sort((a, b) => Rank(a.Slot).CompareTo(Rank(b.Slot)));
            return picks;
        }

        // Style % = (items picked + same-outfit bonus) / best possible on this level.
        // Bonus: the biggest group of picked items from one outfit adds (group size - 1). Best possible = every gate picked and
        // the outfit offered on the most gates picked every time, so the most complete set scores 100%.
        private int FreeStyleScore(List<OutfitItem> picks)
        {
            var gates = Mathf.Max(_freeStyleGates, picks.Count);
            if (gates <= 0)
                return 100;

            var groups = new Dictionary<string, int>();
            var largest = 0;
            foreach (var item in picks)
            {
                var key = OutfitKey(item);
                groups.TryGetValue(key, out var n);
                groups[key] = ++n;
                largest = Mathf.Max(largest, n);
            }

            var points = picks.Count + Mathf.Max(0, largest - 1);
            var mostOffered = 0;
            foreach (var offeredCount in _freeStyleOutfitGates.Values)
                mostOffered = Mathf.Max(mostOffered, offeredCount);
            var best = gates + Mathf.Max(0, Mathf.Max(mostOffered, largest) - 1);
            return Mathf.Clamp(Mathf.RoundToInt(100f * points / best), 0, 100);
        }

        private string OutfitKey(OutfitItem item)
        {
            var catalog = database != null ? database.OutfitCatalog : null;
            var set = catalog != null ? catalog.FindSetOf(item) : null;
            if (set != null)
                return set.name;
            var name = item.name;
            var cut = name.IndexOf('_');
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        // Free style: seconds she stands on the stage showing her final look before the win animation.
        [SerializeField, Min(0f), Tooltip("Free style: seconds she shows her final look on the stage before the win animation.")]
        private float freeStyleShowTime = 1.2f;

        private static int RunningPercent(int itemsMatched, OutfitMatch final, float shareDone)
        {
            if (final.AllChecks <= 0)
                return 100;
            return Mathf.RoundToInt(100f * (itemsMatched + final.MakeupMatched * shareDone) / final.AllChecks);
        }

        // The doll's makeup (paint gates), added if the prefab has none so every doll can be painted and judged.
        private static DollMakeup MakeupOf(RunnerController runner)
        {
            if (runner == null)
                return null;
            var makeup = runner.GetComponentInChildren<DollMakeup>();
            if (makeup != null)
                makeup.RaiseEvents = !runner.IsAI;
            return makeup;
        }

        private static int SlotRank(OutfitSlot slot)
        {
            var index = System.Array.IndexOf(ComparisonOrder, slot);
            return index < 0 ? ComparisonOrder.Length + (int)slot : index;
        }

        private static OutfitItem WornIn(RunnerController runner, OutfitSlot slot) =>
            runner != null && runner.DollOutfit != null && runner.DollOutfit.Worn.TryGetValue(slot, out var item) ? item : null;

        // This level's stage, shown in LoadLevel (or at the finish) and used by FinishSequence.
        private bool _hasStage, _stageShown;
        private Vector3 _stagePlayerSpot, _stageAiSpot;
        private float _stageFrontZ;
        private Vector3 _stagePlayerFacing, _stageAiFacing;
        private FinishStage _stageLayout; // the shown stage's own spots / camera point (null = worked out)

        // Picks one of the scene's stages, shows it (hides the others) and works out where each doll stands on it.
        // The stage stays exactly where it's placed in the scene. Spots are world space; frontZ is the stage's
        // front edge in player track space.
        private bool TryShowStage(bool hasAi, out Vector3 playerSpot, out Vector3 aiSpot, out float frontZ)
        {
            playerSpot = aiSpot = player.transform.position;
            frontZ = _finishDistance;
            _stagePlayerFacing = _stageAiFacing = _rotation * Vector3.back; // back down the track, towards the camera
            _stageLayout = null;

            var stage = PickSceneStage();
            if (stage == null)
                return false;

            SetSceneStagesActive(stage);
            stage.TryGetComponent(out _stageLayout);

            if (!TryGetTrackBounds(stage.transform, out var min, out var max))
            {
                // No renderers: a 8 x 4 m area at the stage's position.
                var at = ToTrack(stage.transform.position);
                min = at + new Vector3(-4f, 0f, 0f);
                max = at + new Vector3(4f, 0f, 4f);
            }

            frontZ = min.z;
            var centreX = (min.x + max.x) * 0.5f;
            var top = max.y;
            var middleZ = (min.z + max.z) * 0.5f;
            var spacing = database.StageSpotSpacing > 0f ? database.StageSpotSpacing : (max.x - min.x) * 0.5f;

            // Fallback spots, used only where the stage's FinishStage has none.
            if (!hasAi)
            {
                playerSpot = FromTrack(new Vector3(centreX, top, middleZ));
                var shown = ApplyStageSpots(false, ref playerSpot, ref aiSpot);

                // Free style: she's alone on the stage, so she stands in the middle (between the stage's two spots).
                if (current != null && current.IsFreeStyle && _stageLayout != null && _stageLayout.PlayerSpot != null && _stageLayout.AiSpot != null)
                {
                    playerSpot = (_stageLayout.PlayerSpot.position + _stageLayout.AiSpot.position) * 0.5f;
                    var face = FlatForward(_stageLayout.PlayerSpot, _stagePlayerFacing) + FlatForward(_stageLayout.AiSpot, _stagePlayerFacing);
                    if (face.sqrMagnitude > 0.0001f)
                        _stagePlayerFacing = face.normalized;
                }
                return shown;
            }

            // Each doll takes the side nearer her own ramp.
            var aiX = scene.AiStartPoint != null ? ToTrack(scene.AiStartPoint.position).x : 1f;
            var playerSide = aiX >= 0f ? -1f : 1f;
            playerSpot = FromTrack(new Vector3(centreX + playerSide * spacing * 0.5f, top, middleZ));
            aiSpot = FromTrack(new Vector3(centreX - playerSide * spacing * 0.5f, top, middleZ));
            return ApplyStageSpots(true, ref playerSpot, ref aiSpot);
        }

        private int _lastSceneStageIndex = -1;

        // A random stage from GameplayScene → Scene Stages (empty entries skipped), or null.
        private GameObject PickSceneStage()
        {
            var stages = scene != null ? scene.SceneStages : null;
            if (stages == null)
                return null;

            var valid = new List<int>();
            for (var i = 0; i < stages.Count; i++)
                if (stages[i] != null)
                    valid.Add(i);
            if (valid.Count == 0)
                return null;

            if (database.StageAvoidRepeat && valid.Count > 1)
                valid.Remove(_lastSceneStageIndex);

            _lastSceneStageIndex = valid[Random.Range(0, valid.Count)];
            return stages[_lastSceneStageIndex];
        }

        // Shows 'shown' and hides every other scene stage (null = hide them all).
        private void SetSceneStagesActive(GameObject shown)
        {
            if (scene == null)
                return;

            foreach (var stage in scene.SceneStages)
                if (stage != null && stage.activeSelf != (stage == shown))
                    stage.SetActive(stage == shown);
        }

        // The stage's own FinishStage spots (if it has them) replace the worked-out ones; each doll faces her spot's forward.
        private bool ApplyStageSpots(bool hasAi, ref Vector3 playerSpot, ref Vector3 aiSpot)
        {
            if (_stageLayout == null)
            {
                Debug.LogWarning("The finish stage has no FinishStage component, so the dolls' spots on it are worked out. Add one with a PlayerSpot and AiSpot.", this);
                return true;
            }

            if (_stageLayout.PlayerSpot != null)
            {
                playerSpot = _stageLayout.PlayerSpot.position;
                _stagePlayerFacing = FlatForward(_stageLayout.PlayerSpot, _stagePlayerFacing);
            }

            if (hasAi && _stageLayout.AiSpot != null)
            {
                aiSpot = _stageLayout.AiSpot.position;
                _stageAiFacing = FlatForward(_stageLayout.AiSpot, _stageAiFacing);
            }

            return true;
        }

        private static Vector3 FlatForward(Transform t, Vector3 fallback)
        {
            var forward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : fallback;
        }

        // The stage's own waypoints (if it has any) then her spot. Otherwise: straight on along her own ramp to the
        // stage, up onto its front edge in line with her spot, then to the spot.
        private List<Vector3> StagePath(Vector3 from, Vector3 spot, float frontZ, IReadOnlyList<Transform> waypoints = null)
        {
            if (waypoints != null && waypoints.Count > 0)
            {
                var authored = new List<Vector3>();
                foreach (var point in waypoints)
                    if (point != null)
                        authored.Add(point.position);

                if (authored.Count > 0)
                {
                    authored.Add(spot);
                    return authored;
                }
            }

            var fromLocal = ToTrack(from);
            var spotLocal = ToTrack(spot);

            var path = new List<Vector3>();
            if (fromLocal.z < frontZ - 0.6f)
                path.Add(FromTrack(new Vector3(fromLocal.x, fromLocal.y, frontZ - 0.6f)));
            path.Add(FromTrack(new Vector3(spotLocal.x, spotLocal.y, Mathf.Min(frontZ + 0.6f, spotLocal.z))));
            path.Add(spot);
            return path;
        }

        private static float PathLength(IReadOnlyList<Transform> points)
        {
            if (points == null)
                return 0f;

            var length = 0f;
            Transform previous = null;
            foreach (var point in points)
            {
                if (point == null)
                    continue;
                if (previous != null)
                    length += Vector3.Distance(previous.position, point.position);
                previous = point;
            }
            return length;
        }

        // While the dolls walk to the stage the camera keeps them in frame (the AI once it's walking too).
        private void WatchDolls(bool withAi)
        {
            if (scene == null || scene.CameraFollow == null || player == null)
                return;

            if (withAi && ai != null)
                scene.CameraFollow.WatchGroup(player.transform, ai.transform);
            else
                scene.CameraFollow.WatchGroup(player.transform);
        }

        // Camera leaves the player and frames both spots on the stage (or goes to the stage's Camera Point).
        private void FrameStage(Vector3 playerSpot, Vector3 aiSpot, Vector3 facing)
        {
            if (scene == null || scene.CameraFollow == null)
                return;

            // The stage's own camera point, if it has one. Free style frames her alone, centred (the camera point is set up for two dolls).
            if (_stageLayout != null && _stageLayout.CameraPoint != null && (current == null || !current.IsFreeStyle))
            {
                scene.CameraFollow.ShowStagePose(_stageLayout.CameraPoint.position, _stageLayout.CameraPoint.rotation);
                return;
            }

            var dollHeight = Mathf.Clamp(GetVisibleBounds(player.gameObject).size.y, 1f, 6f);
            var centre = (playerSpot + aiSpot) * 0.5f;
            var halfWidth = Vector3.Distance(playerSpot, aiSpot) * 0.5f + dollHeight * 0.35f;
            scene.CameraFollow.ShowStage(centre + Vector3.up * dollHeight * 0.55f, facing, halfWidth, dollHeight * 0.6f);
        }

        // Works out pass / fail, saves progress and raises LevelCompleted / LevelFailed (result screens fill in,
        // dolls play win / lose). Doesn't open the panel. Returns whether she passed.
        private bool DecideResult()
        {
            var runScore = player.Score;
            var coins = runScore != null ? runScore.Coins : 0;

            // With a target look the result is how well she copied it (0..100); otherwise the coins gained this run.
            // Free style: the Style % (always passed, stars from the level's free style thresholds).
            if (current.IsFreeStyle && _freeStyleScore <= 0)
                _freeStyleScore = FreeStyleScore(FreeStylePicks());
            var score = current.IsFreeStyle ? _freeStyleScore : current.HasTarget ? match.Percent : coins;

            // The level pass mark applies in every race. Against AI, ties also fail.
            var hasAi = ai != null;
            var aiScore = hasAi ? GetAiScore() : 0;
            var passed = current.IsPass(score) && (!hasAi || score >= aiScore);

            if (passed)
            {
                var result = new LevelResult(current, CurrentLevelNumber, true, score, current.GetStars(score), coins, current.CompletionCoins, match, hasAi, aiScore, aiMatch);
                if (testLevelNumber > 0 || currentIndex >= LevelProgress.CurrentLevelIndex)
                {
                    // Picked-up coins were saved the moment they were collected; only the bonus is added here.
                    LevelProgress.AddCoins(result.CompletionCoins);
                    if (testLevelNumber == 0)
                        LevelProgress.CurrentLevelIndex = currentIndex + 1;
                }

                GameEvents.GameplayEvents.LevelCompleted.Raise(result);
            }
            else
            {
                var result = new LevelResult(current, CurrentLevelNumber, false, score, 0, coins, 0, match, hasAi, aiScore, aiMatch);
                GameEvents.GameplayEvents.LevelFailed.Raise(result);
            }

            return passed;
        }

        private int GetAiScore()
        {
            if (current.HasTarget)
                return aiMatch.Percent;
            return ai.Score != null ? ai.Score.Coins : 0;
        }

        // A retry / next level while the stage sequence is still running.
        private void StopFinishSequence()
        {
            aiFinished = false;
            if (finishRoutine == null)
                return;

            StopCoroutine(finishRoutine);
            finishRoutine = null;
            GameEvents.GameplayEvents.ComparisonEnded.Raise();
            GameEvents.GameplayEvents.InputBlocked.Raise(false);
        }

        [ContextMenu("Reset Saved Progress")]
        private void ResetProgress() => LevelProgress.ResetAll();
    }
}
