using _Core._Player;
using Game.Level;
using UnityEngine;

namespace _Core
{
    public class ReferenceManager : MonoBehaviour
    {
        public RunnerController player;
        public LevelManager levelManager;
        
        public static ReferenceManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Preserve the root parent across scenes
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}