using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 角色展示。所有换装同一规则：翅膀 → 后发 → 衣服 → 头/前发。
    /// 各层顶边对齐同一张基准画布。正好 1084 高时铺满槽位；
    /// 脚超出基准时画布更高，多出来的部分从槽底伸出去。
    /// </summary>
    public class PaperDoll : MonoBehaviour
    {
        [SerializeField] Image wings;
        [SerializeField] Image hairBack;
        [SerializeField] Image body;
        [SerializeField] Image head;
        [SerializeField] WingMotes motes;

        // 翼根贴在肩背。立绘肩线大约在画布从上往下 36% 的地方。
        const float WingRootFromTop = 0.36f;
        // 翅膀比立绘画布再宽一圈，左右露出肩膀。高度跟着每张图自己的比例走。
        const float WingWidthOverSlot = 1.18f;
        // 对齐画布的基准高度。更长的裙子只在下面多出一截，不能把整个人缩进这个高度里。
        const float CanonH = 1084f;

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
            wings = UiKit.Icon(transform, "Wings", null, new Vector2(0.5f, 1f),
                new Vector2(0f, -size.y * WingRootFromTop), new Vector2(size.x * WingWidthOverSlot, size.y * 0.8f));
            hairBack = UiKit.Icon(transform, "HairBack", null, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            body = UiKit.Icon(transform, "Body", null, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            head = UiKit.Icon(transform, "Head", null, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            RectTransform moteHost = UiKit.Stretch(transform, "WingMotes");
            motes = moteHost.gameObject.AddComponent<WingMotes>();
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
            LayoutWings(wingItem);
            ShowArt(hairBack, hair != null ? hair.wornBack : null);
            ShowArt(body, dress != null ? dress.worn : null);
            ShowArt(head, headless && hair != null ? hair.worn : null);

            int order = 0;
            if (wings != null) wings.transform.SetSiblingIndex(order++);
            if (hairBack != null) hairBack.transform.SetSiblingIndex(order++);
            if (body != null) body.transform.SetSiblingIndex(order++);
            if (head != null) head.transform.SetSiblingIndex(order++);
            if (motes != null)
            {
                motes.Play(wingItem);
                motes.transform.SetAsLastSibling();
            }
        }

        // 翼根在贴图里的位置（从上往下）。三对轮廓不一样，根不在同一高度。
        static float RootInSprite(string id)
        {
            if (id == "wing_aqua") return 0.40f;
            if (id == "wing_rose") return 0.46f;
            if (id == "wing_wisteria") return 0.56f;
            return 0.50f;
        }

        void LayoutWings(ItemDef wingItem)
        {
            if (wings == null) return;
            wings.sprite = wingItem != null ? wingItem.worn : null;
            wings.enabled = wings.sprite != null;
            var parent = (RectTransform)transform;
            Canvas.ForceUpdateCanvases();
            float slotH = parent.rect.height;
            if (slotH < 8f)
                slotH = 778f;

            float aspect = 1.1f;
            if (wings.sprite != null)
            {
                Rect sp = wings.sprite.rect;
                aspect = sp.width / Mathf.Max(1f, sp.height);
            }

            float slotW = slotH * 864f / CanonH;
            float wingW = slotW * WingWidthOverSlot;
            float wingH = wingW / Mathf.Max(0.05f, aspect);
            float rootFrac = RootInSprite(wingItem != null ? wingItem.id : null);
            float centerFromTop = slotH * WingRootFromTop + (0.5f - rootFrac) * wingH;
            var wr = wings.rectTransform;
            wr.anchorMin = new Vector2(0.5f, 1f);
            wr.anchorMax = new Vector2(0.5f, 1f);
            wr.pivot = new Vector2(0.5f, 0.5f);
            wr.sizeDelta = new Vector2(wingW, wingH);
            wr.anchoredPosition = new Vector2(0f, -centerFromTop);

            if (motes != null)
            {
                float rootY = slotH * 0.5f - centerFromTop;
                motes.SetArea(new Vector2(0f, rootY), wingW, wingH);
            }
        }

        void ShowArt(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            LayoutArt(image);
            string key = CdnPortraits.KeyFor(sprite);
            if (key == null) return;
            CdnAssets.Bind(image, key, () => LayoutArt(image));
        }

        /// <summary>
        /// 画布宽按 864 对齐槽宽。包里的立绘是缩小的，高清图更大，两边比例相同，
        /// 所以不能拿当前贴图像素高度去除 1084，否则高清一到人就突然变大。
        /// 脚超出基准时画布更高，多出来的部分从槽底伸出去。
        /// </summary>
        void LayoutArt(Image image)
        {
            if (image == null) return;
            image.enabled = image.sprite != null;
            if (image.sprite == null) return;
            var parent = (RectTransform)transform;
            Canvas.ForceUpdateCanvases();
            float slotH = parent.rect.height;
            if (slotH < 8f)
                slotH = 778f;
            var sp = image.sprite.rect;
            float aspect = sp.width / Mathf.Max(1f, sp.height);
            const float DesignW = 864f;
            float drawW = slotH * DesignW / CanonH;
            float drawH = drawW / aspect;
            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(drawW, drawH);
            image.preserveAspect = true;
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
