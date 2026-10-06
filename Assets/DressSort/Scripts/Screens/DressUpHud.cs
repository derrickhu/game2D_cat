using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 装扮页预制：试衣间底图、人偶槽、顶栏，和底部带蕾丝边的衣柜面板（页签 + 可滚动卡片 + 保存）。
    /// 由「叠叠裙/重建装扮预制」生成，在预制里改位置即可，DressUpPanel 只填数据。
    /// </summary>
    public class DressUpHud : MonoBehaviour
    {
        public const int Columns = 4;
        public const int MaxSlots = 12;
        public const float CardW = 196f;
        public const float CardH = 211f;
        public const float GapX = 24f;
        public const float GapY = 16f;

        public static readonly ItemSlot[] TabOrder =
        {
            ItemSlot.Dress, ItemSlot.Hair, ItemSlot.Wings,
        };

        static readonly string[] TabNames = { "裙子", "发型", "翅膀" };
        static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        static readonly Color TabIdle = new Color32(0xF6, 0xE4, 0xDA, 0xFF);

        public Image backdrop;
        public RectTransform topBar;
        public Button backButton;
        public Text starLabel;
        public RectTransform dollSlot;
        public Image panel;
        public Image[] tabPills = new Image[3];
        public Text[] tabLabels = new Text[3];
        public Button[] tabButtons = new Button[3];
        public ScrollRect scroll;
        public RectTransform content;
        public Image[] faces = new Image[MaxSlots];
        public Image[] selected = new Image[MaxSlots];
        public Image[] icons = new Image[MaxSlots];
        public Button[] cardButtons = new Button[MaxSlots];
        public Button saveButton;

        public Sprite cardSprite;
        public Sprite cardLockSprite;

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
            const float panelH = 860f;
            const float laceH = 120f;

            // 底图比屏幕高，底边压到面板下面，地毯中心正好落在面板上沿附近。
            backdrop = UiKit.Icon(transform, "Backdrop", db != null ? db.dressupBg : null,
                new Vector2(0.5f, 0f), new Vector2(0f, 460f + 960f), new Vector2(1080f, 1920f));
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = false;

            dollSlot = UiKit.Rect(transform, "DollSlot", new Vector2(0.5f, 0f),
                new Vector2(0f, 830f + 389f), new Vector2(620f, 778f));

            panel = UiKit.Icon(transform, "Panel", db != null ? db.dressupPanel : null,
                new Vector2(0.5f, 0f), new Vector2(0f, panelH * 0.5f), new Vector2(1080f, panelH));
            panel.preserveAspect = false;
            panel.raycastTarget = true;
            var pr = panel.rectTransform;
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(1f, 0f);
            pr.sizeDelta = new Vector2(0f, panelH);

            float trackY = panelH - laceH - 22f - 48f;
            Image track = Capsule(panel.transform, "TabTrack", db != null ? db.dressupTabTrack : null,
                new Vector2(0f, trackY - panelH * 0.5f), new Vector2(900f, 96f));
            track.raycastTarget = false;
            tabPills = new Image[3];
            tabLabels = new Text[3];
            tabButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 290f;
                RectTransform hit = UiKit.Rect(track.transform, "Tab_" + i, new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(280f, 96f));
                var hitImage = hit.gameObject.AddComponent<Image>();
                hitImage.color = new Color(1f, 1f, 1f, 0f);
                tabPills[i] = Capsule(hit, "Pill", db != null ? db.dressupTabOn : null,
                    new Vector2(0f, 2f), new Vector2(276f, 80f));
                tabPills[i].raycastTarget = false;
                tabLabels[i] = Label(hit, TabNames[i], new Vector2(0f, 5f), new Vector2(276f, 70f), 36, TabIdle);
                tabButtons[i] = MakeHit(hitImage);
            }

            const float saveH = 116f;
            float saveY = 44f + saveH * 0.5f;
            float viewTop = trackY - 48f - 18f;
            float viewBottom = saveY + saveH * 0.5f + 16f;

            var vpGo = new GameObject("Shelf", typeof(RectTransform), typeof(Image), typeof(RectMask2D),
                typeof(ScrollRect));
            vpGo.transform.SetParent(panel.transform, false);
            var vp = (RectTransform)vpGo.transform;
            vp.anchorMin = new Vector2(0.5f, 0f);
            vp.anchorMax = new Vector2(0.5f, 0f);
            vp.pivot = new Vector2(0.5f, 0f);
            float viewW = Columns * CardW + (Columns - 1) * GapX + 24f;
            vp.sizeDelta = new Vector2(viewW, viewTop - viewBottom);
            vp.anchoredPosition = new Vector2(0f, viewBottom);
            vpGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            content = UiKit.Rect(vp, "Cards", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(viewW, 0f));
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            scroll = vpGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            cardSprite = db != null ? db.dressupCard : null;
            cardLockSprite = db != null ? db.dressupCardLock : null;
            Sprite onSprite = db != null ? db.dressupCardOn : null;
            faces = new Image[MaxSlots];
            selected = new Image[MaxSlots];
            icons = new Image[MaxSlots];
            cardButtons = new Button[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                float x = (col - (Columns - 1) * 0.5f) * (CardW + GapX);
                float y = -10f - CardH * 0.5f - row * (CardH + GapY);
                RectTransform cell = UiKit.Rect(content, "Card_" + i, new Vector2(0.5f, 1f),
                    new Vector2(x, y), new Vector2(CardW, CardH));
                var hit = cell.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);

                faces[i] = UiKit.Icon(cell, "Face", cardSprite, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(CardW, CardH));
                faces[i].preserveAspect = false;
                faces[i].raycastTarget = false;

                // card_on 多出右上角的勾，按卡身对齐：卡身宽 317/340，中心相对图片中心偏左下。
                float s = CardW / 317f;
                selected[i] = UiKit.Icon(cell, "Selected", onSprite, new Vector2(0.5f, 0.5f),
                    new Vector2(12f * s, 10.5f * s), new Vector2(340f * s, 363f * s));
                selected[i].preserveAspect = false;
                selected[i].raycastTarget = false;
                selected[i].gameObject.SetActive(false);

                icons[i] = UiKit.Icon(cell, "Icon", null, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f),
                    new Vector2(CardW - 40f, CardH - 46f));
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
                cardButtons[i] = MakeHit(hit);
            }
            LayoutCards(8);

            Image save = Capsule(panel.transform, "Save", db != null ? db.dressupSave : null,
                new Vector2(0f, saveY - panelH * 0.5f), new Vector2(440f, saveH));
            Text saveText = Label(save.transform, "保存搭配", new Vector2(0f, 7f), new Vector2(420f, 80f), 42,
                Color.white);
            var shadow = saveText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.29f, 0.08f, 0.14f, 0.7f);
            shadow.effectDistance = new Vector2(0f, -3f);
            saveButton = MakeHit(save);

            topBar = UiKit.Stretch(transform, "TopBar");
            Image back = UiKit.Icon(topBar, "Back", db != null ? db.dressupBack : null,
                new Vector2(0f, 1f), new Vector2(40f + 52f, -40f - 56f), new Vector2(104f, 112f));
            backButton = MakeHit(back);

            Image chip = Capsule(topBar, "StarChip", db != null ? db.dressupStarChip : null, Vector2.zero,
                new Vector2(210f, 90f));
            var cr = chip.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-40f - 105f, -40f - 56f);
            chip.raycastTarget = false;
            starLabel = Label(chip.transform, "0", new Vector2(28f, 3f), new Vector2(120f, 70f), 40, Color.white);

            ShowTab(ItemSlot.Dress);
        }

        /// <summary>按当前页签的件数撑开滚动内容，超过两行才能滑。</summary>
        public void LayoutCards(int count)
        {
            if (content == null) return;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)Columns));
            content.sizeDelta = new Vector2(content.sizeDelta.x, 20f + rows * CardH + (rows - 1) * GapY);
            content.anchoredPosition = Vector2.zero;
        }

        public void ShowTab(ItemSlot slot)
        {
            for (int i = 0; i < tabPills.Length; i++)
            {
                bool on = TabOrder[i] == slot;
                if (tabPills[i] != null)
                    tabPills[i].gameObject.SetActive(on);
                if (tabLabels[i] != null)
                    tabLabels[i].color = on ? Cocoa : TabIdle;
            }
        }

        public void PaintCard(int index, Sprite icon, bool unlocked, bool equipped, bool empty)
        {
            if (index < 0 || index >= MaxSlots || faces[index] == null) return;
            GameObject cell = faces[index].transform.parent.gameObject;
            if (empty)
            {
                cell.SetActive(false);
                return;
            }

            cell.SetActive(true);
            faces[index].sprite = unlocked ? cardSprite : cardLockSprite;
            faces[index].enabled = !equipped || !unlocked;
            selected[index].gameObject.SetActive(unlocked && equipped);
            icons[index].enabled = unlocked && icon != null;
            icons[index].sprite = icon;
            icons[index].color = Color.white;
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

        static Image Capsule(Transform parent, string name, Sprite sprite, Vector2 offset, Vector2 size)
        {
            Image image = UiKit.Icon(parent, name, sprite, new Vector2(0.5f, 0.5f), offset, size);
            image.preserveAspect = false;
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = sprite.rect.height / size.y;
            }
            return image;
        }

        static Text Label(Transform parent, string text, Vector2 offset, Vector2 size, int fontSize, Color color)
        {
            Text label = UiKit.Label(parent, "Text", text, new Vector2(0.5f, 0.5f), offset, size, fontSize, color);
            label.fontStyle = FontStyle.Normal;
            label.raycastTarget = false;
            return label;
        }

        static Button MakeHit(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
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
