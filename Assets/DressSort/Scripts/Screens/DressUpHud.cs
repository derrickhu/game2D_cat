using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 装扮页预制：紫藤庭院底、货架卡片、页签和保存都是切图。
    /// </summary>
    public class DressUpHud : MonoBehaviour
    {
        public const int Columns = 3;
        public const int MaxSlots = 9;

        public static readonly ItemSlot[] TabOrder =
        {
            ItemSlot.Dress, ItemSlot.Hair, ItemSlot.Wings,
        };

        public Image backdrop;
        public Button backButton;
        public Image title;
        public Image star;
        public Text starLabel;
        public RectTransform dollSlot;
        public Image[] tabs = new Image[3];
        public Button[] tabButtons = new Button[3];
        public Image shelf;
        public Image[] cards = new Image[MaxSlots];
        public Image[] icons = new Image[MaxSlots];
        public Button[] cardButtons = new Button[MaxSlots];
        public Button saveButton;

        public Sprite cardSprite;
        public Sprite cardOnSprite;
        public Sprite cardLockSprite;
        public Sprite[] tabOn = new Sprite[3];
        public Sprite[] tabOff = new Sprite[3];

        public static DressUpHud Assemble(RectTransform parent, GameDatabase db)
        {
            var go = new GameObject("DressUpScreen", typeof(RectTransform), typeof(DressUpHud));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var hud = go.GetComponent<DressUpHud>();
            hud.BuildChildren(db);
            return hud;
        }

        void BuildChildren(GameDatabase db)
        {
            backdrop = UiKit.Backdrop(transform, db != null ? db.dressupBg : null);
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = false;

            dollSlot = UiKit.Stretch(backdrop.transform, "DollSlot");
            dollSlot.pivot = new Vector2(0.5f, 0f);
            dollSlot.anchorMin = new Vector2(0.22f, 0.42f);
            dollSlot.anchorMax = new Vector2(0.78f, 0.82f);
            dollSlot.offsetMin = Vector2.zero;
            dollSlot.offsetMax = Vector2.zero;

            title = UiKit.Icon(transform, "Title", db != null ? db.dressupTitle : null,
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(400f, 108f));
            title.preserveAspect = true;

            Image back = UiKit.Icon(transform, "Back", db != null ? db.dressupBack : null,
                new Vector2(0f, 1f), new Vector2(88f, -96f), new Vector2(88f, 88f));
            back.preserveAspect = true;
            backButton = MakeHit(back);

            star = UiKit.Icon(transform, "Star", db != null ? db.iconStar : null,
                new Vector2(1f, 1f), new Vector2(-176f, -96f), new Vector2(52f, 52f));
            starLabel = UiKit.Label(transform, "Stars", "0", new Vector2(1f, 1f),
                new Vector2(-96f, -96f), new Vector2(80f, 52f), 36, Palette.Ink);

            tabOn = new[]
            {
                db != null ? db.dressupTabDressOn : null,
                db != null ? db.dressupTabHairOn : null,
                db != null ? db.dressupTabWingsOn : null,
            };
            tabOff = new[]
            {
                db != null ? db.dressupTabDressOff : null,
                db != null ? db.dressupTabHairOff : null,
                db != null ? db.dressupTabWingsOff : null,
            };

            const float shelfH = 760f;
            const float shelfY = 430f;
            float shelfTop = shelfY + shelfH * 0.5f;

            shelf = UiKit.Icon(transform, "Shelf", db != null ? db.dressupShelf : null,
                new Vector2(0.5f, 0f), new Vector2(0f, shelfY), new Vector2(1080f, shelfH));
            shelf.type = Image.Type.Sliced;
            shelf.preserveAspect = false;
            shelf.raycastTarget = false;

            cardSprite = db != null ? db.dressupCard : null;
            cardOnSprite = db != null ? db.dressupCardOn : null;
            cardLockSprite = db != null ? db.dressupCardLock : null;

            cards = new Image[MaxSlots];
            icons = new Image[MaxSlots];
            cardButtons = new Button[MaxSlots];
            const float cellW = 196f;
            const float cellH = 214f;
            const float gapX = 18f;
            const float gapY = 10f;
            const float topPad = 72f;
            float row0 = shelfH * 0.5f - topPad - cellH * 0.5f;
            for (int i = 0; i < MaxSlots; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                float x = (col - 1) * (cellW + gapX);
                float y = row0 - row * (cellH + gapY);
                Image card = UiKit.Slice(shelf.transform, "Card_" + i, cardSprite,
                    new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(cellW, cellH),
                    Color.white);
                card.raycastTarget = true;
                Image icon = UiKit.Icon(card.transform, "Icon", null,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(152f, 176f));
                icon.preserveAspect = true;
                cards[i] = card;
                icons[i] = icon;
                cardButtons[i] = MakeHit(card);
            }

            tabs = new Image[3];
            tabButtons = new Button[3];
            float[] tabX = { -300f, 0f, 300f };
            for (int i = 0; i < 3; i++)
            {
                Image tab = UiKit.Icon(transform, "Tab_" + i, tabOff[i],
                    new Vector2(0.5f, 0f), new Vector2(tabX[i], shelfTop + 8f),
                    new Vector2(268f, 150f));
                tab.preserveAspect = true;
                tabs[i] = tab;
                tabButtons[i] = MakeHit(tab);
            }

            Image save = UiKit.Icon(transform, "Save", db != null ? db.dressupSave : null,
                new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(560f, 104f));
            save.preserveAspect = true;
            saveButton = MakeHit(save);
        }

        static Button MakeHit(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        public void ShowTab(ItemSlot slot)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                bool on = TabOrder[i] == slot;
                Sprite sprite = on ? tabOn[i] : tabOff[i];
                if (sprite != null)
                    tabs[i].sprite = sprite;
            }
        }

        public void PaintCard(int index, Sprite icon, bool unlocked, bool equipped, bool empty)
        {
            if (index < 0 || index >= MaxSlots) return;
            Image card = cards[index];
            Image face = icons[index];
            if (card == null) return;
            if (empty)
            {
                card.gameObject.SetActive(false);
                return;
            }

            card.gameObject.SetActive(true);
            if (!unlocked)
            {
                card.sprite = cardLockSprite != null ? cardLockSprite : cardSprite;
                card.type = cardLockSprite != null ? Image.Type.Simple : Image.Type.Sliced;
                card.preserveAspect = false;
                if (face != null)
                    face.enabled = false;
                return;
            }

            card.type = Image.Type.Sliced;
            card.preserveAspect = false;
            card.sprite = equipped && cardOnSprite != null ? cardOnSprite : cardSprite;
            if (face != null)
            {
                face.enabled = icon != null;
                face.sprite = icon;
                face.color = Color.white;
            }
        }

        public void Wire(UnityAction onBack, UnityAction onSave, UnityAction<ItemSlot> onTab,
            UnityAction<int> onCard)
        {
            Bind(backButton, onBack);
            Bind(saveButton, onSave);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                ItemSlot slot = TabOrder[i];
                Bind(tabButtons[i], () => onTab?.Invoke(slot));
            }
            for (int i = 0; i < cardButtons.Length; i++)
            {
                int index = i;
                Bind(cardButtons[i], () => onCard?.Invoke(index));
            }
        }

        static void Bind(Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
