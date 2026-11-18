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

        [Tooltip("On: the player starts running by herself as soon as the target-look intro ends (demo moved to its corner). Off: she waits for the first touch.")]
        [SerializeField] private bool autoStartRun = true;

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
        private Camera demoCamera;
        private bool waitingForTargetLook;
        private OutfitMatch match;

        // AI opponent on its own ramp next to the player's.
        private Transform aiRamp;
        private List<GameObject> aiHurdles = new();
        private RunnerController ai;
        private AiInput aiInput;
        private OutfitMatch aiMatch;

        // Where the demo stands at the start: this far down the track, facing the player.
        private const float DemoDistance = 4f;

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

            // Back to following her (the finish stage view left the camera on the stage).
            if (scene.CameraFollow != null)
                scene.CameraFollow.SetTarget(player.transform);
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

            // Player's ramp gets a random texture (not the same as last level's).
            var playerTexture = PickRampTexture(_lastRampTexture);
            _lastRampTexture = playerTexture;
            _rampTexture = TextureAt(playerTexture);

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

            // The AI's ramp gets a different texture from the player's.
            _rampTexture = TextureAt(PickRampTexture(playerTexture));
            aiHurdles = BuildRamp(level, aiRamp, true, false);
        }

        // Spawns one ramp's ground, hurdles and finish line under 'root'. Returns the hurdles in entry order.
        private List<GameObject> BuildRamp(LevelData level, Transform root, bool spawnGround, bool warnMissing)
        {
            if (spawnGround && database.RampPrefab != null)
                SpawnRamp(root, level.FinishDistance);

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
                ConfigureCoinGates(instance, entry.coinGates);
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

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private Texture2D _rampTexture;
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

        // Swaps the texture on this ramp only (the material asset itself isn't changed).
        private static void ApplyRampTexture(Transform ramp, Texture2D texture)
        {
            if (texture == null)
                return;

            var block = new MaterialPropertyBlock();
            foreach (var renderer in ramp.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                block.SetTexture(BaseMapId, texture); // URP
                block.SetTexture(MainTexId, texture); // Built-in / others
                renderer.SetPropertyBlock(block);
            }
        }

        // How far the ramp reaches behind the start line and past the finish line.
        private const float RampBehindStart = 20f;
        private const float RampPastFinish = 200f;

        // Any ramp model works: it's measured and made to cover start..finish, lined up with the start.
        // Tile repeats the model end to end at its real size, so its texture isn't stretched;
        // Stretch scales one copy along the track. Height (Y), width and rotation come from the prefab.
        private void SpawnRamp(Transform root, float finishDistance)
        {
            // The holder lines up with the track, so tiling/stretching is always along the run direction,
            // whatever way the model itself is rotated inside the prefab.
            var holder = new GameObject("Ramp").transform;
            holder.SetParent(root, false);
            var first = Instantiate(database.RampPrefab, holder, false);

            if (!TryGetLocalBounds(holder, root, out var min, out var max) || max.z - min.z <= 0.001f)
            {
                ApplyRampTexture(holder, _rampTexture);
                return;
            }

            var length = max.z - min.z;
            var wanted = finishDistance + RampBehindStart + RampPastFinish;

            if (database.RampFitMode == RampFit.Stretch)
            {
                holder.localScale = new Vector3(1f, 1f, wanted / length);
            }
            else
            {
                // Each copy starts where the last one ended (minus a hair of overlap to hide seams).
                var step = Mathf.Max(0.01f, length - database.RampTileOverlap);
                var count = Mathf.CeilToInt((wanted - length) / step) + 1;
                for (var i = 1; i < count; i++)
                {
                    var copy = Instantiate(database.RampPrefab, holder, false);
                    copy.name = $"{first.name} ({i})";
                    copy.transform.localPosition = first.transform.localPosition + new Vector3(0f, 0f, step * i);
                }
            }

            ApplyRampTexture(holder, _rampTexture);

            if (TryGetLocalBounds(holder, root, out min, out max))
                holder.localPosition += new Vector3(0f, 0f, -RampBehindStart - min.z);
        }


        // Bounds of every renderer under 'target', in 'space' local coordinates.
        private static bool TryGetLocalBounds(Transform target, Transform space, out Vector3 min, out Vector3 max)
        {
            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;
            var found = false;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                var b = renderer.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var local = space.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                    found = true;
                }
            }

            return found;
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
        // With a demo render texture the doll stands on its own far from the track, filmed by a demo camera into
        // the texture; the Gameplay UI shows it big, moves it to its corner and then raises TargetLookIntroDone.
        // Without one, the old way: the doll stands on the track in front of the player for Demo Show Time.
        private void ShowTargetLook(LevelData level)
        {
            demo = null; // the old ones went with the previous level root
            demoCamera = null;
            waitingForTargetLook = false;

            var prefab = level.DemoPrefab != null ? level.DemoPrefab : database.DefaultDemoPrefab;
            if (!level.HasTarget || prefab == null)
            {
                if (level.HasTarget)
                    Debug.LogWarning($"'{level.name}' has a target look but no demo doll prefab (set Default Demo Prefab on the {nameof(LevelDatabase)}).", level);

                ReleaseRunners();
                return;
            }

            var onStage = database.DemoRenderTexture != null;
            var point = scene.DemoPoint;
            Vector3 position;
            Quaternion rotation;
            if (point != null)
            {
                position = point.position;
                rotation = point.rotation;
            }
            else if (onStage)
            {
                position = levelRoot.TransformPoint(database.DemoStageOffset);
                rotation = database.DemoFaceSun ? FaceTheSun(_rotation) : _rotation;
            }
            else
            {
                position = levelRoot.TransformPoint(new Vector3(0f, 0f, DemoDistance));
                rotation = _rotation * Quaternion.Euler(0f, 180f, 0f);
            }

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

            if (onStage)
                demoCamera = CreateDemoCamera(demo, database.DemoRenderTexture);

            player.StartLocked = true;
            waitingForTargetLook = true;
            GameEvents.GameplayEvents.TargetLookShown.Raise(level);

            // On stage the Gameplay UI says when its intro is over (the player pressed PLAY), so no timer.
            // Without the render texture the demo stands on the track for Demo Show Time.
            if (!onStage)
                Invoke(nameof(HideTargetLook), database.DemoShowTime);
        }

        private void OnTargetLookIntroDone()
        {
            if (!waitingForTargetLook)
                return;

            CancelInvoke(nameof(HideTargetLook));
            HideTargetLook();
        }

        // A camera in front of the demo doll that films it (whole body, transparent background) into the render texture.
        // Short far plane, so nothing else is in the shot. Framed now, and again a frame later once the
        // doll has put her outfit on (DollOutfit wears it in Start) and her animator has posed her.
        private Camera CreateDemoCamera(GameObject doll, RenderTexture texture)
        {
            var go = new GameObject("DemoCamera");
            go.transform.SetParent(levelRoot, true);

            var cam = go.AddComponent<Camera>();
            cam.targetTexture = texture;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = database.DemoCameraFov;
            cam.nearClipPlane = 0.1f;
            cam.depth = -10f;

            // Its own light, so she's lit from the camera's side whatever way the sun points.
            // The stage is far from the track, so it lights nothing else.
            if (database.DemoLightIntensity > 0f)
            {
                var lightGo = new GameObject("DemoLight");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = new Vector3(0.6f, 1.2f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
                light.color = Color.white;
            }

            FrameDemoCamera(cam, doll, texture);
            StartCoroutine(ReframeDemoCamera(cam, doll, texture));
            return cam;
        }

        private System.Collections.IEnumerator ReframeDemoCamera(Camera cam, GameObject doll, RenderTexture texture)
        {
            yield return null;
            yield return null;
            if (cam != null && doll != null && doll.activeInHierarchy && cam.enabled)
                FrameDemoCamera(cam, doll, texture);
        }

        private void FrameDemoCamera(Camera cam, GameObject doll, RenderTexture texture)
        {
            var bounds = GetVisibleBounds(doll);

            var aspect = texture.height > 0 ? (float)texture.width / texture.height : 1f;
            var halfTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var fitHeight = bounds.extents.y * 1.1f / halfTan;
            var fitWidth = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.1f / (halfTan * aspect);
            var distance = Mathf.Max(fitHeight, fitWidth) + bounds.extents.z;

            var forward = Vector3.ProjectOnPlane(doll.transform.forward, Vector3.up).normalized;
            if (forward == Vector3.zero)
                forward = Vector3.forward;

            cam.transform.position = bounds.center + forward * distance;
            cam.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            cam.farClipPlane = distance + bounds.size.magnitude + 1f;

            // URP point lights fade with distance squared, so scale by it to get the same brightness however far the camera is.
            var light = cam.GetComponentInChildren<Light>();
            if (light != null)
            {
                var toDoll = Vector3.Distance(light.transform.position, bounds.center);
                light.range = toDoll * 2.5f + bounds.size.magnitude;
                light.intensity = DemoLightBoost * toDoll * toDoll;
            }
        }

        private float DemoLightBoost => database != null ? database.DemoLightIntensity : 1f;

        // Turns 'rotation' to face the scene's sun (RenderSettings.sun, else the first directional light).
        private static Quaternion FaceTheSun(Quaternion rotation)
        {
            var sun = RenderSettings.sun;
            if (sun == null || sun.type != LightType.Directional)
            {
                sun = null;
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional && l.isActiveAndEnabled)
                    {
                        sun = l;
                        break;
                    }
                }
            }

            if (sun == null)
                return rotation;

            var toSun = Vector3.ProjectOnPlane(-sun.transform.forward, Vector3.up);
            return toSun.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toSun.normalized, Vector3.up) : rotation;
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

            // On stage the doll keeps posing for the UI's corner image during the run.
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
            var facing = _rotation * Vector3.back; // on the stage they face back down the track, towards the camera

            // 1. Stage + walk.
            var playerArrived = false;
            var aiArrived = !hasAi;
            var hasStage = TrySpawnStage(hasAi, out var playerSpot, out var aiSpot, out var frontZ);

            if (hasStage)
            {
                player.WalkTo(StagePath(player.transform.position, playerSpot, frontZ), database.StageWalkSpeed, facing, () => playerArrived = true);
                FrameStage(playerSpot, hasAi ? aiSpot : playerSpot, facing);
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
                        ai.WalkTo(StagePath(ai.transform.position, aiSpot, frontZ), database.StageWalkSpeed, facing, () => aiArrived = true);
                    }
                    else if (playerArrived && (waitedForAi += Time.deltaTime) >= database.AiFinishWait)
                    {
                        // The AI is too far behind: put it on its spot.
                        aiStarted = aiArrived = true;
                        ai.PlaceAt(aiSpot, facing);
                    }
                }

                if (elapsed > StageWalkTimeout)
                {
                    if (!playerArrived)
                        player.PlaceAt(playerSpot, facing);
                    if (hasAi && !aiArrived)
                        ai.PlaceAt(aiSpot, facing);
                    break;
                }

                yield return null;
            }

            // 2. Item-by-item comparison.
            match = OutfitComparer.Compare(current.TargetOutfit, player.DollOutfit);
            aiMatch = hasAi ? OutfitComparer.Compare(current.TargetOutfit, ai.DollOutfit) : default;

            // The demo image goes away for the comparison: nothing left to film.
            if (demoCamera != null)
                demoCamera.enabled = false;

            GameEvents.GameplayEvents.ComparisonStarted.Raise(match, aiMatch);
            GameEvents.GameplayEvents.StageComparisonStarted.Raise(player, hasAi ? ai : null);

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
            foreach (var item in current.TargetOutfit)
            {
                if (item != null)
                    targets.Add(item);
            }

            targets.Sort((a, b) => SlotRank(a.Slot).CompareTo(SlotRank(b.Slot)));
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

                GameEvents.GameplayEvents.ComparisonItemShown.Raise(new ComparisonStep(target, playerItem, aiItem, playerOk, aiOk,
                    Mathf.RoundToInt(100f * playerMatched / targets.Count), Mathf.RoundToInt(100f * aiMatched / targets.Count),
                    i, targets.Count, hasAi));

                yield return new WaitForSeconds(interval);
            }
        }

        private static int SlotRank(OutfitSlot slot)
        {
            var index = System.Array.IndexOf(ComparisonOrder, slot);
            return index < 0 ? ComparisonOrder.Length + (int)slot : index;
        }

        private static OutfitItem WornIn(RunnerController runner, OutfitSlot slot) =>
            runner != null && runner.DollOutfit != null && runner.DollOutfit.Worn.TryGetValue(slot, out var item) ? item : null;

        // Spawns the stage past the finish line (centred between the ramps, or on the player's) and works out
        // where each doll stands on it. All positions are world space; frontZ is the stage's front edge in level space.
        private bool TrySpawnStage(bool hasAi, out Vector3 playerSpot, out Vector3 aiSpot, out float frontZ)
        {
            playerSpot = aiSpot = player.transform.position;
            frontZ = current.FinishDistance;

            var prefab = database.StagePrefab;
            if (prefab == null)
                return false;

            var aiX = hasAi && aiRamp != null ? aiRamp.localPosition.x : 0f;
            var centreX = hasAi && database.StageBetweenRamps ? aiX * 0.5f : 0f;
            var front = current.FinishDistance + database.StageGap;

            // Child of the level root, so it goes with the level.
            var stage = Instantiate(prefab, levelRoot.TransformPoint(new Vector3(centreX, 0f, front)), _rotation, levelRoot);
            stage.name = "FinishStage";
            if (!stage.activeSelf)
                stage.SetActive(true);

            // Resize to Stage Size (width x depth), then line it up: centred on centreX, front edge on 'front', whatever its pivot.
            if (TryGetLocalBounds(stage.transform, levelRoot, out var min, out var max))
            {
                var size = database.StageSize;
                var scale = stage.transform.localScale;
                if (size.x > 0f && max.x - min.x > 0.01f)
                    scale.x *= size.x / (max.x - min.x);
                if (size.y > 0f && max.z - min.z > 0.01f)
                    scale.z *= size.y / (max.z - min.z);
                stage.transform.localScale = scale;
                TryGetLocalBounds(stage.transform, levelRoot, out min, out max);

                stage.transform.position += levelRoot.TransformVector(new Vector3(centreX - (min.x + max.x) * 0.5f, 0f, front - min.z));
                TryGetLocalBounds(stage.transform, levelRoot, out min, out max);
            }
            else
            {
                min = new Vector3(centreX - 4f, 0f, front);
                max = new Vector3(centreX + 4f, 0f, front + 4f);
            }

            frontZ = min.z;
            var top = max.y;
            var middleZ = (min.z + max.z) * 0.5f;
            var spacing = database.StageSpotSpacing > 0f ? database.StageSpotSpacing : (max.x - min.x) * 0.5f;

            if (!hasAi)
            {
                playerSpot = levelRoot.TransformPoint(new Vector3(centreX, top, middleZ));
                return true;
            }

            // Each doll takes the side nearer her own ramp.
            var playerSide = aiX >= 0f ? -1f : 1f;
            playerSpot = levelRoot.TransformPoint(new Vector3(centreX + playerSide * spacing * 0.5f, top, middleZ));
            aiSpot = levelRoot.TransformPoint(new Vector3(centreX - playerSide * spacing * 0.5f, top, middleZ));
            return true;
        }

        // Straight on along her own ramp to the stage, up onto its front edge in line with her spot, then to the spot.
        private List<Vector3> StagePath(Vector3 from, Vector3 spot, float frontZ)
        {
            var fromLocal = levelRoot.InverseTransformPoint(from);
            var spotLocal = levelRoot.InverseTransformPoint(spot);

            var path = new List<Vector3>();
            if (fromLocal.z < frontZ - 0.6f)
                path.Add(levelRoot.TransformPoint(new Vector3(fromLocal.x, fromLocal.y, frontZ - 0.6f)));
            path.Add(levelRoot.TransformPoint(new Vector3(spotLocal.x, spotLocal.y, Mathf.Min(frontZ + 0.6f, spotLocal.z))));
            path.Add(spot);
            return path;
        }

        // Camera leaves the player and frames both spots on the stage.
        private void FrameStage(Vector3 playerSpot, Vector3 aiSpot, Vector3 facing)
        {
            if (scene == null || scene.CameraFollow == null)
                return;

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
            var score = current.HasTarget ? match.Percent : coins;

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
