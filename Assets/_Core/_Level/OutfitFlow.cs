using System;
using System.Collections.Generic;
using Game.Outfit;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Outfit flow (LevelDatabase → Use Outfit Flow, 23 Nov 2026):
    ///   Tutorial levels (first 'Tutorial Levels' of the list) → unlock loops of 'Levels Per Unlock' main levels.
    ///   At the end of a loop the next Outfit Reward is unlocked: the Level Complete panel offers Claim or Skip.
    ///   Claim → that outfit's free style level is played next; the look she designs is saved.
    ///   Skip  → the outfit is skipped for good: no free style level, no Gift Box, no "later" (24 Nov 2026).
    ///   A saved look comes back later as a random "Your Look" level (target = her own design), within limits:
    ///   at least Min Gap levels after its free style, forced by Max Gap, only on progress slot 1 or 2, max one per loop.
    ///   Your Look counts toward the unlock bar; free style and tutorial levels don't.
    /// The play-through index → level mapping is saved as a history, so retries and the level map stay consistent.
    /// Future (not yet played) indices are only predicted: queued free styles first, then the main levels.
    /// </summary>
    public static class OutfitFlow
    {
        private const string SaveKey = "outfit_flow_v1";

        public enum Kind { Main = 0, FreeStyle = 1, YourLook = 2 }

        [Serializable]
        private class Slot
        {
            public Kind kind;
            public int index; // main level index / reward index / design index
        }

        [Serializable]
        public class Design
        {
            public int reward;
            public int createdAt;      // play-through index of its free style level
            public List<string> items = new();
            public bool hasSkin;
            public Color skin = Color.white;
            public bool hasDressColor;
            public Color dressColor = Color.white;
            public bool played;
        }

        [Serializable]
        private class State
        {
            public List<Slot> history = new();
            public int mainNext;
            public int progress;            // levels counted in the current loop
            public int loop;                // loops completed
            public int nextReward;          // index of the next reward to unlock
            public bool yourLookThisLoop;
            public int pendingUnlock = -1;  // reward waiting for Claim / Skip
            public List<int> claimed = new();
            public List<int> giftBox = new(); // old saves only: emptied into 'skipped' on load
            public List<int> skipped = new();
            public List<int> inserts = new(); // free style levels to play next (reward indices)
            public List<Design> designs = new();
            // Bar shown on Level Complete for the level just passed.
            public int lastIndex = -1, lastDone, lastNeeded, lastReward = -1;
        }

        private static State _state;
        private static readonly Dictionary<int, LevelData> _yourLookCache = new();

        /// <summary>Raised when claims, the gift box or the pending unlock change.</summary>
        public static event Action Changed;

        private static State S
        {
            get
            {
                if (_state != null)
                    return _state;
                var json = PlayerPrefs.GetString(SaveKey, "");
                _state = string.IsNullOrEmpty(json) ? new State() : JsonUtility.FromJson<State>(json) ?? new State();
                _state.skipped ??= new List<int>();
                // No more "claim later": outfits left in the old Gift Box count as skipped.
                if (_state.giftBox != null && _state.giftBox.Count > 0)
                {
                    foreach (var reward in _state.giftBox)
                        if (!_state.skipped.Contains(reward) && !_state.claimed.Contains(reward))
                            _state.skipped.Add(reward);
                    _state.giftBox.Clear();
                }
                return _state;
            }
        }

        private static void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(S));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            _state = new State();
            _yourLookCache.Clear();
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // ---------- Queries ----------

        public static int PendingUnlock => S.pendingUnlock;
        public static IReadOnlyList<int> GiftBox => S.giftBox;
        public static IReadOnlyList<int> Claimed => S.claimed;
        public static bool IsClaimed(int reward) => S.claimed.Contains(reward);
        public static IReadOnlyList<int> Skipped => S.skipped;
        public static bool IsSkipped(int reward) => S.skipped.Contains(reward);

        /// <summary>What a play-through index is, if it has been played (or committed).</summary>
        public static bool TryGetSlot(int index, out Kind kind, out int slotIndex)
        {
            kind = Kind.Main;
            slotIndex = -1;
            if (index < 0 || index >= S.history.Count)
                return false;
            kind = S.history[index].kind;
            slotIndex = S.history[index].index;
            return true;
        }

        /// <summary>The level at a play-through index. 'commit' = it's about to be played: decide it and save.</summary>
        public static LevelData Resolve(LevelDatabase db, int index, bool commit)
        {
            index = Mathf.Max(0, index);
            var s = S;
            EnsureMigrated(db);

            if (commit)
            {
                var changed = false;
                while (s.history.Count <= index)
                {
                    s.history.Add(DecideNext(db));
                    changed = true;
                }
                if (changed)
                    Save();
            }

            if (index < s.history.Count)
                return Make(db, s.history[index]);

            // Prediction for the map: queued free styles, then main levels in order.
            var ahead = index - s.history.Count;
            if (ahead < s.inserts.Count)
                return Make(db, new Slot { kind = Kind.FreeStyle, index = s.inserts[ahead] });
            return db.GetMainLevel(s.mainNext + ahead - s.inserts.Count);
        }

        // Players who already had progress before the flow existed: their played levels count as main levels.
        private static void EnsureMigrated(LevelDatabase db)
        {
            var s = S;
            if (s.history.Count > 0 || s.mainNext > 0)
                return;
            var played = LevelProgress.CurrentLevelIndex;
            if (played <= 0)
                return;
            for (var i = 0; i < played; i++)
                s.history.Add(new Slot { kind = Kind.Main, index = i });
            s.mainNext = played;
            var counted = Mathf.Max(0, played - db.TutorialLevels);
            s.loop = counted / db.LevelsPerUnlock;
            s.progress = counted % db.LevelsPerUnlock;
            s.nextReward = Mathf.Min(s.loop, db.OutfitRewards.Count);
            Save();
        }

        private static Slot DecideNext(LevelDatabase db)
        {
            var s = S;
            if (s.inserts.Count > 0)
            {
                var reward = s.inserts[0];
                s.inserts.RemoveAt(0);
                return new Slot { kind = Kind.FreeStyle, index = reward };
            }

            if (TryPickYourLook(db, out var design))
            {
                s.designs[design].played = true;
                s.yourLookThisLoop = true;
                return new Slot { kind = Kind.YourLook, index = design };
            }

            return new Slot { kind = Kind.Main, index = s.mainNext++ };
        }

        private static bool TryPickYourLook(LevelDatabase db, out int design)
        {
            design = -1;
            var s = S;
            if (!db.YourLookEnabled || s.designs.Count == 0 || s.yourLookThisLoop)
                return false;
            if (s.mainNext < db.TutorialLevels)
                return false;
            var upcomingSlot = s.progress + 1;
            if (upcomingSlot > 2)
                return false; // slot 3 stays the handcrafted unlock level

            var at = s.history.Count;
            var rng = new System.Random(unchecked(db.FlowSeed * 397 ^ at * 7919));

            // Newest designs wait until Min Gap; the oldest unplayed one comes first.
            for (var i = 0; i < s.designs.Count; i++)
            {
                var d = s.designs[i];
                if (d.played)
                    continue;
                var gap = at - d.createdAt;
                if (gap < db.YourLookMinGap)
                    continue;
                if (gap >= db.YourLookMaxGap || rng.NextDouble() < db.YourLookChance)
                {
                    design = i;
                    return true;
                }
                return false; // missed the roll: try again next level
            }

            // Throwback: an already played design, now and then.
            var played = new List<int>();
            for (var i = 0; i < s.designs.Count; i++)
                if (s.designs[i].played)
                    played.Add(i);
            if (played.Count > 0 && rng.NextDouble() < db.YourLookThrowbackChance)
            {
                design = played[rng.Next(played.Count)];
                return true;
            }
            return false;
        }

        private static LevelData Make(LevelDatabase db, Slot slot)
        {
            switch (slot.kind)
            {
                case Kind.FreeStyle:
                    var reward = db.GetReward(slot.index);
                    if (reward != null && reward.freeStyleLevel != null)
                        return reward.freeStyleLevel;
                    Debug.LogWarning($"Outfit flow: reward {slot.index} has no free style level.", db);
                    return db.GetMainLevel(0);
                case Kind.YourLook:
                    return MakeYourLook(db, slot.index) ?? db.GetMainLevel(0);
                default:
                    return db.GetMainLevel(slot.index);
            }
        }

        private static LevelData MakeYourLook(LevelDatabase db, int designIndex)
        {
            if (_yourLookCache.TryGetValue(designIndex, out var cached) && cached != null)
                return cached;
            if (designIndex < 0 || designIndex >= S.designs.Count)
                return null;

            var d = S.designs[designIndex];
            var reward = db.GetReward(d.reward);
            if (reward == null || reward.freeStyleLevel == null)
                return null;

            var items = new List<OutfitItem>();
            foreach (var itemName in d.items)
            {
                var item = FindItem(db.OutfitCatalog, itemName);
                if (item != null)
                    items.Add(item);
            }
            if (items.Count == 0)
                return null;

            var ai = S.loop + 1 >= db.YourLookLateFromLoop && db.YourLookAiLate != null ? db.YourLookAiLate : db.YourLookAi;
            var coins = Mathf.RoundToInt(db.GetMainLevel(Mathf.Max(0, S.mainNext)).CompletionCoins * db.YourLookCoinFactor);
            var level = LevelData.CreateYourLook(reward.freeStyleLevel, items, d.hasSkin, d.skin, d.hasDressColor, d.dressColor,
                ai, coins, unchecked(db.FlowSeed + designIndex * 31));
            _yourLookCache[designIndex] = level;
            return level;
        }

        public static OutfitItem FindItem(OutfitCatalog catalog, string itemName)
        {
            if (catalog == null || string.IsNullOrEmpty(itemName))
                return null;
            foreach (var set in catalog.Outfits)
            {
                if (set == null)
                    continue;
                if (set.SharedParts != null)
                    foreach (var p in set.SharedParts)
                        if (p != null && p.name == itemName)
                            return p;
                if (set.Variants == null)
                    continue;
                foreach (var v in set.Variants)
                {
                    if (v == null || v.OwnParts == null)
                        continue;
                    foreach (var p in v.OwnParts)
                        if (p != null && p.name == itemName)
                            return p;
                }
            }
            return null;
        }

        // ---------- Results ----------

        /// <summary>Call once, the first time the level at 'index' is passed. Moves the unlock bar.</summary>
        public static void OnLevelPassed(LevelDatabase db, int index)
        {
            var s = S;
            s.lastIndex = -1;
            if (!TryGetSlot(index, out var kind, out var slotIndex))
                return;

            var counts = kind == Kind.YourLook || (kind == Kind.Main && slotIndex >= db.TutorialLevels);
            if (!counts)
            {
                Save();
                return;
            }

            s.progress++;
            s.lastIndex = index;
            s.lastDone = s.progress;
            s.lastNeeded = db.LevelsPerUnlock;
            s.lastReward = s.nextReward < db.OutfitRewards.Count ? s.nextReward : -1;

            if (s.progress >= db.LevelsPerUnlock)
            {
                s.progress = 0;
                s.loop++;
                s.yourLookThisLoop = false;
                if (s.nextReward < db.OutfitRewards.Count)
                {
                    s.pendingUnlock = s.nextReward;
                    s.nextReward++;
                }
            }
            Save();
            Changed?.Invoke();
        }

        /// <summary>Bar for the Level Complete panel of the level just passed.</summary>
        public static bool TryGetProgress(int index, out int reward, out int done, out int needed)
        {
            var s = S;
            reward = s.lastReward;
            done = s.lastDone;
            needed = s.lastNeeded;
            return s.lastIndex == index && reward >= 0 && needed > 0;
        }

        /// <summary>Free style finished: save the look she designed.</summary>
        public static void SaveDesign(int index, IReadOnlyList<OutfitItem> picks, bool hasSkin, Color skin, bool hasDressColor, Color dressColor)
        {
            if (!TryGetSlot(index, out var kind, out var reward) || kind != Kind.FreeStyle)
                return;
            var d = new Design { reward = reward, createdAt = index, hasSkin = hasSkin, skin = skin, hasDressColor = hasDressColor, dressColor = dressColor };
            foreach (var item in picks)
                if (item != null)
                    d.items.Add(item.name);
            if (d.items.Count == 0)
                return;
            S.designs.Add(d);
            Save();
        }

        public static void ClaimPending()
        {
            var s = S;
            if (s.pendingUnlock < 0)
                return;
            Claim(s.pendingUnlock);
            s.pendingUnlock = -1;
            Save();
            Changed?.Invoke();
        }

        public static void SkipPending()
        {
            var s = S;
            if (s.pendingUnlock < 0)
                return;
            // Skipped for good: no Gift Box, no free style level for it.
            if (!s.skipped.Contains(s.pendingUnlock) && !s.claimed.Contains(s.pendingUnlock))
                s.skipped.Add(s.pendingUnlock);
            s.pendingUnlock = -1;
            Save();
            Changed?.Invoke();
        }

        /// <summary>Old Gift Box claim. Unused since 24 Nov 2026 (Skip is final); kept so old references compile.</summary>
        public static bool ClaimFromGiftBox(int reward)
        {
            var s = S;
            if (!s.giftBox.Remove(reward))
                return false;
            Claim(reward);
            Save();
            Changed?.Invoke();
            return true;
        }

        private static void Claim(int reward)
        {
            var s = S;
            if (!s.claimed.Contains(reward))
                s.claimed.Add(reward);
            s.giftBox.Remove(reward);
            if (!s.inserts.Contains(reward))
                s.inserts.Add(reward);
        }
    }
}
