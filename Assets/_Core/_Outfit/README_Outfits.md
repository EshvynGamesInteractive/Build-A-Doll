# Outfit system

```
OutfitCatalog                 every outfit (wardrobe UI, progress, gate decoys)
└─ OutfitSet  "Police"        Shared Parts: hair, hat, pants, shoes
   ├─ OutfitVariant Police_V01   own parts: shirt 1      → wears shirt 1 + shared parts
   ├─ OutfitVariant Police_V02   own parts: shirt 2
   └─ OutfitVariant Police_V03   own parts: shirt 3
OutfitItem (part config)      slot, prefab, icon, style score, also-clears
└─ prefab                     the part's mesh + SkinnedPart (bone names), spawned on the doll at runtime
```

Slots: Hair, Shirt/Top, Pants/Skirt, Dress (full body, takes off Top + Bottom), Shoes, Socks,
Hat, Glasses, Hand Item, Bag, Wings, Accessory, Makeup. A variant's own part replaces a shared
part in the same slot.

## One-time setup (Unity menu)

1. **Build A Doll > Outfits > Preview Import (log only)**: prints every outfit / variant / part
   with its detected slot. Nothing is created. Parts marked `(?)` had no recognisable name.
2. **Build A Doll > Outfits > Import Outfits From Doll FBX**: creates
   - part prefabs in `_Prefabs/_Items/...` (`_Hairs`, `_Head_accessaries`, `_Glasses`,
     `_Hand_Items`, `_Angle_Wings`, `__Dresss/_<Outfit>`)
   - part configs (OutfitItem) in the same folders under `_Data/_Items/...`
   - `_Data/_Outfits/<Outfit>/Outfit_<Outfit>.asset` + `<Outfit>_V01.asset`...
   - `_Data/_Outfits/OutfitCatalog.asset`
   Re-run it whenever the FBX changes: GUIDs are kept, so levels and gates stay linked, and
   display names, icons and style scores you edited are kept.
3. **Build A Doll > Outfits > Set Up Doll As Player**: makes `_Prefabs/_Player/Doll_Base.prefab`
   (doll FBX without the Wardrobe) and puts it under `Visual` in `Player.prefab` and
   `DemoDoll.prefab`, wiring DollOutfit (skeleton + anchor) and RunnerAnimator.

4. **Build A Doll > Outfits > Render Item Icons (missing only)**: renders every part to a
   transparent PNG in `Assets/_ART/_2D/_ItemIcons/...` (same sub-folders as the configs),
   imports it as a Sprite and sets it as the item's **Icon**. Icons you set by hand are kept;
   use **(re-render all)** to redo everything, or right-click one item > **Render Icon**.

Gates show the item's Icon as a 2D sprite at the preview anchor of the red/green visual
(ItemGate: Icon Size, Icon Material, Icon Sorting Order). No 3D model is spawned on gates.

## Making a level (one outfit per level)

1. On the LevelData, set **Outfit to complete** to a variant (e.g. `Police_V02`).
   Leave Target Look empty: the level builds its target from the variant, using
   Outfit Pass Percent / Outfit Star Percents. (Or point a Target Look's **Variant** at it.)
2. Add gate entries along the track, one per part of the outfit. **Hair and accessories**
   (hair, hat, glasses, hand item, bag, accessory, wings) are picked from **RingPair** (floor
   rings, icon floating above); **clothes** (shirt, pants, dress, shoes, socks) from **GatePair**
   (or GatePair_Moving).
3. Right-click the LevelData header > **Fill Gate Items From Outfit**: switches each entry to
   RingPair or GatePair by the part's slot, and each one gets the next part (hair, dress/shirt, pants, shoes, hat, glasses, hand item...) and the other gate a
   decoy from another outfit for the same slot, on a random side.
4. Passing the level marks the variant completed (`OutfitProgress.IsCompleted(variant)`,
   event `GameplayEvents.OutfitCompleted`).

## Runtime

- Gates call `DollOutfit.Equip(item)`: the part's prefab is spawned and bound to the doll's
  bones by name (`SkinnedPart`), replacing whatever was in that slot.
- `DollOutfit.WearVariant(variant)` puts on a whole outfit; `IsWearing(variant)` checks it.
- In Play mode, right-click a variant asset > **Wear On Player (Play Mode)** to try it on.
