using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 角色展示。所有换装同一规则：翅膀 → 后发 → 衣服 → 头/前发。
    /// 各层必须是同一张 864x1084 画布，靠原图像素坐标对齐。
    /// </summary>
    public class PaperDoll : MonoBehaviour
    {
        [SerializeField] Image wings;
        [SerializeField] Image hairBack;
        [SerializeField] Image body;
        [SerializeField] Image head;

        const float WingAnchorY = 0.33f;
        const float WingWidthRatio = 1.55f;

        public static PaperDoll Create(Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            RectTransform root = UiKit.Rect(parent, "PaperDoll", anchor, offset, size);
            var doll = root.gameObject.AddComponent<PaperDoll>();
            doll.BuildLayers(size);
            return doll;
        }

        public static PaperDoll CreateFill(RectTransform parent)
        {
            RectTransform root = UiKit.Stretch(parent, "PaperDoll");
            var doll = root.gameObject.AddComponent<PaperDoll>();
            Vector2 size = parent.rect.size;
            if (size.x < 8f)
                size = new Vector2(640f, 804f);
            doll.BuildLayers(size);
            StretchLayers(root);
            return doll;
        }

        void BuildLayers(Vector2 size)
        {
            float wingWidth = size.x * WingWidthRatio;
            wings = UiKit.Icon(transform, "Wings", null, new Vector2(0.5f, 1f),
                new Vector2(0f, -size.y * WingAnchorY), new Vector2(wingWidth, wingWidth * 0.62f));
            hairBack = UiKit.Icon(transform, "HairBack", null, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            body = UiKit.Icon(transform, "Body", null, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            head = UiKit.Icon(transform, "Head", null, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }

        static void StretchLayers(RectTransform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = (RectTransform)root.GetChild(i);
                if (child.gameObject.name == "Wings")
                    continue;
                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.offsetMin = Vector2.zero;
                child.offsetMax = Vector2.zero;
            }
        }

        public void Show(ItemDef dress, ItemDef wingItem, ItemDef hair)
        {
            bool headless = dress != null && dress.layeredWithHair;
            if (wings != null)
            {
                wings.sprite = wingItem != null ? wingItem.worn : null;
                wings.enabled = wings.sprite != null;
                var wr = wings.rectTransform;
                float pw = ((RectTransform)transform).rect.width;
                if (pw < 8f) pw = 640f;
                wr.anchorMin = new Vector2(0.5f, 1f - WingAnchorY);
                wr.anchorMax = new Vector2(0.5f, 1f - WingAnchorY);
                wr.pivot = new Vector2(0.5f, 0.5f);
                wr.anchoredPosition = Vector2.zero;
                wr.sizeDelta = new Vector2(pw * WingWidthRatio, pw * WingWidthRatio * 0.62f);
            }
            if (hairBack != null)
            {
                hairBack.sprite = hair != null ? hair.wornBack : null;
                hairBack.enabled = hairBack.sprite != null;
            }
            if (body != null)
            {
                body.sprite = dress != null ? dress.worn : null;
                body.enabled = body.sprite != null;
            }
            if (head != null)
            {
                head.sprite = headless && hair != null ? hair.worn : null;
                head.enabled = head.sprite != null;
            }

            int order = 0;
            if (wings != null) wings.transform.SetSiblingIndex(order++);
            if (hairBack != null) hairBack.transform.SetSiblingIndex(order++);
            if (body != null) body.transform.SetSiblingIndex(order++);
            if (head != null) head.transform.SetSiblingIndex(order++);
        }

        public void Pop()
        {
            StopAllCoroutines();
            StartCoroutine(PopRoutine());
        }

        System.Collections.IEnumerator PopRoutine()
        {
            RectTransform rect = (RectTransform)transform;
            float t = 0f;
            const float duration = 0.22f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float scale = 1f + 0.07f * Mathf.Sin(k * Mathf.PI);
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }
    }
}
