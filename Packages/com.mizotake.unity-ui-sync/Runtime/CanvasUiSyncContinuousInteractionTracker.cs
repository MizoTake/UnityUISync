using UnityEngine;
using UnityEngine.EventSystems;

namespace Mizotake.UnityUiSync
{
    [DisallowMultipleComponent]
    internal sealed class CanvasUiSyncContinuousInteractionTracker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler, ICancelHandler
    {
        private CanvasUiSync owner;
        private CanvasUiSync.UiSyncBinding binding;
        private bool interacting;

        internal static CanvasUiSyncContinuousInteractionTracker GetOrAdd(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            return target.TryGetComponent<CanvasUiSyncContinuousInteractionTracker>(out var tracker) ? tracker : target.AddComponent<CanvasUiSyncContinuousInteractionTracker>();
        }

        internal void Configure(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding)
        {
            this.owner = owner;
            this.binding = binding;
        }

        internal void Clear(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding)
        {
            if (this.owner != owner || this.binding != binding)
            {
                return;
            }

            Cancel(owner, binding);
            this.owner = null;
            this.binding = null;
        }

        internal void Cancel(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding)
        {
            if (this.owner != owner || this.binding != binding)
            {
                return;
            }

            interacting = false;
            binding.IsInteracting = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            BeginInteraction();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EndInteraction();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            BeginInteraction();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EndInteraction();
        }

        public void OnCancel(BaseEventData eventData)
        {
            EndInteraction();
        }

        private void OnDisable()
        {
            EndInteraction();
        }

        private void OnDestroy()
        {
            owner = null;
            binding = null;
            interacting = false;
        }

        private void BeginInteraction()
        {
            if (interacting || owner == null || binding == null || !owner.CanProcessRuntimeEvents())
            {
                return;
            }

            interacting = true;
            owner.OnInteractionStarted(binding);
        }

        private void EndInteraction()
        {
            if (!interacting)
            {
                return;
            }

            interacting = false;
            if (owner == null || binding == null)
            {
                return;
            }

            if (!owner.CanProcessRuntimeEvents())
            {
                binding.IsInteracting = false;
                return;
            }

            owner.OnInteractionEnded(binding);
        }
    }
}
