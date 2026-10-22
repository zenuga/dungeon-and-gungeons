using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Connects the settings panel controls to Unity's display and audio settings.</summary>
public class Settings : MonoBehaviour
{
    private const string GraphicsPreference = "GraphicsQuality";
    private const string FullscreenPreference = "FullscreenEnabled";

    private GameObject settingsRoot;
    private Dropdown graphicsDropdown;
    private bool initialized;

    /// <summary>Connect the settings controls on the supplied panel. Safe to call more than once.</summary>
    public void Configure(GameObject root)
    {
        if (root == null || initialized) return;

        settingsRoot = root;
        initialized = true;
        ConfigureFullscreen();
        ConfigureVolume();
        ConfigureGraphics();
    }

    public void ConfigureFullscreen()
    {
        Toggle toggle = settingsRoot.GetComponentInChildren<Toggle>(true);
        bool fullscreen = PlayerPrefs.GetInt(FullscreenPreference, Screen.fullScreen ? 1 : 0) == 1;
        Screen.fullScreen = fullscreen;
        if (toggle == null) return;

        toggle.SetIsOnWithoutNotify(fullscreen);
        toggle.onValueChanged.AddListener(SetFullscreen);
    }

    public void ConfigureVolume()
    {
        Slider[] sliders = settingsRoot.GetComponentsInChildren<Slider>(true);
        Slider effectsSlider = FindSlider(sliders, "vfx", "sfx", "effect");
        if (effectsSlider == null && sliders.Length > 0) effectsSlider = sliders[0];
        if (effectsSlider == null) return;

        Slider musicSlider = FindSlider(sliders, "music");
        if (musicSlider == null)
        {
            // Reuse the second slider already present in the start screen. This keeps
            // both controls inside the panel and avoids creating a misplaced duplicate.
            foreach (Slider candidate in sliders)
            {
                if (candidate != null && candidate != effectsSlider)
                {
                    musicSlider = candidate;
                    break;
                }
            }
        }
        if (musicSlider == null) musicSlider = CreateVolumeSliderCopy(effectsSlider, "Background Music Volume Slider", -80f);

        RectTransform effectsRect = effectsSlider.GetComponent<RectTransform>();
        RectTransform musicRect = musicSlider.GetComponent<RectTransform>();
        if (effectsRect != null && musicRect != null && musicSlider != effectsSlider)
        {
            Vector2 musicPosition = effectsRect.anchoredPosition;
            musicPosition.y -= 80f;
            musicRect.anchoredPosition = musicPosition;
        }

        ConfigureVolumeSlider(effectsSlider, "VFX / SFX", GameAudioManager.GetSavedEffectVolume(), SetEffectVolume);
        ConfigureVolumeSlider(musicSlider, "Background Music", GameAudioManager.GetSavedMusicVolume(), SetMusicVolume);
    }

    public void ConfigureGraphics()
    {
        graphicsDropdown = settingsRoot.GetComponentInChildren<Dropdown>(true);
        if (graphicsDropdown == null)
            graphicsDropdown = CreateGraphicsDropdown(settingsRoot.transform);

        List<string> options = new List<string>(QualitySettings.names);
        if (options.Count == 0) options.Add("Default");
        graphicsDropdown.ClearOptions();
        graphicsDropdown.AddOptions(options);

        int quality = Mathf.Clamp(PlayerPrefs.GetInt(GraphicsPreference, QualitySettings.GetQualityLevel()), 0, options.Count - 1);
        QualitySettings.SetQualityLevel(quality, true);
        graphicsDropdown.SetValueWithoutNotify(quality);
        graphicsDropdown.RefreshShownValue();
        graphicsDropdown.onValueChanged.AddListener(SetGraphicsQuality);
    }

    private void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(FullscreenPreference, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Public Slider callback for sound effects and VFX volume.</summary>
    public void SetEffectVolume(float volume)
    {
        GameAudioManager.SetEffectVolume(volume);
    }

    /// <summary>Public Slider callback for background and boss music volume.</summary>
    public void SetMusicVolume(float volume)
    {
        GameAudioManager.SetMusicVolume(volume);
    }

    private static Slider FindSlider(Slider[] sliders, params string[] nameHints)
    {
        foreach (Slider slider in sliders)
        {
            if (slider == null) continue;
            string name = slider.gameObject.name.ToLowerInvariant();
            foreach (string hint in nameHints)
            {
                if (name.Contains(hint)) return slider;
            }
        }

        return null;
    }

    private static Slider CreateVolumeSliderCopy(Slider source, string objectName, float verticalOffset)
    {
        GameObject copy = Instantiate(source.gameObject, source.transform.parent, false);
        copy.name = objectName;
        RectTransform sourceRect = source.GetComponent<RectTransform>();
        RectTransform copyRect = copy.GetComponent<RectTransform>();
        if (sourceRect != null && copyRect != null)
        {
            copyRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, verticalOffset);
        }

        Slider slider = copy.GetComponent<Slider>();
        if (slider != null)
        {
            slider.onValueChanged.RemoveAllListeners();
        }

        return slider;
    }

    private static void ConfigureVolumeSlider(Slider slider, string label, float volume, UnityEngine.Events.UnityAction<float> onChanged)
    {
        if (slider == null) return;

        slider.gameObject.name = label == "VFX / SFX" ? "VFX Volume Slider" : "Background Music Volume Slider";
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(Mathf.Clamp01(volume));
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(onChanged);

        TMP_Text tmpLabel = slider.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = label;
            return;
        }

        Text legacyLabel = slider.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.text = label;
            return;
        }

        Text createdLabel = CreateText(label + " Label", slider.transform, TextAnchor.MiddleCenter);
        RectTransform labelRect = createdLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 4f);
        labelRect.sizeDelta = new Vector2(0f, 24f);
        createdLabel.text = label;
    }

    private void SetGraphicsQuality(int quality)
    {
        if (QualitySettings.names.Length == 0) return;
        quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(quality, true);
        PlayerPrefs.SetInt(GraphicsPreference, quality);
        PlayerPrefs.Save();
    }

    private static Dropdown CreateGraphicsDropdown(Transform parent)
    {
        GameObject dropdownObject = new GameObject("Graphics Quality Dropdown", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Dropdown));
        dropdownObject.transform.SetParent(parent, false);
        RectTransform rect = dropdownObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 90f);
        rect.sizeDelta = new Vector2(260f, 42f);

        Text qualityLabel = CreateText("Graphics Quality Label", parent, TextAnchor.MiddleCenter);
        RectTransform qualityLabelRect = qualityLabel.rectTransform;
        qualityLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
        qualityLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
        qualityLabelRect.pivot = new Vector2(0.5f, 0.5f);
        qualityLabelRect.anchoredPosition = new Vector2(0f, 124f);
        qualityLabelRect.sizeDelta = new Vector2(260f, 24f);
        qualityLabel.text = "Graphics Quality";

        Image background = dropdownObject.GetComponent<Image>();
        background.color = new Color(0.16f, 0.16f, 0.16f, 0.95f);

        Text caption = CreateText("Caption", dropdownObject.transform, TextAnchor.MiddleLeft);
        RectTransform captionRect = caption.rectTransform;
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = Vector2.one;
        captionRect.offsetMin = new Vector2(12f, 2f);
        captionRect.offsetMax = new Vector2(-32f, -2f);
        caption.text = "Graphics";

        GameObject arrow = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        arrow.transform.SetParent(dropdownObject.transform, false);
        RectTransform arrowRect = arrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = new Vector2(1f, 1f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-10f, 0f);
        arrowRect.sizeDelta = new Vector2(24f, 0f);
        Text arrowText = arrow.GetComponent<Text>();
        arrowText.font = BuiltinFont();
        arrowText.fontSize = 18;
        arrowText.alignment = TextAnchor.MiddleCenter;
        arrowText.text = "▼";

        GameObject template = new GameObject("Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(dropdownObject.transform, false);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, 0f);
        templateRect.sizeDelta = new Vector2(0f, 150f);
        template.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.98f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        item.transform.SetParent(content.transform, false);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0f, 30f);
        item.GetComponent<LayoutElement>().preferredHeight = 30f;
        Toggle itemToggle = item.GetComponent<Toggle>();

        GameObject checkmark = new GameObject("Item Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        checkmark.transform.SetParent(item.transform, false);
        RectTransform checkRect = checkmark.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0f, 0.5f);
        checkRect.anchorMax = new Vector2(0f, 0.5f);
        checkRect.pivot = new Vector2(0f, 0.5f);
        checkRect.anchoredPosition = new Vector2(8f, 0f);
        checkRect.sizeDelta = new Vector2(12f, 12f);
        Image checkImage = checkmark.GetComponent<Image>();
        checkImage.color = new Color(0.95f, 0.75f, 0.2f, 1f);

        Text itemText = CreateText("Item Label", item.transform, TextAnchor.MiddleLeft);
        RectTransform itemTextRect = itemText.rectTransform;
        itemTextRect.anchorMin = Vector2.zero;
        itemTextRect.anchorMax = Vector2.one;
        itemTextRect.offsetMin = new Vector2(28f, 0f);
        itemTextRect.offsetMax = new Vector2(-6f, 0f);
        itemToggle.targetGraphic = itemText;
        itemToggle.graphic = checkImage;

        ScrollRect scroll = template.GetComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        template.SetActive(false);
        Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
        dropdown.targetGraphic = background;
        dropdown.template = templateRect;
        dropdown.captionText = caption;
        dropdown.itemText = itemText;
        dropdown.itemImage = checkImage;
        dropdown.alphaFadeSpeed = 0.15f;
        return dropdown;
    }

    private static Text CreateText(string objectName, Transform parent, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = BuiltinFont();
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Font BuiltinFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
