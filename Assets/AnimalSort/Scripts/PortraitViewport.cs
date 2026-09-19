using UnityEngine;

[ExecuteAlways]
public class PortraitViewport : MonoBehaviour
{
    [SerializeField] float aspect = 9f / 16f;

    void LateUpdate()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        float screen = (float)Screen.width / Mathf.Max(1, Screen.height);
        if (screen > aspect)
        {
            float width = aspect / screen;
            cam.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
        else
        {
            float height = screen / aspect;
            cam.rect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }
    }
}
