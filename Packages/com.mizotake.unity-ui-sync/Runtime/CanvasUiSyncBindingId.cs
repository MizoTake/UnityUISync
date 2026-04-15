using UnityEngine;

namespace Mizotake.UnityUiSync
{
    [DisallowMultipleComponent]
    public sealed class CanvasUiSyncBindingId : MonoBehaviour
    {
        [SerializeField] private string bindingId = string.Empty;

        public string BindingId
        {
            get => bindingId;
            set => bindingId = value;
        }
    }
}
