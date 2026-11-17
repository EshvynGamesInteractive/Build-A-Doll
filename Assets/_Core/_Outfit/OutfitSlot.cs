using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// Where on the doll an item goes. One item per slot at a time.
    /// Values are saved as numbers in assets, so only ever add new slots at the end.
    /// </summary>
    public enum OutfitSlot
    {
        Hair = 0,
        Makeup = 1,
        [InspectorName("Shirt / Top")] Top = 2,
        [InspectorName("Pants / Skirt")] Bottom = 3,
        [Tooltip("Full-body piece: takes off Top and Bottom.")] Dress = 4,
        Shoes = 5,
        Bag = 6,
        Accessory = 7,
        [InspectorName("Hat / Head Accessory")] Hat = 8,
        [InspectorName("Hand Item")] HandItem = 9,
        Glasses = 10,
        Wings = 11,
        Socks = 12
    }
}
