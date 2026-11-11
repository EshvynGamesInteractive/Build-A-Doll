using UnityEngine;

namespace Core.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private UIScreen[] screens;
        
    }
}
