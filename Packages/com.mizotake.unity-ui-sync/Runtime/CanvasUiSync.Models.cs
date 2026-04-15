using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    public sealed class CanvasUiSyncSamplePresenter : MonoBehaviour
    {
        public Toggle powerToggle;
        public Image powerToggleBackground;
        public Image powerToggleCheckmark;
        public Image powerLamp;
        public Text powerValueText;
        public Slider masterSlider;
        public Image sliderFill;
        public Text sliderValueText;
        public Scrollbar intensityScrollbar;
        public Image scrollbarFill;
        public Text scrollbarValueText;
        public Dropdown modeDropdown;
        public Text modeValueText;
        public InputField operatorInput;
        public Text operatorValueText;
        public Toggle targetToggle;
        public Image targetToggleBackground;
        public Image targetToggleCheckmark;
        public Image targetLamp;
        public Text targetValueText;
        public Button syncButton;
        public Image buttonPulse;
        public Text buttonValueText;
        private bool hasCachedState;
        private bool lastPowerState;
        private float lastSliderValue;
        private float lastScrollbarValue;
        private int lastDropdownValue = -1;
        private string lastOperatorText = string.Empty;
        private bool lastTargetState;

        private void Awake()
        {
            Refresh();
        }

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void LateUpdate()
        {
            SyncStateViews();
        }

        private void Bind()
        {
            if (powerToggle != null)
            {
                powerToggle.onValueChanged.AddListener(OnPowerToggleChanged);
            }

            if (masterSlider != null)
            {
                masterSlider.onValueChanged.AddListener(OnMasterSliderChanged);
            }

            if (intensityScrollbar != null)
            {
                intensityScrollbar.onValueChanged.AddListener(OnIntensityScrollbarChanged);
            }

            if (modeDropdown != null)
            {
                modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
            }

            if (operatorInput != null)
            {
                operatorInput.onValueChanged.AddListener(OnOperatorInputChanged);
                operatorInput.onEndEdit.AddListener(OnOperatorInputChanged);
            }

            if (targetToggle != null)
            {
                targetToggle.onValueChanged.AddListener(OnTargetToggleChanged);
            }

            if (syncButton != null)
            {
                syncButton.onClick.AddListener(OnSyncButtonClicked);
            }
        }

        private void Unbind()
        {
            if (powerToggle != null)
            {
                powerToggle.onValueChanged.RemoveListener(OnPowerToggleChanged);
            }

            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
            }

            if (intensityScrollbar != null)
            {
                intensityScrollbar.onValueChanged.RemoveListener(OnIntensityScrollbarChanged);
            }

            if (modeDropdown != null)
            {
                modeDropdown.onValueChanged.RemoveListener(OnModeDropdownChanged);
            }

            if (operatorInput != null)
            {
                operatorInput.onValueChanged.RemoveListener(OnOperatorInputChanged);
                operatorInput.onEndEdit.RemoveListener(OnOperatorInputChanged);
            }

            if (targetToggle != null)
            {
                targetToggle.onValueChanged.RemoveListener(OnTargetToggleChanged);
            }

            if (syncButton != null)
            {
                syncButton.onClick.RemoveListener(OnSyncButtonClicked);
            }
        }

        private void OnPowerToggleChanged(bool value)
        {
            ApplyToggleVisual(powerToggleBackground, powerToggleCheckmark, value, new Color(0.22f, 0.78f, 0.36f), new Color(0.17f, 0.2f, 0.24f));
            ApplyLamp(powerLamp, value, new Color(0.22f, 0.78f, 0.36f), new Color(0.21f, 0.24f, 0.28f));
            ApplyText(powerValueText, value ? "ON" : "OFF");
        }

        private void OnMasterSliderChanged(float value)
        {
            ApplyFill(sliderFill, value, new Color(0.15f, 0.65f, 0.95f), new Color(0.16f, 0.2f, 0.28f));
            ApplyText(sliderValueText, value.ToString("0.00"));
        }

        private void OnIntensityScrollbarChanged(float value)
        {
            ApplyFill(scrollbarFill, value, new Color(1f, 0.68f, 0.2f), new Color(0.22f, 0.18f, 0.13f));
            ApplyText(scrollbarValueText, value.ToString("0.00"));
        }

        private void OnModeDropdownChanged(int value)
        {
            if (modeDropdown == null)
            {
                return;
            }

            var label = value >= 0 && value < modeDropdown.options.Count ? modeDropdown.options[value].text : value.ToString();
            ApplyText(modeValueText, label);
        }

        private void OnOperatorInputChanged(string value)
        {
            ApplyText(operatorValueText, string.IsNullOrWhiteSpace(value) ? "(empty)" : value);
        }

        private void OnTargetToggleChanged(bool value)
        {
            ApplyToggleVisual(targetToggleBackground, targetToggleCheckmark, value, new Color(0.98f, 0.38f, 0.24f), new Color(0.24f, 0.2f, 0.2f));
            ApplyLamp(targetLamp, value, new Color(0.98f, 0.38f, 0.24f), new Color(0.24f, 0.2f, 0.2f));
            ApplyText(targetValueText, value ? "ARMED" : "IDLE");
        }

        private void OnSyncButtonClicked()
        {
            if (buttonPulse != null)
            {
                buttonPulse.color = new Color(1f, 0.93f, 0.35f, 1f);
            }

            ApplyText(buttonValueText, "TRIGGERED");
        }

        public void Refresh()
        {
            hasCachedState = false;
            SyncStateViews();
            if (buttonPulse != null)
            {
                buttonPulse.color = new Color(0.33f, 0.38f, 0.46f, 1f);
            }

            ApplyText(buttonValueText, "READY");
        }

        private void SyncStateViews()
        {
            if (powerToggle != null)
            {
                if (!hasCachedState || lastPowerState != powerToggle.isOn)
                {
                    lastPowerState = powerToggle.isOn;
                    OnPowerToggleChanged(powerToggle.isOn);
                }
            }

            if (masterSlider != null)
            {
                if (!hasCachedState || !Mathf.Approximately(lastSliderValue, masterSlider.value))
                {
                    lastSliderValue = masterSlider.value;
                    OnMasterSliderChanged(masterSlider.value);
                }
            }

            if (intensityScrollbar != null)
            {
                if (!hasCachedState || !Mathf.Approximately(lastScrollbarValue, intensityScrollbar.value))
                {
                    lastScrollbarValue = intensityScrollbar.value;
                    OnIntensityScrollbarChanged(intensityScrollbar.value);
                }
            }

            if (modeDropdown != null)
            {
                if (!hasCachedState || lastDropdownValue != modeDropdown.value)
                {
                    lastDropdownValue = modeDropdown.value;
                    OnModeDropdownChanged(modeDropdown.value);
                }
            }

            if (operatorInput != null)
            {
                if (!hasCachedState || !string.Equals(lastOperatorText, operatorInput.text, System.StringComparison.Ordinal))
                {
                    lastOperatorText = operatorInput.text;
                    OnOperatorInputChanged(operatorInput.text);
                }
            }

            if (targetToggle != null)
            {
                if (!hasCachedState || lastTargetState != targetToggle.isOn)
                {
                    lastTargetState = targetToggle.isOn;
                    OnTargetToggleChanged(targetToggle.isOn);
                }
            }

            hasCachedState = true;
        }

        private static void ApplyLamp(Image image, bool isActive, Color activeColor, Color inactiveColor)
        {
            if (image != null)
            {
                image.color = isActive ? activeColor : inactiveColor;
            }
        }

        private static void ApplyToggleVisual(Image background, Image checkmark, bool isActive, Color activeColor, Color inactiveColor)
        {
            if (background != null)
            {
                background.color = isActive ? new Color(activeColor.r, activeColor.g, activeColor.b, 0.32f) : inactiveColor;
            }

            if (checkmark != null)
            {
                checkmark.enabled = isActive;
                checkmark.color = isActive ? activeColor : new Color(1f, 1f, 1f, 0f);
            }
        }

        private static void ApplyFill(Image image, float normalizedValue, Color highColor, Color lowColor)
        {
            if (image != null)
            {
                image.color = Color.Lerp(lowColor, highColor, Mathf.Clamp01(normalizedValue));
            }
        }

        private static void ApplyText(Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
