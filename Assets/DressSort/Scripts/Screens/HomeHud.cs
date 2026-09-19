using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 首页预制：顶部状态、左右功能钮、立绘槽、底部主按钮。
    /// </summary>
    public class HomeHud : MonoBehaviour
    {
        public RectTransform dollSlot;
        public Text starLabel;
        public Text energyLabel;
        public Text progressLabel;
        public Text toastLabel;
        public Button startButton;
        public Button dressButton;
        public Button gearButton;
        public Button wipeButton;
        public HomeSideEntry[] sideButtons;

        static readonly string[] LeftIds = { "circle", "rank", "checkin" };
        static readonly string[] LeftNames = { "游戏圈", "排行榜", "签到" };
        static readonly string[] RightIds = { "workshop", "quest", "event" };
        static readonly string[] RightNames = { "工坊", "任务", "活动" };
        static readonly float[] SideYs = { 318f, 122f, -74f };
        static readonly Color CaptionFill = new Color32(0x1B, 0x72, 0x6E, 0xFF);
        const float StagePodiumY = 0.258f;
        const float DollHeightFrac = 0.42f;
        const float PortraitAspect = 864f / 1084f;
        const float StageAspect = 1080f / 1920f;

        public static HomeHud Assemble(RectTransform parent, GameDatabase db)
        {
            var go = new GameObject("HomeScreen", typeof(RectTransform), typeof(HomeHud));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var hud = go.GetComponent<HomeHud>();
            hud.BuildChildren(db);
            return hud;
        }

        void BuildChildren(GameDatabase db)
        {
            UiKit.Icon(transform, "Star", db != null ? db.iconStar : null, new Vector2(0f, 1f),
                new Vector2(70f, -70f), new Vector2(64f, 64f));
            starLabel = UiKit.Label(transform, "StarCount", "0", new Vector2(0f, 1f),
                new Vector2(168f, -70f), new Vector2(140f, 70f), 46, Palette.Ink,
                TextAnchor.MiddleLeft);
            energyLabel = UiKit.Label(transform, "Energy", "体力 5/5", new Vector2(0f, 1f),
                new Vector2(430f, -70f), new Vector2(220f, 70f), 36, Palette.Ink,
                TextAnchor.MiddleLeft);

            Image gear = UiKit.Icon(transform, "Gear", db != null ? db.iconGear : null,
                new Vector2(1f, 1f), new Vector2(-70f, -70f), new Vector2(64f, 64f));
            gearButton = MakeHit(gear.gameObject);

            dollSlot = UiKit.Rect(transform, "DollSlot", new Vector2(0.5f, 1f),
                new Vector2(0f, -780f), new Vector2(640f, 780f));

            var sides = new List<HomeSideEntry>();
            for (int i = 0; i < 3; i++)
            {
                sides.Add(MakeSide(LeftIds[i], LeftNames[i], SpriteOf(db, LeftIds[i]),
                    new Vector2(0f, 0.5f), new Vector2(88f, SideYs[i])));
                sides.Add(MakeSide(RightIds[i], RightNames[i], SpriteOf(db, RightIds[i]),
                    new Vector2(1f, 0.5f), new Vector2(-88f, SideYs[i])));
            }

            sideButtons = sides.ToArray();

            startButton = MakeCta("开始游戏", db != null ? db.btnHomeStart : UiKit.SpriteOf(Chip.Teal),
                new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(460f, 184f), 40);
            dressButton = MakeCta("装扮", db != null ? db.btnHomeDress : UiKit.SpriteOf(Chip.Pink),
                new Vector2(0.5f, 0f), new Vector2(0f, 126f), new Vector2(348f, 140f), 32);

            progressLabel = UiKit.Label(transform, "Progress", "", new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(360f, 32f), 24, Palette.Ink);

            wipeButton = UiKit.Button(transform, "重置", new Vector2(1f, 0f),
                new Vector2(-88f, 56f), new Vector2(140f, 56f), Chip.White, 24, () => { });

            toastLabel = UiKit.Label(transform, "Toast", "", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -420f), new Vector2(620f, 64f), 32, Palette.RoseDark);
            toastLabel.gameObject.SetActive(false);
        }

        public void ApplyChromeLayout()
        {
            Transform title = transform.Find("Title");
            if (title != null)
                title.gameObject.SetActive(false);

            Place(startButton != null ? startButton.transform as RectTransform : null,
                new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(460f, 184f));
            Place(dressButton != null ? dressButton.transform as RectTransform : null,
                new Vector2(0.5f, 0f), new Vector2(0f, 126f), new Vector2(348f, 140f));
            SetCtaAspect(startButton);
            SetCtaAspect(dressButton);
            if (sideButtons == null) return;
            for (int i = 0; i < sideButtons.Length; i++)
            {
                HomeSideEntry entry = sideButtons[i];
                if (entry == null || entry.button == null) continue;
                bool left = entry.id == "circle" || entry.id == "rank" || entry.id == "checkin";
                int row = entry.id == "circle" || entry.id == "workshop" ? 0
                    : entry.id == "rank" || entry.id == "quest" ? 1 : 2;
                var group = entry.button.transform as RectTransform;
                Place(group, left ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f),
                    new Vector2(left ? 88f : -88f, SideYs[row]), new Vector2(168f, 168f));
                if (entry.icon != null)
                    Place(entry.icon.rectTransform, new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(168f, 168f));
                OverlaySideLabel(entry);
            }
        }

        static void SetCtaAspect(Button button)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.preserveAspect = true;
        }

        void OverlaySideLabel(HomeSideEntry entry)
        {
            if (entry == null || entry.button == null || entry.label == null) return;
            Transform plateT = entry.button.transform.Find("Plate");
            if (plateT != null)
                plateT.gameObject.SetActive(false);

            Transform host = entry.icon != null ? entry.icon.transform : entry.button.transform;
            entry.label.transform.SetParent(host, false);
            Place(entry.label.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(160f, 40f));
            StyleSideLabel(entry.label);
        }

        static void StyleSideLabel(Text label)
        {
            if (label == null) return;
            label.fontSize = 32;
            label.fontStyle = FontStyle.Normal;
            label.color = CaptionFill;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            var outline = label.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
            var shadow = label.GetComponent<Shadow>();
            if (shadow != null)
                shadow.enabled = false;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        HomeSideEntry MakeSide(string id, string caption, Sprite sprite, Vector2 anchor, Vector2 offset)
        {
            RectTransform rect = UiKit.Rect(transform, "Side_" + id, anchor, offset, new Vector2(168f, 168f));
            Image icon = UiKit.Icon(rect, "Icon", sprite, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(168f, 168f));
            icon.raycastTarget = true;

            Text label = UiKit.Label(icon.transform, "Label", caption, new Vector2(0.5f, 0f),
                new Vector2(0f, 22f), new Vector2(160f, 40f), 32, CaptionFill);
            StyleSideLabel(label);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = icon;
            button.transition = Selectable.Transition.None;
            return new HomeSideEntry
            {
                id = id,
                caption = caption,
                button = button,
                icon = icon,
                label = label,
            };
        }

        Button MakeCta(string caption, Sprite sprite, Vector2 anchor, Vector2 offset, Vector2 size,
            int fontSize)
        {
            RectTransform rect = UiKit.Rect(transform, "Cta_" + caption, anchor, offset, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : UiKit.Rounded;
            image.preserveAspect = true;
            image.raycastTarget = true;

            UiKit.Label(rect, "Label", caption, new Vector2(0.5f, 0.5f),
                new Vector2(28f, 1f), new Vector2(size.x * 0.56f, size.y * 0.62f), fontSize, Color.white);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            return button;
        }

        static Button MakeHit(GameObject go)
        {
            var image = go.GetComponent<Image>();
            image.raycastTarget = true;
            var button = go.GetComponent<Button>();
            if (button == null)
                button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        public void BindStage(RectTransform frame)
        {
            if (frame == null || dollSlot == null) return;
            Transform bgT = frame.Find("Backdrop");
            if (bgT == null) return;

            var bg = (RectTransform)bgT;
            bg.anchorMin = new Vector2(0.5f, 0.5f);
            bg.anchorMax = new Vector2(0.5f, 0.5f);
            bg.pivot = new Vector2(0.5f, 0.5f);
            bg.anchoredPosition = Vector2.zero;
            var image = bg.GetComponent<Image>();
            if (image != null)
                image.preserveAspect = false;

            var fitter = bg.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = StageAspect;

            dollSlot.SetParent(bg, false);
            float h = DollHeightFrac;
            float w = h * PortraitAspect / StageAspect;
            dollSlot.pivot = new Vector2(0.5f, 0f);
            dollSlot.anchorMin = new Vector2(0.5f - w * 0.5f, StagePodiumY);
            dollSlot.anchorMax = new Vector2(0.5f + w * 0.5f, StagePodiumY + h);
            dollSlot.offsetMin = Vector2.zero;
            dollSlot.offsetMax = Vector2.zero;
            dollSlot.anchoredPosition = Vector2.zero;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Sprite SpriteOf(GameDatabase db, string id)
        {
            if (db == null) return null;
            switch (id)
            {
                case "circle": return db.iconHomeCircle;
                case "rank": return db.iconHomeRank;
                case "checkin": return db.iconHomeCheckin;
                case "workshop": return db.iconHomeWorkshop;
                case "quest": return db.iconHomeQuest;
                case "event": return db.iconHomeEvent;
                default: return null;
            }
        }

        public void Wire(UnityAction onStart, UnityAction onDress, UnityAction onGear,
            UnityAction onWipe, Action<string, string> onSide)
        {
            Bind(startButton, onStart);
            Bind(dressButton, onDress);
            Bind(gearButton, onGear);
            Bind(wipeButton, onWipe);
            if (sideButtons == null) return;
            for (int i = 0; i < sideButtons.Length; i++)
            {
                HomeSideEntry entry = sideButtons[i];
                if (entry == null || entry.button == null) continue;
                string id = entry.id;
                string title = entry.Caption;
                Bind(entry.button, () => onSide?.Invoke(id, title));
            }
        }

        static void Bind(Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public void SetEnergy(int current, int max)
        {
            if (energyLabel == null)
                energyLabel = UiKit.Label(transform, "Energy", "", new Vector2(0f, 1f),
                    new Vector2(430f, -70f), new Vector2(220f, 70f), 36, Palette.Ink,
                    TextAnchor.MiddleLeft);
            energyLabel.text = "体力 " + current + "/" + max;
        }

        public void ShowToast(string message)
        {
            if (toastLabel == null) return;
            toastLabel.text = message;
            toastLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }

    [Serializable]
    public class HomeSideEntry
    {
        public string id;
        public string caption;
        public Button button;
        public Image icon;
        public Text label;

        public string Caption => !string.IsNullOrEmpty(caption) ? caption
            : label != null ? label.text : "";
    }
}
