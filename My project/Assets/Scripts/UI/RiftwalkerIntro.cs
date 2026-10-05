using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Inserted into the build copy of MenuPrincipal; source scenes stay untouched.
public sealed class RiftwalkerIntro : MonoBehaviour
{
    [SerializeField] private Texture2D logo;
    [SerializeField] private GameObject[] delayedRoots;
    private static bool played;
    private bool released;
    private CanvasGroup group;
    private RectTransform reveal;
    private RectTransform logoRect;
    private RawImage logoImage;
    private RiftwalkerRiftGraphic rift;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => played = false;

    public void ConfigureForBuild(Texture2D texture, GameObject[] roots)
    {
        logo = texture;
        delayedRoots = roots;
    }

    private void Awake()
    {
        // Returning to the menu never replays the opening. Automated clients skip it.
        if (played || gameObject.scene.buildIndex != 0 || Application.isBatchMode || logo == null
            || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-unirse") >= 0)
        {
            ReleaseMenu();
            Destroy(gameObject);
            return;
        }
        played = true;
        BuildVisuals();
    }

    private void BuildVisuals()
    {
        var cameraObject = new GameObject("Intro black camera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 0;
        camera.depth = -100;

        var canvasObject = new GameObject("Intro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        group = canvasObject.GetComponent<CanvasGroup>();
        var black = Rect("Black", canvas.transform, Vector2.zero);
        black.anchorMin = Vector2.zero;
        black.anchorMax = Vector2.one;
        black.gameObject.AddComponent<Image>().color = Color.black;

        reveal = Rect("Rift reveal", canvas.transform, new Vector2(0, 460));
        reveal.gameObject.AddComponent<RectMask2D>();
        logoRect = Rect("Original logo", reveal, new Vector2(1160, 1160f * logo.height / logo.width));
        logoImage = logoRect.gameObject.AddComponent<RawImage>();
        logoImage.texture = logo;
        logoImage.raycastTarget = false;
        var effects = Rect("Rift energy", canvas.transform, new Vector2(1920, 1080));
        rift = effects.gameObject.AddComponent<RiftwalkerRiftGraphic>();
        rift.raycastTarget = false;
    }

    private IEnumerator Start()
    {
        if (released) yield break;
        float elapsed = 0f;
        while (elapsed < 4.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            float open = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.05f, 2.35f, elapsed));
            reveal.sizeDelta = new Vector2(1240 * open, 460);
            logoRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, open);
            rift.SetTime(elapsed);
            // Fade to pure black before activating the menu behind the overlay.
            if (elapsed > 3.5f)
                logoImage.color = new Color(1, 1, 1, 1 - Mathf.InverseLerp(3.5f, 4.1f, elapsed));
            yield return null;
        }
        ReleaseMenu();
        yield return null; // Let the menu's Start methods configure their first frame.
        float fade = 0f;
        while (fade < 0.35f)
        {
            fade += Time.unscaledDeltaTime;
            group.alpha = 1 - Mathf.Clamp01(fade / 0.35f);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void ReleaseMenu()
    {
        if (released) return;
        released = true;
        if (delayedRoots == null) return;
        foreach (var root in delayedRoots)
            if (root != null) root.SetActive(true);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return rect;
    }
}
