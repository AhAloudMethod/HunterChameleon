using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class TimerHUD : MonoBehaviour
{
    TextMeshProUGUI timerText;
    TextMeshProUGUI modeText;

    void Awake() => BuildUI();

    void BuildUI()
    {
        var canvasGO = new GameObject("TimerHUD_Canvas");
        DontDestroyOnLoad(canvasGO);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            DontDestroyOnLoad(es);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var panelGO = new GameObject("TimerPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-16f, -16f);
        panelRect.sizeDelta = new Vector2(200f, 70f);

        timerText = CreateText(panelGO, "TimerText", 42);
        var tr = timerText.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(8f, 6f);
        tr.offsetMax = new Vector2(-44f, -6f);

        var modeBtnGO = new GameObject("ModeButton");
        modeBtnGO.transform.SetParent(panelGO.transform, false);
        var modeBtnImg = modeBtnGO.AddComponent<Image>();
        modeBtnImg.color = Color.clear;
        var modeBtn = modeBtnGO.AddComponent<Button>();
        var mr = modeBtnGO.GetComponent<RectTransform>();
        mr.anchorMin = new Vector2(1f, 0f);
        mr.anchorMax = new Vector2(1f, 1f);
        mr.pivot = new Vector2(1f, 0.5f);
        mr.offsetMin = new Vector2(-42f, 6f);
        mr.offsetMax = new Vector2(-6f, -6f);
        modeBtn.onClick.AddListener(() => GameTimer.i?.ToggleMode());

        modeText = CreateText(modeBtnGO, "ModeText", 20);
        modeText.color = new Color(1f, 1f, 1f, 0.7f);
        var modeTextRect = modeText.GetComponent<RectTransform>();
        modeTextRect.anchorMin = Vector2.zero;
        modeTextRect.anchorMax = Vector2.one;
        modeTextRect.offsetMin = Vector2.zero;
        modeTextRect.offsetMax = Vector2.zero;
    }

    TextMeshProUGUI CreateText(GameObject parent, string name, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    void Update()
    {
        if (GameTimer.i == null) return;

        float t = GameTimer.i.DisplayTime;
        timerText.text = $"{(int)(t / 60):00}:{(int)(t % 60):00}";
        modeText.text = GameTimer.i.CurrentMode == TimerMode.CountUp ? "▲" : "▼";
    }
}
