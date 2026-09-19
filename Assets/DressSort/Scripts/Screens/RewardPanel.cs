using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 过关解锁：五彩剪影礼盒从上面落下，落地后播打开序列，
    /// 新解锁的衣服从盒子里升出来。
    /// </summary>
    public class RewardPanel : Panel
    {
        static readonly string[] FrameNames =
        {
            "gift_0_closed", "gift_1_lift", "gift_2_hover", "gift_3_open",
        };

        Image box;
        Image dress;
        Image shadow;
        Text nameLabel;
        Text hintLabel;
        Button takeButton;
        ItemDef reward;
        bool revealed;
        Sprite mysterySprite;
        readonly Sprite[] openFrames = new Sprite[4];

        protected override void Build()
        {
            UiKit.Label(root, "Title", "整理完成", new Vector2(0.5f, 1f), new Vector2(0f, -220f),
                new Vector2(900f, 120f), 80, Palette.Ink);

            RectTransform stage = UiKit.Rect(root, "Stage", new Vector2(0.5f, 1f),
                new Vector2(0f, -820f), new Vector2(720f, 720f));

            shadow = UiKit.Slice(stage, "Shadow", UiKit.Circle, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -210f), new Vector2(260f, 70f), new Color(0.2f, 0.16f, 0.22f, 0.18f));

            box = UiKit.Icon(stage, "Box", null, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(420f, 420f));

            dress = UiKit.Icon(stage, "Dress", null, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 168f), new Vector2(300f, 300f));
            dress.gameObject.SetActive(false);

            UiKit.HitArea(stage, "Tap", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(720f, 720f), OnTap);

            nameLabel = UiKit.Label(root, "Name", "", new Vector2(0.5f, 1f), new Vector2(0f, -1240f),
                new Vector2(900f, 100f), 56, Palette.Ink);
            hintLabel = UiKit.Label(root, "Hint", "", new Vector2(0.5f, 1f),
                new Vector2(0f, -1330f), new Vector2(900f, 80f), 36, Palette.Caption);

            takeButton = UiKit.Button(root, "收下并试穿", new Vector2(0.5f, 0f), new Vector2(0f, 380f),
                new Vector2(640f, 140f), Chip.Teal, 54, OnTake);
            takeButton.gameObject.SetActive(false);
        }

        public override void OnShow()
        {
            reward = app.PendingLevel != null ? app.PendingLevel.reward : null;
            revealed = false;
            LoadArt();
            ResetPose();
            takeButton.gameObject.SetActive(false);
            nameLabel.text = "";
            hintLabel.text = "";
            if (reward == null) return;
            StartCoroutine(ShowRoutine());
        }

        public override void OnHide()
        {
            StopAllCoroutines();
        }

        void LoadArt()
        {
            mysterySprite = app.Database != null ? app.Database.iconMystery : null;
            if (mysterySprite == null && app.PendingLevel != null && app.PendingLevel.mystery != null)
                mysterySprite = app.PendingLevel.mystery.ResolveIcon();
            for (int i = 0; i < FrameNames.Length; i++)
                openFrames[i] = Resources.Load<Sprite>("GiftOpen/" + FrameNames[i]);
        }

        void ResetPose()
        {
            box.sprite = mysterySprite;
            box.rectTransform.anchoredPosition = new Vector2(0f, 520f);
            box.rectTransform.localScale = Vector3.one * 0.72f;
            box.color = Color.white;
            dress.gameObject.SetActive(false);
            dress.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            dress.rectTransform.localScale = Vector3.one * 0.16f;
            shadow.rectTransform.localScale = new Vector3(0.45f, 0.45f, 1f);
            shadow.color = new Color(0.2f, 0.16f, 0.22f, 0.08f);
        }

        void OnTap()
        {
            if (revealed || reward == null) return;
            StopAllCoroutines();
            ApplyReveal();
        }

        IEnumerator ShowRoutine()
        {
            yield return LandBox();
            yield return BecomeGift();
            yield return PlayOpen();
            yield return PopDress();
            FinishReveal(false);
        }

        IEnumerator LandBox()
        {
            box.sprite = mysterySprite;
            Vector2 from = new Vector2(0f, 520f);
            Vector2 to = Vector2.zero;
            float t = 0f;
            const float duration = 0.46f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float drop = k * k;
                box.rectTransform.anchoredPosition = Vector2.Lerp(from, to, drop);
                box.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.04f, drop);
                float shade = Mathf.Lerp(0.08f, 0.22f, drop);
                shadow.color = new Color(0.2f, 0.16f, 0.22f, shade);
                shadow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.05f, drop);
                yield return null;
            }

            t = 0f;
            const float bounce = 0.16f;
            while (t < bounce)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / bounce);
                box.rectTransform.localScale = new Vector3(1.08f - k * 0.08f, 0.88f + k * 0.12f, 1f);
                box.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(k * Mathf.PI) * -18f);
                shadow.rectTransform.localScale = new Vector3(1.12f - k * 0.12f, 0.9f + k * 0.1f, 1f);
                yield return null;
            }

            box.rectTransform.anchoredPosition = Vector2.zero;
            box.rectTransform.localScale = Vector3.one;
            shadow.rectTransform.localScale = Vector3.one;
        }

        IEnumerator BecomeGift()
        {
            if (openFrames[0] == null) yield break;

            float t = 0f;
            const float duration = 0.18f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float pulse = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f;
                box.rectTransform.localScale = Vector3.one * pulse;
                if (k >= 0.45f)
                    box.sprite = openFrames[0];
                yield return null;
            }

            box.sprite = openFrames[0];
            box.rectTransform.localScale = Vector3.one;
        }

        IEnumerator PlayOpen()
        {
            for (int i = 1; i < openFrames.Length; i++)
            {
                if (openFrames[i] == null) continue;
                box.sprite = openFrames[i];
                box.rectTransform.localScale = Vector3.one * 1.04f;
                float t = 0f;
                const float hold = 0.16f;
                while (t < hold)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / hold);
                    box.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, k);
                    yield return null;
                }
            }
        }

        IEnumerator PopDress()
        {
            if (reward == null) yield break;
            dress.sprite = reward.ResolveIcon();
            dress.gameObject.SetActive(true);
            dress.rectTransform.SetAsLastSibling();

            Vector2 from = new Vector2(0f, 28f);
            Vector2 to = new Vector2(0f, 176f);
            float t = 0f;
            const float duration = 0.42f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = EaseOutBack(k);
                dress.rectTransform.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                dress.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.16f, 1f, e);
                yield return null;
            }

            dress.rectTransform.anchoredPosition = to;
            dress.rectTransform.localScale = Vector3.one;
            BurstSparks();
        }

        void BurstSparks()
        {
            RectTransform stage = box.rectTransform.parent as RectTransform;
            if (stage == null) return;
            Vector2 origin = new Vector2(0f, 120f);
            for (int i = 0; i < 7; i++)
            {
                float angle = (i / 7f) * Mathf.PI * 2f - Mathf.PI * 0.5f;
                Image spark = UiKit.Slice(stage, "Spark", UiKit.Circle, new Vector2(0.5f, 0.5f),
                    origin, new Vector2(16f, 16f), new Color(1f, 0.86f, 0.38f, 1f));
                spark.raycastTarget = false;
                StartCoroutine(FlySpark(spark.rectTransform, origin,
                    origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 90f));
            }
        }

        IEnumerator FlySpark(RectTransform rect, Vector2 from, Vector2 to)
        {
            float t = 0f;
            Image image = rect.GetComponent<Image>();
            while (t < 0.36f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.36f);
                rect.anchoredPosition = Vector2.Lerp(from, to, 1f - (1f - k) * (1f - k));
                if (image != null)
                    image.color = new Color(1f, 0.86f, 0.38f, 1f - k);
                rect.localScale = Vector3.one * (1.15f - k);
                yield return null;
            }
            UiKit.Discard(rect);
        }

        /// <summary>自检脚本直接跳到揭开后的结果，跳过落地动画。</summary>
        public void ApplyReveal()
        {
            if (reward == null) return;
            StopAllCoroutines();
            LoadArt();
            FinishReveal(true);
        }

        void FinishReveal(bool snap)
        {
            revealed = true;
            if (openFrames[3] != null)
                box.sprite = openFrames[3];
            else if (openFrames[0] != null)
                box.sprite = openFrames[0];

            box.rectTransform.anchoredPosition = Vector2.zero;
            box.rectTransform.localScale = Vector3.one;
            shadow.rectTransform.localScale = Vector3.one;
            shadow.color = new Color(0.2f, 0.16f, 0.22f, 0.2f);

            dress.sprite = reward.ResolveIcon();
            dress.gameObject.SetActive(true);
            dress.rectTransform.anchoredPosition = new Vector2(0f, 176f);
            dress.rectTransform.localScale = Vector3.one;

            if (snap)
                dress.rectTransform.SetAsLastSibling();

            app.Wardrobe.Unlock(reward);
            nameLabel.text = "解锁了「" + reward.displayName + "」";
            hintLabel.text = reward.slot == ItemSlot.Wings ? "翅膀已收进衣柜" : "已经放进衣柜";
            takeButton.gameObject.SetActive(true);
        }

        void OnTake()
        {
            if (reward != null)
                app.Wardrobe.Equip(reward);
            app.Show(ScreenId.DressUp);
        }

        static float EaseOutBack(float k)
        {
            const float s = 1.35f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }
    }
}
