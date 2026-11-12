using UnityEngine;

namespace Core.UI
{
    public class TabGroup : MonoBehaviour
    {
        [SerializeField] private TabItem[] tabs;
        [SerializeField] private int defaultTabIndex;

        public TabItem[] Tabs => tabs;

        private int _selectedIndex = -1;

        private void Awake()
        {
            for (var i = 0; i < tabs.Length; i++)
            {
                var index = i;

                if (tabs[i] != null && tabs[i].Button != null)
                    tabs[i].Button.onClick.AddListener(() => SelectTab(index));
            }
        }

        private void Start()
        {
            SelectTab(defaultTabIndex);
        }

        public void SelectTab(int index)
        {
            if (index < 0 || index >= tabs.Length)
                return;

            if (_selectedIndex == index)
                return;

            _selectedIndex = index;

            for (var i = 0; i < tabs.Length; i++)
            {
                tabs[i].SetSelected(i == index);
            }
        }
    }
}
