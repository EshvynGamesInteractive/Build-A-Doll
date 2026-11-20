# Build A Doll: Level design (Nov 2026, v5: level design rules)

Reference: "Build A Doll – Dress Up Run Race" (Fried Chicken Games). Run down a ramp, pick the right dress-up gate left or right, dodge hurdles, beat a rival doll on the next ramp by matching the target look better.

## Level design rules (19 Nov 2026, from the team)
1. **A hurdle follows the GREEN gate, on its lane, with a margin**: 12 m (2 s at speed 6; 15 m on tutorials) after the gate so she can steer / jump / slide, and 10 m of free track after it. Only lane-sized hurdles are used, so there's always room to go round.
2. **Cash stands in front of the RED gate side.** A CashBundle trail starts near the middle and leads to the red gate (temptation).
3. **Cash bundles make the player swipe left/right** (that trail is the swipe cue).
4. **Starting levels (1–3) teach the controls:** a hand on screen shows hold-and-drag, then points to the green gate before each choice.
5. **Levels 1–8 are eight different outfits.**
6. **After level 8, every 3–4 regular levels unlock a special outfit level** (Basketball, Baseball, Police, Nurse, Tennis). Level Complete shows the progress bar toward it. Princess / Cinderella style outfits are skipped (Princess, Cinderella, Snow White, Disney, Bella are out).
7. **Ring gates (hair, accessories) show a spinning 3D model** of the item; clothing gates show the 2D icon sprite.
8. **The target card says what to design**: each level's brief, e.g. "Design a NURSE outfit!" ("SPECIAL! …" on special levels).

### How the rules are built in
- **Rules 1–3 (importer, automatic):** every left/right `part` gate gets a fixed green side (stored on the entry as `LevelData.Entry.greenSide`, random per level but never 3× the same side, same result every import). The importer adds `lureCount` (4) `CashBundle`s from `lureSpan` (8 m, or gap − 1.5) before the gate to 2 m before it, sliding from 15% to 100% of the red gate's lateral, and one hurdle from the level's `guards` list (used in turn) `guardOffset` (12 m, per-level override; tutorials 15) after the gate on the green gate's lateral, then `guardClearance` (10 m) of free track before the next section (the cursor moves on by max(partLength, guardOffset + guardClearance − gap)). Lanes come from the pair prefab's ItemGate positions (RingPair ±2.25). Single (free) gates get neither. "Fill Gate Items From Outfit" keeps the stored side (Random = any side). Per level `lure: false` turns the cash trail off.
- Guards by tier: L1–3 Kit_Puddle · L4–8 Kit_SlimeTray / Kit_LowBlock · L9–17 Kit_LowBlock / Kit_Hurdle / Kit_ToyWall · L18–30 Kit_ToyWall / Kit_Hurdle / Kit_SpikeRail_Low / Kit_LowBlock (Kit_Puncher dropped: its arm sweeps too wide to go round).
- **Rule 4:** `LevelData.showSwipeGuide` (plan `tutorial`) + `SwipeGuide` component (`_UI/_Views/SwipeGuide.cs`) in the GameplayMenu prefab: hand Image + optional hint text, swipes both ways until the first touch, then toward the next green gate 24 → 3 m ahead while she's more than 0.9 m off its lane.
- **Rule 6:** `LevelData.isSpecial` / `specialTitle` / `specialIcon` (empty = the outfit's dress/top icon). `LevelDatabase.TryGetSpecialProgress(index)` counts the regular levels between the previous special (or `specialProgressFrom`, level 9) and the next one; nothing to save, the level order is the progress. LevelCompleteViewBehavior: `specialProgressRoot`, `specialProgressFill` (Image, Filled), `specialProgressText` ("Basketball Star 2/3" / "Basketball Star UNLOCKED!"), `specialIcon`, `specialUnlockedObject`; the bar fills when the panel opens. LevelMapView puts the special badge on special levels (Special Every is only the fallback).
- **Rule 7:** `ItemGate.previewStyle` Auto = 3D model when the gate has no preview anchor (rings), icon otherwise. The model is a static copy of the item prefab's meshes (bind pose), centred, `modelSize` 1.3, turning `modelSpinSpeed` 60°/s, bobbing like the icon. Falls back to the icon if the item has no meshes.
- **Rule 8:** `LevelData.brief` (plan `brief`) → GameplayMenuViewBehavior `briefText` (assign the card's "DRESS HER LIKE THIS!" title); empty brief = the title's own text.

### UI to wire in the prefabs (nothing is created from code)
- GameplayMenu: assign **Brief Text**; add a hand Image (+ optional hint TMP) and a **SwipeGuide** component pointing at them.
- LevelComplete: build the progress bar (root, Filled fill image, text, optional icon and "UNLOCKED!" badge) and assign the five fields.

## How to build the levels
1. **Build A Doll > Hurdles > Build Hurdle Kit Prefabs** (`_Core/_Editor/HurdleKitBuilder.cs`). Turns `_ART/_3D/_Hurdles/Hurdle1–26.fbx` into prefabs in `_Prefabs/_Hurdles/_Kit`. Re-runnable; prefabs are overwritten in place.
2. **Build A Doll > Levels > Import Level Plan (overwrite existing)**. Reads `_Core/_Data/_Levels/LevelPlan.json` (v5), writes the 30 LevelData assets, AI profiles and LevelDatabase order. v5 needs "overwrite existing", otherwise the old levels are kept. Warns if a level's finish is past `maxTrackLength` (250 m; the scene ramp is 270 m).

The v4 plan is saved next to it as `LevelPlan_v4_backup.json.txt`. Old scripts from before this change: project root `_Backup_2026-11-19_LevelRules/`. The plan was generated by a script that checks every outfit's parts (one gate per part, no duplicate slots), track length and that the new cash / hurdles don't overlap other items.

Don't run **Build A Doll > Create Sample Levels** / **Add Sample Hurdles** (LevelSetup.cs / ItemGateSetup.cs, early scaffolding): they recreate the deleted demo hurdles.

Starting outfit: every level starts in **Casual_V01**. No level targets a Casual variant.

## v5 structure
- **1–3 tutorial** (swipe guide; 1 = steer, 2 = jump, 3 = slide; level 1's first gate is free).
- **1–8 first outfits**, eight different themes, 3 → 5 parts.
- **After 8: 3 or 4 regular levels, then a special**: 9–11 → **12 Basketball**, 13–16 → **17 Baseball**, 18–20 → **21 Police**, 22–25 → **26 Nurse**, 27–29 → **30 Tennis**. Specials: AI duel, double completion coins, coin bonanza when it fits.
- AI on 1, 2, 5, 8, 10, 12, 15, 17, 19, 21, 24, 26, 28, 30 (Easy 1–8, Normal 10–15, Hard 17–28, Expert 30). Pass: tutorials allow 1 miss; AI levels and specials allow half the parts; solo levels allow 1.
- From level 4: one coin gate after the 2nd part (dropped where the track is too short). Moving gates: 1 from level 4, 2 from 12, 3 from 22 (clothes only).
- Hurdle chunks between parts by tier (4–8 walls/rails/spinner bar, 9–17 spinners/rollers/paddles/fans, 18–30 doors/pillars/pendulums/crushers/slime gauntlet). Gap 12 → 8. Long 5–6 part outfits drop every other between-chunk to fit 250 m; the guard hurdle after each green gate still keeps every part challenging.

## The 30 levels (T = tutorial, ★ = special; gate order shown)
| # | Name | Outfit | Brief | AI | Gates (* = moving) | Run |
|---|---|---|---|---|---|---|
| 1 T | First Steps | Date_V01 | Design a DATE NIGHT outfit! | Easy | Dress (free) > Hair > Shoes | ~210 m, ~35s |
| 2 T | Gym Class | Sport_2_V01 | Design a GYM outfit! | Easy | Bottom > Top > Shoes | ~191 m, ~32s |
| 3 T | Weekend Errands | Basics_V01 | Design a CASUAL outfit! | – | Bottom > Top > Hair | ~203 m, ~34s |
| 4 | Office Hours | Business_V01 | Design an OFFICE outfit! | – | Bottom > *Top > Hair > Shoes | ~247 m, ~41s |
| 5 | Tea Party | Cheongsam_V01 | Design a TEA PARTY outfit! | Easy | Dress > Hair > *Shoes | ~214 m, ~36s |
| 6 | First Day at School | Student_V01 | Design a SCHOOL outfit! | – | Bottom > *Top > Hair > Shoes | ~228 m, ~38s |
| 7 | Bunny Hop | Bunny_V01 | Design a COSTUME PARTY outfit! | – | Dress > Hair > *Shoes > Hat | ~243 m, ~40s |
| 8 | Beach Day | Bikini_V01 | Design a BEACH outfit! | Easy | Bottom > *Top > Hair > Shoes > Hat | ~237 m, ~40s |
| 9 | Cherry Blossom | Kimono_V01 | Design a KIMONO outfit! | – | Dress > Hair > *Shoes | ~208 m, ~35s |
| 10 | City Stroll | Citizen_V01 | Design a CITY outfit! | Normal | Bottom > *Top > Hair > Shoes | ~232 m, ~39s |
| 11 | Rainy Day | Rain_V03 | Design a RAINY DAY outfit! | – | Bottom > *Top > Hair > Shoes > HandItem | ~244 m, ~41s |
| 12 ★ | Basketball Star | Basketball_V01 | SPECIAL! Design a BASKETBALL outfit! | Normal | Bottom > *Top > Hair > *Shoes > Hat > HandItem | ~246 m, ~41s |
| 13 | Road Trip | Jumpsuit_V01 | Design a ROAD TRIP outfit! | – | Dress > Hair > *Shoes | ~198 m, ~33s |
| 14 | Class President | Uniform_V01 | Design a SCHOOL UNIFORM outfit! | – | Bottom > *Top > Hair > *Shoes > Glasses | ~246 m, ~41s |
| 15 | Hero Time | Heroine_V01 | Design a SUPERHERO outfit! | Normal | Dress > Hair > *Shoes > HandItem | ~234 m, ~39s |
| 16 | Neon City | Futuristic_V01 | Design a FUTURISTIC outfit! | – | Dress > Hair > *Shoes | ~212 m, ~35s |
| 17 ★ | Baseball Star | Baseball_V03 | SPECIAL! Design a BASEBALL outfit! | Hard | Bottom > *Top > Hair > *Shoes > Hat > HandItem | ~246 m, ~41s |
| 18 | Maid Cafe | Maid_V01 | Design a MAID CAFE outfit! | – | Dress > Hair > *Shoes > *Socks > Hat | ~240 m, ~40s |
| 19 | Game On | Squid_V01 | Design a GAME SHOW outfit! | Hard | Bottom > *Top > Hair > *Shoes | ~230 m, ~38s |
| 20 | Undercover | Killer_V01 | Design a SPY outfit! | – | Bottom > *Top > Hair > *Shoes > Glasses | ~240 m, ~40s |
| 21 ★ | Police Officer | Police_V01 | SPECIAL! Design a POLICE outfit! | Hard | Bottom > *Top > Hair > *Shoes > Hat | ~250 m, ~42s |
| 22 | Sleepover | Sleep_V03 | Design a PYJAMA PARTY outfit! | – | Dress > Hair > *Shoes | ~200 m, ~33s |
| 23 | Snow Day | Winter_V01 | Design a WINTER outfit! | – | Bottom > *Top > Hair > *Shoes > Hat | ~244 m, ~41s |
| 24 | Puppet Show | Puppet_V01 | Design a PUPPET SHOW outfit! | Hard | Bottom > *Top > Hair > *Shoes > *Socks | ~235 m, ~39s |
| 25 | Skate Park | Skate_V05 | Design a SKATER outfit! | – | Bottom > *Top > Hair > *Shoes > Hat > HandItem | ~245 m, ~41s |
| 26 ★ | Night Nurse | Nurse_V01 | SPECIAL! Design a NURSE outfit! | Hard | Dress > Hair > *Shoes > Hat | ~230 m, ~38s |
| 27 | Coffee Date | Date_V03 | Design a COFFEE DATE outfit! | – | Dress > Hair > *Shoes | ~190 m, ~32s |
| 28 | Sports Day | Sport_V01 | Design a SPORTS DAY outfit! | Hard | Bottom > *Top > Hair > *Shoes | ~238 m, ~40s |
| 29 | Jungle Explorer | Primitive_V01 | Design a JUNGLE outfit! | – | Bottom > *Top > Hair > Accessory | ~244 m, ~41s |
| 30 ★ | Tennis Champion | Tennis_V01 | SPECIAL! Design a TENNIS outfit! | Expert | Bottom > *Top > Hair > *Shoes > Hat > HandItem | ~240 m, ~40s |
## After the last level (18 Nov 2026)
`LevelDatabase.GetLevel(index)`: levels 1–30 in order; past 30, **a random level** from `loopFromLevel` (4) on (`randomAfterLast`, on). Shuffled like a deck per round, seeded by `randomSeed` and the level number, so the same level number always gives the same level. Special levels can come up in the random part too (the map badge follows them); the progress bar only shows for levels 9–29.

## Ramp
Levels use the **database ramp as before** (Walk Ramp, tiled). A pit-tile/bridge system from `_ART/_3D/_Ramps/Ramps.fbx` was tried on 17 Nov 2026 and **reverted at the user's request**. Ramps.fbx is only used for the finish Stage.prefab mesh.

## Gate order (17 Nov 2026)
Parts are offered in this order: **pants (or a dress, which replaces pants and shirt) → shirt → hair → shoes → socks → hat → glasses → hand item → bag → wings → accessory → makeup** (`OutfitLevelTools.SlotOrder`). Hair and accessories are always on a RingPair (the importer now picks RingPair itself for them), so `part:moving` only lands on pants, shirt, dress, shoes or socks.

Track half width is 4 (RunnerMovementConfig), so laterals go from −4 to 4; gate lanes are about ±2.25.

## Hurdle kit (scale: Hurdle16's rail = 8.9 wide; jump boxes capped at 1.1 high, slide boxes start at 0.95)
| Prefab | Model | What it does | Beat it by |
|---|---|---|---|
| Kit_Spinner_Cross / _Tri / _Bar | Hurdle1 / 2 / 3 | 4-, 3-, 2-arm spiked spinners (90–120°/s) | jump (or edge past the cross) |
| Kit_SweepArm_Long / _Short | Hurdle4 / 5 | one spiked arm sweeping round a post at the side | jump or other side |
| Kit_Paddles | Hurdle18 | spinning paddles | jump |
| Kit_Puncher | Hurdle23 | drum on an arm turning round a post | jump or other side |
| Kit_SpikeRoller | Hurdle6 | spiked roller rolling ±2.5 across | jump |
| Kit_SpikeRail_Low | Hurdle15 | ground rail, small spike sliding on it (half track) | jump or steer |
| Kit_SpikeRail_Wide | Hurdle16 | knee-high rail across the track + sliding spike | jump (forced) |
| Kit_Hurdle | Hurdle11 | classic low hurdle, 1.8 wide (used in rows of 4) | jump |
| Kit_LowBlock | Hurdle8 | low wedge block | jump or steer |
| Kit_SlimeTray / Kit_Puddle | Hurdle7 / 14 | slime puddles | steer |
| Kit_Cone / Kit_Bollard | Hurdle13 / 12 | small markers (scaled ×2 / ×1.6), used in rows | steer |
| Kit_BrickWall / Kit_ToyWall | Hurdle19 / 20 | 5.1- and 2.8-wide walls, 2.1 high | steer |
| Kit_Fan | Hurdle24 | big fan spinning facing the runner, half the track | steer |
| Kit_RollerGate | Hurdle25 | frame with two spinning spiked rollers | middle gap or outside |
| Kit_SlideGate | Hurdle26 | spiked roller across a frame at head height | slide, or outside |
| Kit_SlideBlock | Hurdle9 | block sliding along a full-width rail | timing |
| Kit_ClosingDoors / Kit_ClosingPillars | Hurdle10 / 17 | two blocks / spinning pillars closing in and opening | timing |
| Kit_Pendulum | Hurdle21 | spiked mace swinging ±50° across the track | timing |
| Kit_Crusher | Hurdle22 | spiked crusher going up and down 2.4 | pass while up |
All penalties: stumble + coin cost (1–3), no item loss. Cash: `CashBundle` (Collectable + bob), `Coins_Line`, `Coins_Wave`.

## Chunks (reusable patterns in LevelPlan.json)
coins_line(_l/_r), coins_wave, coin_bonanza · steer: cones_slalom, bollard_gate, puddles, slime_trays, slime_one, slime_gauntlet, brick_left/right/zigzag, toy_walls, fan_left/right, roller_gate · jump: hurdle_row, hurdle_double, low_blocks, spinner_cross/tri/bar, sweep_arms, paddles, puncher_pair, spike_roller, spike_rail_low, spike_rail_wide · slide: slide_gate · timing: slide_block, closing_doors, closing_pillars, pendulum_pair, crusher_row.

## Outfits to avoid
- **Princess / Cinderella style (rule, 19 Nov 2026):** Princess, Cinderella, Snow_White, Disney, Bella (Forest_Fairy also left out).
- **Two parts in the same slot (100% impossible):** Basketball_V02, Business_V02, Cheongsam_V02, Dress_V02, Jumpsuit_V02, Killer_V02, Kimono_V02, Maid_V02, Nurse_V02, Rain_V02, Rain_V04, Skate_V02–V04, Sleep_V02, Sport_V02, Sport_V04, Tennis_V02, Tomboy_V01/V03/V04, Uniform_V02, Waiter_V01–V04, Wedding_V01/V03/V04, Winter_V02, Winter_V04.
- Casual_V01/V02 (starting outfit), Default_V01, Wings_V01, Shared_Accessories_V01 (not full outfits).
- Spare valid outfits for more levels: Basics_V02/03, Basketball_V03, Bikini_V02/03, Bikini_2_V01, Bunny_V02/03, Business_V03, Cheongsam_V03, Date_V02/V04–V06, Dress_V01/V03, Jumpsuit_V03, Killer_V03, Maid_V03, Nurse_V03, Police_V02/03, Rain_V01, Sleep_V01, Sport_2_V02–V04, Sport_V03, Student_V02/03, Tennis_V03, Uniform_V03, Winter_V03.

## GDD mechanics not in the game yet (ideas for later levels)
Body-shape gates, colour paint pools / spray cans, fire + extinguisher, rewarded set pieces (pet ride, ramp flight, Fashion House), item-collection levels, make-up mini-step, surfing environment. See track-elements.md for the code that exists for these.

## Not checked yet
- Written without compiling or play-testing (19 Nov 2026). Import with "overwrite existing", then check in Play mode: cash trail reads as a lure, the 12 m reaction margin before the guard hurdle feels right (`guardOffset` in LevelPlan.json), 3D ring previews' size/facing (`modelSize`, `modelSpinSpeed`), hand guide timing.
- Guard hurdle sizes vs lanes (Kit_Hurdle is 1.8 wide, Kit_ToyWall 2.8, Kit_SpikeRail_Low half track) — tune in Play mode.
- Movers in one entry share timing (same phase).
