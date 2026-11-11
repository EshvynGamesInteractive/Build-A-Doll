using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// One wearable item (a dress, a hairstyle, shoes...). Gates hand these to the doll.
    /// Make one asset per item via Create > Build A Doll > Outfit > Outfit Item.
    /// </summary>
    [CreateAssetMenu(fileName = "OutfitItem", menuName = "Build A Doll/Outfit/Outfit Item")]
    public class OutfitItem : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private OutfitSlot slot;

        [Tooltip("Spawned on the doll at this slot's anchor. A skinned mesh is bound to the doll's skeleton automatically.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Shown on the gate so the player can see what they're picking.")]
        [SerializeField] private Sprite icon;

        [Tooltip("Points towards the end-of-level rating. Use negative values for 'wrong' choices.")]
        [SerializeField] private int styleScore = 1;

        [Tooltip("Other slots this item takes off, e.g. a Dress takes off Top and Bottom.")]
        [SerializeField] private OutfitSlot[] alsoClears;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public OutfitSlot Slot => slot;
        public GameObject Prefab => prefab;
        public Sprite Icon => icon;
        public int StyleScore => styleScore;
        public OutfitSlot[] AlsoClears => alsoClears;
    }
}
