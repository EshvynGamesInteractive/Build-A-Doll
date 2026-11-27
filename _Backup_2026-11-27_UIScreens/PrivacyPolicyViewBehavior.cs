using _Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class PrivacyPolicyViewBehavior : UIScreen
    {
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button visitButton;

        protected override void Initialize()
        {
            base.Initialize();
        }
    }
}
