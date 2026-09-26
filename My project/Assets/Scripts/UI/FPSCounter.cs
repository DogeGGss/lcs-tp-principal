using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text textoFPS;

    private float tiempo;
    private int frames;

    private void Update()
    {
        frames++;
        tiempo += Time.unscaledDeltaTime;

        if (tiempo >= 0.5f)
        {
            float fps = frames / tiempo;

            textoFPS.text = "FPS: " + Mathf.RoundToInt(fps);

            frames = 0;
            tiempo = 0f;
        }
    }
}