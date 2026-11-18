using UnityEngine;

namespace Game.Hurdles.Data
{
    /// <summary>
    /// An item for item-collection levels ("Collect These Kitchen Items"): a pan, a whisk, a cup...
    /// Track pickups (AddToCartEffect) put it in the shopping cart; LevelData lists the ones wanted.
    /// </summary>
    [CreateAssetMenu(fileName = "Collectible_", menuName = "Build A Doll/Level/Collectible Item")]
    public class CollectibleItemData : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("Shown in the cart and on the wanted list.")]
        [SerializeField] private Sprite icon;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Icon => icon;
    }
}
