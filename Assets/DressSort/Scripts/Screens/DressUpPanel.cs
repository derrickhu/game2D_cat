using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 秀台装扮页：只用正式切图，按钮按原图比例摆，不再 9 切拉扁。
    /// </summary>
    public class DressUpPanel : Panel
    {
        const int Columns = 4;
        const int Rows = 2;
        const float TrayW = 1040f;
        const float BtnH = 100f;
        const float SaveH = 108f;
        const float CellW = 160f;

        class Slot
        {
            public Image card;
            public Image icon;
            public ItemDef item;
        }

        PaperDoll doll;
        readonly List<Slot> slots = new List<Slot>();
        readonly Dictionary<ItemSlot, Image> tabFaces = new Dictionary<ItemSlot, Image>();
        ItemSlot activeSlot = ItemSlot.Dress;
        Text starLabel;
        Sprite btnCream;
        Sprite btnPeach;
        Sprite card;
        Sprite cardOn;
        Sprite cardLock;

        protected override void Build()
        {
            GameDatabase db = app.Database;
            btnCream = db != null ? db.dressupBtnCream : null;
            btnPeach = db != null ? db.dressupBtnPeach : null;
            Sprite btnTeal = db != null ? db.dressupSave : null;
            Sprite traySprite = db != null ? db.dressupTray : null;
            card = db != null ? db.dressupCard : null;
            cardOn = db != null ? db.dressupCardOn : null;
            cardLock = db != null ? db.dressupCardLock : null;

            Vector2 topSize = FitHeight(btnCream, BtnH);
            Capsule(root, "返回", btnCream, new Vector2(0f, 1f), new Vector2(40f + topSize.x * 0.5f, -90f),
                topSize, Palette.Ink, 34, () => app.Show(ScreenId.Home));
            Capsule(root, "装扮", btnCream, new Vector2(0.5f, 1f), new Vector2(0f, -90f),
                topSize, Palette.Ink, 38, null);

            UiKit.Icon(root, "Star", db != null ? db.iconStar : null, new Vector2(1f, 1f),
                new Vector2(-160f, -90f), new Vector2(48f, 48f));
            starLabel = UiKit.Label(root, "Stars", "0", new Vector2(1f, 1f),
                new Vector2(-80f, -90f), new Vector2(80f, 56f), 34, Palette.Ink);

            doll = PaperDoll.Create(root, new Vector2(0.5f, 1f), new Vector2(0f, -620f),
                new Vector2(560f, 700f));

            Vector2 traySize = FitWidth(traySprite, TrayW);
            Image tray = Plate(root, "Tray", traySprite, new Vector2(0.5f, 0f),
                new Vector2(0f, 24f + traySize.y * 0.5f), traySize, false);

            Vector2 tabSize = FitHeight(btnCream, BtnH);
            float tabY = -tabSize.y * 0.42f;
            float tabSpan = tabSize.x + 18f;
            BuildTab(tray.transform, ItemSlot.Dress, "裙子", -tabSpan, tabY, tabSize, true);
            BuildTab(tray.transform, ItemSlot.Hair, "发型", 0f, tabY, tabSize, false);
            BuildTab(tray.transform, ItemSlot.Wings, "翅膀", tabSpan, tabY, tabSize, false);

            Vector2 cellSize = FitWidth(card, CellW);
            float gapX = 16f;
            float gapY = 12f;
            float gridTop = tabY - tabSize.y * 0.5f - 10f;
            for (int i = 0; i < Columns * Rows; i++)
            {
                int row = i / Columns;
                int col = i % Columns;
                float x = (col - (Columns - 1) * 0.5f) * (cellSize.x + gapX);
                float y = gridTop - cellSize.y * 0.5f - row * (cellSize.y + gapY);

                Image face = Plate(tray.transform, "Cell_" + i, card,
                    new Vector2(0.5f, 1f), new Vector2(x, y), cellSize, true);
                Image icon = UiKit.Icon(face.transform, "Icon", null, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(cellSize.x - 18f, cellSize.y - 16f));

                int index = i;
                UiKit.HitArea(face.transform, "Hit", new Vector2(0.5f, 0.5f), Vector2.zero,
                    cellSize, () => OnPick(index));
                slots.Add(new Slot { card = face, icon = icon });
            }

            Vector2 saveSize = FitHeight(btnTeal, SaveH);
            Capsule(tray.transform, "保存搭配", btnTeal, new Vector2(0.5f, 0f),
                new Vector2(0f, 20f + saveSize.y * 0.5f), saveSize, Color.white, 36,
                () => app.Show(ScreenId.Home));
        }

        void BuildTab(Transform parent, ItemSlot slot, string caption, float x, float y,
            Vector2 size, bool on)
        {
            Image face = Capsule(parent, caption, on ? btnPeach : btnCream,
                new Vector2(0.5f, 1f), new Vector2(x, y), size,
                on ? Color.white : Palette.Ink, 32, () => SwitchTo(slot));
            tabFaces[slot] = face;
        }

        public override void OnShow()
        {
            if (starLabel != null)
                starLabel.text = app.Wardrobe.Stars.ToString();
            SwitchTo(activeSlot);
        }

        void SwitchTo(ItemSlot slot)
        {
            activeSlot = slot;
            PaintTabs();

            List<ItemDef> list = app.Database.ItemsInSlot(slot);
            for (int i = 0; i < slots.Count; i++)
            {
                Slot cell = slots[i];
                cell.item = i < list.Count ? list[i] : null;
                PaintSlot(cell, slot);
            }

            Redraw();
        }

        void PaintTabs()
        {
            foreach (KeyValuePair<ItemSlot, Image> pair in tabFaces)
            {
                bool on = pair.Key == activeSlot;
                ApplySprite(pair.Value, on ? btnPeach : btnCream);
                Text text = pair.Value.GetComponentInChildren<Text>();
                if (text != null)
                    text.color = on ? Color.white : Palette.Ink;
            }
        }

        void PaintSlot(Slot cell, ItemSlot slot)
        {
            if (cell.item == null)
            {
                cell.card.gameObject.SetActive(false);
                return;
            }

            cell.card.gameObject.SetActive(true);
            bool unlocked = app.Wardrobe.IsUnlocked(cell.item);
            bool equipped = app.Wardrobe.Equipped(slot) == cell.item;

            if (!unlocked)
            {
                ApplySprite(cell.card, cardLock);
                cell.icon.enabled = false;
                return;
            }

            ApplySprite(cell.card, equipped ? cardOn : card);
            cell.icon.enabled = true;
            cell.icon.sprite = PreviewOf(cell.item);
            cell.icon.preserveAspect = true;
            cell.icon.color = Color.white;
        }

        void OnPick(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            Slot cell = slots[index];
            if (cell.item == null || !app.Wardrobe.IsUnlocked(cell.item)) return;

            if (activeSlot == ItemSlot.Wings && app.Wardrobe.EquippedWings == cell.item)
                app.Wardrobe.Unequip(ItemSlot.Wings);
            else
                app.Wardrobe.Equip(cell.item);

            for (int i = 0; i < slots.Count; i++)
                PaintSlot(slots[i], activeSlot);

            Redraw();
            if (doll != null)
                doll.Pop();
        }

        void Redraw()
        {
            if (doll != null)
                doll.Show(app.Wardrobe.EquippedDress, app.Wardrobe.EquippedWings,
                    app.Wardrobe.EquippedHair);
        }

        static Image Capsule(Transform parent, string caption, Sprite sprite, Vector2 anchor,
            Vector2 offset, Vector2 size, Color textColor, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            Image face = Plate(parent, "Btn_" + caption, sprite, anchor, offset, size, onClick != null);
            UiKit.Label(face.transform, "Text", caption, new Vector2(0.5f, 0.5f), Vector2.zero,
                size, fontSize, textColor);
            if (onClick == null) return face;

            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            return face;
        }

        static Image Plate(Transform parent, string name, Sprite sprite, Vector2 anchor,
            Vector2 offset, Vector2 size, bool raycast)
        {
            RectTransform rect = UiKit.Rect(parent, name, anchor, offset, size);
            var image = rect.gameObject.AddComponent<Image>();
            ApplySprite(image, sprite);
            image.raycastTarget = raycast;
            return image;
        }

        static void ApplySprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        static Sprite PreviewOf(ItemDef item)
        {
            if (item == null)
                return null;
            if (!string.IsNullOrEmpty(item.id))
            {
                Sprite preview = Resources.Load<Sprite>("WardrobePreview/" + item.id);
                if (preview != null)
                    return preview;
            }
            return item.ResolveIcon();
        }

        static Vector2 FitHeight(Sprite sprite, float height)
        {
            if (sprite == null || sprite.rect.height < 1f)
                return new Vector2(height * 2.74f, height);
            return new Vector2(height * sprite.rect.width / sprite.rect.height, height);
        }

        static Vector2 FitWidth(Sprite sprite, float width)
        {
            if (sprite == null || sprite.rect.width < 1f)
                return new Vector2(width, width * 0.5f);
            return new Vector2(width, width * sprite.rect.height / sprite.rect.width);
        }
    }
}
