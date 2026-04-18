using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    [DisallowMultipleComponent]
    internal sealed class CanvasUiSyncDropdownRuntimeMarker : MonoBehaviour
    {
        [SerializeField] private CanvasUiSync owner;
        [SerializeField] private Dropdown uiDropdown;
        [SerializeField] private TMP_Dropdown tmpDropdown;

        internal static CanvasUiSyncDropdownRuntimeMarker GetOrAdd(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            return target.TryGetComponent<CanvasUiSyncDropdownRuntimeMarker>(out var marker) ? marker : target.AddComponent<CanvasUiSyncDropdownRuntimeMarker>();
        }

        internal void Configure(CanvasUiSync owner, Dropdown dropdown)
        {
            this.owner = owner;
            uiDropdown = dropdown;
            tmpDropdown = null;
        }

        internal void Configure(CanvasUiSync owner, TMP_Dropdown dropdown)
        {
            this.owner = owner;
            uiDropdown = null;
            tmpDropdown = dropdown;
        }

        internal bool Matches(CanvasUiSync owner, Dropdown dropdown)
        {
            return this.owner == owner && uiDropdown == dropdown && tmpDropdown == null;
        }

        internal bool Matches(CanvasUiSync owner, TMP_Dropdown dropdown)
        {
            return this.owner == owner && uiDropdown == null && tmpDropdown == dropdown;
        }
    }
}
