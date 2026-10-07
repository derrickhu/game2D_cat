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
        public Image energyIcon;
        public Image energyFill;
        public Text progressLabel;
        public Text toastLabel;
        public Button startButton;
        public Button dressButton;
        public Button gearButton;
        public Button wipeButton;
        public Button gmButton;
        public Button energyPlus;
        public HomeSideEntry[] sideButtons;

        static readonly string[] LeftIds = { "circle", "rank", "checkin" };
        static readonly string[] LeftNames = { "游戏圈", "排行榜", "签到" };
        static readonly string[] RightIds = { "workshop", "quest", "event" };
        static readonly string[] RightNames = { "工坊", "任务", "活动" };
        // 图标 168，缎带垂到中心下方约 120。间距 268 时，上一条缎带和下一个圆钮之间留出大约 60。
        static readonly float[] SideYs = { 390f, 122f, -146f };
        static readonly Color CaptionInk = Palette.Ink;
        static readonly Color TagInk = new Color32(0x4A, 0x26, 0x2C, 0xFF);
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
            EnsureEnergyBar(db);

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
                new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(520f, 134f), 44);
            dressButton = MakeCta("装扮", db != null ? db.btnHomeDress : UiKit.SpriteOf(Chip.Pink),
                new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(380f, 139f), 36);

            progressLabel = UiKit.Label(transform, "Progress", "", new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(360f, 32f), 24, Palette.Ink);

            wipeButton = UiKit.Button(transform, "重置", new Vector2(1f, 0f),
                new Vector2(-88f, 56f), new Vector2(140f, 56f), Chip.White, 24, () => { });
            gmButton = UiKit.Button(transform, "GM", new Vector2(0f, 0f),
                new Vector2(88f, 56f), new Vector2(140f, 56f), Chip.White, 24, () => { });

            toastLabel = UiKit.Label(transform, "Toast", "", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -420f), new Vector2(620f, 64f), 32, Palette.RoseDark);
            toastLabel.gameObject.SetActive(false);
        }

        public void ApplyChromeLayout()
        {
            Transform title = transform.Find("Title");
            if (title != null)
                title.gameObject.SetActive(false);

            EnsureEnergyBar(UiKit.Skin);

            Place(startButton != null ? startButton.transform as RectTransform : null,
                new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(520f, 134f));
            Place(dressButton != null ? dressButton.transform as RectTransform : null,
                new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(380f, 139f));
            SetCtaAspect(startButton);
            SetCtaAspect(dressButton);
            // 图标占左侧，字放在右侧空面上，并略微上移，躲开底部的深色底边。
            StyleCtaLabel(startButton, 0.16f);
            StyleCtaLabel(dressButton, 0.14f);
            EnsureGmButton();
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

        static void StyleCtaLabel(Button button, float shift)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            var host = (RectTransform)button.transform;
            float w = host.sizeDelta.x;
            float h = host.sizeDelta.y;
            var rect = label.rectTransform;
            rect.anchoredPosition = new Vector2(w * shift, h * 0.05f);
            rect.sizeDelta = new Vector2(w * 0.56f, h * 0.6f);
            label.color = TagInk;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            var shadow = label.GetComponent<Shadow>();
            if (shadow == null)
                shadow = label.gameObject.AddComponent<Shadow>();
            shadow.enabled = false;
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
            Sprite tagSprite = LabelOf(UiKit.Skin, entry.id);
            Transform tagT = entry.button.transform.Find("Tag");
            if (tagSprite != null)
            {
                Image tag = tagT != null ? tagT.GetComponent<Image>() : null;
                if (tag == null)
                {
                    tag = UiKit.Icon(entry.button.transform, "Tag", tagSprite, new Vector2(0.5f, 0.5f),
                        Vector2.zero, Vector2.one);
                    tag.raycastTarget = false;
                }
                tag.sprite = tagSprite;
                tag.preserveAspect = true;
                tag.gameObject.SetActive(true);
                tag.transform.SetAsLastSibling();
                Place(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(156f, 64f));
                entry.label.transform.SetParent(tag.transform, false);
                Place(entry.label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(156f, 52f));
                entry.label.text = entry.Caption;
                entry.label.gameObject.SetActive(true);
                StyleSideLabel(entry.label);
                entry.label.fontSize = 28;
                entry.label.color = TagInk;
                Transform labelHost = entry.button.transform.Find("Plate");
                if (labelHost != null)
                    labelHost.gameObject.SetActive(false);
                return;
            }
            if (tagT != null) tagT.gameObject.SetActive(false);
            Transform plateT = entry.button.transform.Find("Plate");
            if (plateT != null)
            {
                plateT.gameObject.SetActive(true);
                var plateImg = plateT.GetComponent<Image>();
                if (plateImg != null)
                {
                    plateImg.enabled = false;
                    plateImg.raycastTarget = false;
                }
                Place((RectTransform)plateT, new Vector2(0.5f, 0f),
                    new Vector2(0f, -36f), new Vector2(168f, 36f));
                Place(entry.label.rectTransform, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(168f, 36f));
            }
            else
            {
                Place(entry.label.rectTransform, new Vector2(0.5f, 0f),
                    new Vector2(0f, -36f), new Vector2(168f, 36f));
            }
            StyleSideLabel(entry.label);
        }

        static void StyleSideLabel(Text label)
        {
            if (label == null) return;
            label.fontSize = 26;
            // 字体文件已经是 700 字重，再开 Bold 会错位叠一层，边缘发虚。
            label.fontStyle = FontStyle.Normal;
            label.color = CaptionInk;
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

            Text label = UiKit.Label(rect, "Label", caption, new Vector2(0.5f, 0f),
                new Vector2(0f, -36f), new Vector2(168f, 36f), 26, CaptionInk);
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

        static Sprite LabelOf(GameDatabase db, string id)
        {
            if (db == null) return null;
            switch (id)
            {
                case "circle": return db.labelHomeCircle;
                case "rank": return db.labelHomeRank;
                case "checkin": return db.labelHomeCheckin;
                case "workshop": return db.labelHomeWorkshop;
                case "quest": return db.labelHomeQuest;
                case "event": return db.labelHomeEvent;
                default: return null;
            }
        }

        public void Wire(UnityAction onStart, UnityAction onDress, UnityAction onEnergyPlus,
            UnityAction onWipe, UnityAction onGm, Action<string, string> onSide)
        {
            Bind(startButton, onStart);
            Bind(dressButton, onDress);
            Bind(energyPlus, onEnergyPlus);
            Bind(wipeButton, onWipe);
            Bind(gmButton, onGm);
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
            Sfx.BindClick(button, action);
        }

        void EnsureGmButton()
        {
            if (gmButton != null)
            {
                Place((RectTransform)gmButton.transform, new Vector2(0f, 0f),
                    new Vector2(88f, 56f), new Vector2(140f, 56f));
                return;
            }
            gmButton = UiKit.Button(transform, "GM", new Vector2(0f, 0f),
                new Vector2(88f, 56f), new Vector2(140f, 56f), Chip.White, 24, () => { });
        }

        public void SetStartCaption(string caption)
        {
            if (startButton == null) return;
            var label = startButton.GetComponentInChildren<Text>();
            if (label == null) return;
            label.text = caption;
            label.fontSize = caption != null && caption.Length > 4 ? 36 : 44;
        }

        public void SetEnergy(int current, int max)
        {
            EnsureEnergyBar(UiKit.Skin);
            if (energyLabel != null)
                energyLabel.text = current + "/" + max;
        }

        void EnsureEnergyBar(GameDatabase db)
        {
            HideNamed("Star");
            HideNamed("StarCount");
            HideNamed("Gear");
            HideNamed("EnergyTrack");
            HideNamed("EnergyTrackBack");
            if (gearButton != null)
                gearButton.gameObject.SetActive(false);
            if (starLabel != null)
                starLabel.gameObject.SetActive(false);
            if (energyFill != null)
                energyFill.gameObject.SetActive(false);

            Sprite sprite = EnergySprite(db);
            if (energyIcon == null)
            {
                Transform existing = transform.Find("EnergyIcon");
                energyIcon = existing != null ? existing.GetComponent<Image>() : null;
            }
            if (energyIcon == null)
                energyIcon = UiKit.Icon(transform, "EnergyIcon", sprite, new Vector2(0f, 1f),
                    new Vector2(72f, -72f), new Vector2(64f, 64f));
            if (sprite != null)
                energyIcon.sprite = sprite;
            energyIcon.type = Image.Type.Simple;
            energyIcon.preserveAspect = true;
            energyIcon.color = Color.white;
            energyIcon.raycastTarget = false;
            energyIcon.gameObject.SetActive(true);
            Place(energyIcon.rectTransform, new Vector2(0f, 1f),
                new Vector2(72f, -72f), new Vector2(64f, 64f));

            Image plate = EnsureEnergyCountPlate();
            Place(plate.rectTransform, new Vector2(0f, 1f),
                new Vector2(176f, -72f), new Vector2(132f, 44f));

            if (energyLabel == null)
            {
                Transform labelT = transform.Find("Energy");
                if (labelT == null && energyIcon != null)
                    labelT = energyIcon.transform.Find("Energy");
                if (labelT == null)
                    labelT = plate.transform.Find("Energy");
                energyLabel = labelT != null ? labelT.GetComponent<Text>() : null;
            }
            if (energyLabel == null)
                energyLabel = UiKit.Label(plate.transform, "Energy", "5/5", new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(124f, 40f), 34, Color.white);
            energyLabel.transform.SetParent(plate.transform, false);
            energyLabel.fontSize = 34;
            energyLabel.color = Color.white;
            energyLabel.alignment = TextAnchor.MiddleCenter;
            var outline = energyLabel.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
            Place(energyLabel.rectTransform, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(124f, 40f));

            if (energyPlus == null)
            {
                Transform plusT = transform.Find("EnergyPlus");
                if (plusT != null)
                    energyPlus = plusT.GetComponent<Button>();
            }
            if (energyPlus == null)
            {
                Image face = UiKit.Icon(transform, "EnergyPlus", UiKit.Circle, new Vector2(0f, 1f),
                    new Vector2(276f, -72f), new Vector2(40f, 40f));
                face.color = new Color32(0x6F, 0xC8, 0x5A, 0xFF);
                face.raycastTarget = true;
                UiKit.Label(face.transform, "Label", "+", new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(40f, 40f), 32, Color.white);
                energyPlus = face.gameObject.AddComponent<Button>();
                energyPlus.targetGraphic = face;
                energyPlus.transition = Selectable.Transition.None;
            }
            energyPlus.gameObject.SetActive(true);
            Place(energyPlus.transform as RectTransform, new Vector2(0f, 1f),
                new Vector2(276f, -72f), new Vector2(40f, 40f));
        }

        Image EnsureEnergyCountPlate()
        {
            Transform plateT = transform.Find("EnergyCountPlate");
            Image plate = plateT != null ? plateT.GetComponent<Image>() : null;
            if (plate == null)
                plate = UiKit.Slice(transform, "EnergyCountPlate", UiKit.SoftRect, new Vector2(0f, 1f),
                    new Vector2(176f, -72f), new Vector2(132f, 44f), Palette.Ink);
            plate.sprite = UiKit.SoftRect;
            plate.type = Image.Type.Sliced;
            plate.color = Palette.Ink;
            plate.raycastTarget = false;
            plate.gameObject.SetActive(true);
            return plate;
        }

        static Sprite EnergySprite(GameDatabase db)
        {
            if (db != null && db.iconHomeEnergy != null)
                return db.iconHomeEnergy;
            return UiKit.Skin != null ? UiKit.Skin.iconHomeEnergy : null;
        }

        void HideNamed(string child)
        {
            Transform found = transform.Find(child);
            if (found != null)
                found.gameObject.SetActive(false);
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
