using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 过关任务。一条往下排的礼物，到了关卡就能领。前面密，后面疏。
    /// </summary>
    public class QuestPopup : PopupView
    {
        public RectTransform content;
        public ScrollRect scroll;
        public Text progressLabel;
        public Text footerLabel;
        public Sprite rowSprite;
        public Sprite badgeSprite;
        public Sprite claimSprite;
        public Sprite doneSprite;

        const float RowH = 188f;
        const float Gap = 16f;
        static readonly Color Ink = new Color32(0x3A, 0x1C, 0x35, 0xFF);
        static readonly Color Sub = new Color32(0x8A, 0x4A, 0x5E, 0xFF);
        static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);

        readonly List<TaskCatalog.SpriteSlot> icons = new List<TaskCatalog.SpriteSlot>();

        protected override void OnOpen()
        {
            Fill();
        }

        void Fill()
        {
            WardrobeService wardrobe = app.Wardrobe;
            if (progressLabel != null)
                progressLabel.text = "已通关 " + wardrobe.LevelsCleared + " 关";
            int gap = wardrobe.NextTaskGap();
            if (footerLabel != null)
            {
                footerLabel.fontSize = 34;
                footerLabel.alignment = TextAnchor.MiddleCenter;
                footerLabel.rectTransform.anchoredPosition = new Vector2(0f, 12f);
                if (gap < 0) footerLabel.text = "礼物都领完啦";
                else if (gap == 0) footerLabel.text = "有礼物可以领";
                else footerLabel.text = "距离下一份还差 " + gap + " 关";
            }

            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
                UiKit.Discard(content.GetChild(i));

            int count = TaskCatalog.Count;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 12f + count * (RowH + Gap));
            int focus = 0;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                BuildRow(i);
                if (!found && !wardrobe.TaskClaimed(i))
                {
                    focus = i;
                    found = true;
                }
            }
            if (scroll != null)
                scroll.verticalNormalizedPosition = count <= 1 ? 1f : 1f - focus / (float)(count - 1);
        }

        void BuildRow(int index)
        {
            TaskCatalog.Reward reward = TaskCatalog.Track[index];
            bool claimed = app.Wardrobe.TaskClaimed(index);
            bool ready = app.Wardrobe.CanClaimTask(index);
            int left = Mathf.Max(0, reward.level - app.Wardrobe.LevelsCleared);

            float y = -12f - RowH * 0.5f - index * (RowH + Gap);
            Image plate = UiKit.Slice(content, "Row" + index, rowSprite, new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(760f, RowH), Color.white);
            plate.raycastTarget = true;
            RectTransform row = plate.rectTransform;

            Image badge = UiKit.Icon(row, "Badge", badgeSprite, new Vector2(0f, 0.5f),
                new Vector2(64f, 0f), new Vector2(96f, 96f));
            badge.raycastTarget = false;
            Text level = UiKit.Label(badge.rectTransform, "Level", reward.level.ToString(), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(84f, 56f), 32, Color.white);
            level.resizeTextForBestFit = true;
            level.resizeTextMinSize = 20;
            level.resizeTextMaxSize = 34;

            TaskCatalog.Icons(reward, app.Database, icons);
            int shown = Mathf.Min(3, icons.Count);
            string title = TaskCatalog.Describe(reward, app.Database);
            if (shown <= 1)
            {
                const float iconSize = 112f;
                float textW = Mathf.Clamp((title.Length > 0 ? title.Length : 1) * 34f, 68f, 300f);
                const float gap = 18f;
                float origin = 330f - (iconSize + gap + textW) * 0.5f;
                Image icon = UiKit.Icon(row, "Icon0", shown == 1 ? icons[0].sprite : null, new Vector2(0f, 0.5f),
                    new Vector2(origin + iconSize * 0.5f, 0f), new Vector2(iconSize, iconSize));
                icon.raycastTarget = false;
                Text name = UiKit.Label(row, "Name", title, new Vector2(0f, 0.5f),
                    new Vector2(origin + iconSize + gap + textW * 0.5f, 0f), new Vector2(textW + 8f, 80f), 34, Ink);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.verticalOverflow = VerticalWrapMode.Truncate;
            }
            else
            {
                const float iconSize = 86f;
                float span = shown * iconSize + (shown - 1) * 12f;
                float origin = 300f - span * 0.5f + iconSize * 0.5f;
                for (int n = 0; n < shown; n++)
                {
                    Image icon = UiKit.Icon(row, "Icon" + n, icons[n].sprite, new Vector2(0f, 0.5f),
                        new Vector2(origin + n * (iconSize + 12f), 28f), new Vector2(iconSize, iconSize));
                    icon.raycastTarget = false;
                }
                Text name = UiKit.Label(row, "Name", title, new Vector2(0f, 0.5f),
                    new Vector2(300f, -48f), new Vector2(440f, 52f), 30, Ink, TextAnchor.MiddleCenter);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
            }

            if (claimed)
            {
                LabelButton(row, "已领", doneSprite, false);
                return;
            }
            if (ready)
            {
                Button button = LabelButton(row, "领取", claimSprite, true);
                int captured = index;
                button.onClick.AddListener(() => OnClaim(captured));
                return;
            }
            UiKit.Label(row, "Wait", "再过 " + left + " 关", new Vector2(1f, 0.5f),
                new Vector2(-100f, 0f), new Vector2(170f, 56f), 28, Sub);
        }

        Button LabelButton(RectTransform row, string caption, Sprite face, bool enabled)
        {
            Image image = UiKit.Slice(row, "Claim", face, new Vector2(1f, 0.5f),
                new Vector2(-96f, 0f), new Vector2(156f, 78f), Color.white);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            Text label = UiKit.Label(image.rectTransform, "Text", caption, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 4f), new Vector2(140f, 60f), 28, Cocoa);
            label.raycastTarget = false;
            return button;
        }

        void OnClaim(int index)
        {
            if (!app.Wardrobe.ClaimTask(index))
            {
                Sfx.Play(SfxId.Deny);
                return;
            }
            Sfx.Play(SfxId.Claim);
            TaskCatalog.Reward reward = TaskCatalog.Track[index];
            if (reward.items != null)
            {
                for (int i = 0; i < reward.items.Length; i++)
                {
                    ItemDef item = app.Database.Find(reward.items[i]);
                    if (item != null)
                        app.Wardrobe.Equip(item);
                }
            }
            Toast("领到了 " + TaskCatalog.Describe(reward, app.Database));
            changed?.Invoke();
            Fill();
        }
    }
}
