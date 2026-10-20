using TMPro;
using UnityEngine;

/// <summary>Updates the dungeon-clear counter on each local player's HUD.</summary>
public static class DungeonProgressDisplay
{
    private const string ProgressObjectName = "DungeonProgressText";
    private static int currentProgress;

    public static void SetProgress(int completedDungeons)
    {
        currentProgress = Mathf.Clamp(completedDungeons, 0, 5);
        string progressText = currentProgress == 0
            ? "dungeon cleared: 0/5"
            : currentProgress >= 5
                ? "take the ladder to the next layer or keep looting"
                : $"dungeons cleared: {currentProgress}/5";

        PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerController player in players)
        {
            if (player == null || player.PlayerHud == null) continue;

            TMP_Text display = FindDisplay(player.PlayerHud);
            if (display == null) display = CreateDisplay(player.PlayerHud);
            if (display == null) continue;

            display.gameObject.name = ProgressObjectName;
            display.text = progressText;
        }
    }

    private static TMP_Text FindDisplay(GameObject playerHud)
    {
        TMP_Text[] textComponents = playerHud.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in textComponents)
        {
            if (text.gameObject.name == ProgressObjectName) return text;
        }

        foreach (TMP_Text text in textComponents)
        {
            if (text.text.ToLowerInvariant().Contains("dungeon cleared")) return text;
        }

        return null;
    }

    private static TMP_Text CreateDisplay(GameObject playerHud)
    {
        GameObject textObject = new GameObject(ProgressObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(playerHud.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -28f);
        rect.sizeDelta = new Vector2(535f, 165f);

        TextMeshProUGUI display = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text style = playerHud.GetComponentInChildren<TMP_Text>(true);
        display.font = style != null ? style.font : TMP_Settings.defaultFontAsset;
        display.fontSize = style != null ? style.fontSize : 60f;
        display.enableAutoSizing = true;
        display.fontSizeMin = 18f;
        display.fontSizeMax = style != null ? style.fontSize : 60f;
        display.color = style != null ? style.color : new Color(0.84f, 0.91f, 0.3f, 1f);
        display.alignment = TextAlignmentOptions.Center;
        display.enableWordWrapping = true;
        display.raycastTarget = false;
        return display;
    }
}
