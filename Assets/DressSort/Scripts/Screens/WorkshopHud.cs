using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 工坊页的预制：顶栏、三个页签、图纸卡滚动区和底部材料库存。
    /// 图纸卡随存档变化，由 WorkshopPanel 运行时往 content 里摆。
    /// </summary>
    public class WorkshopHud : MonoBehaviour
    {
        public const float CardW = 470f;
        public const float CardH = 630f;
        public const float GapX = 24f;
        public const float GapY = 26f;
        const float TabsY = -200f;
        const float ListTop = 290f;
        const float ListBottom = 290f;

        public static readonly string[] TabNames = { "裙子", "发型", "翅膀" };

        public Button backButton;
        public Button[] tabButtons = new Button[3];
        public Image[] tabFaces = new Image[3];
        public Text[] tabLabels = new Text[3];
        public Sprite[] tabOn = new Sprite[3];
        public Sprite[] tabOff = new Sprite[3];
        public ScrollRect scroll;
        public RectTransform content;
        public Text emptyLabel;
        public Image[] stockIcons = new Image[CraftCatalog.MatCount];
        public Text[] stockLabels = new Text[CraftCatalog.MatCount];
        public Text toastLabel;
        public Sprite cardSprite;

        public static WorkshopHud Assemble(RectTransform parent, GameDatabase db)
        {
            var go = new GameObject("WorkshopScreen", typeof(RectTransform), typeof(WorkshopHud));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var hud = go.GetComponent<WorkshopHud>();
            hud.BuildChildren(db);
            return hud;
        }

        void BuildChildren(GameDatabase db)
        {
            cardSprite = db != null ? db.craftCard : null;

            Image back = UiKit.Icon(transform, "Back", db != null ? db.dressupBack : null,
                new Vector2(0f, 1f), new Vector2(84f, -72f), new Vector2(96f, 104f));
            if (back.sprite == null)
                back.sprite = UiKit.SpriteOf(Chip.White);
            backButton = MakeHit(back);

            Text title = UiKit.Label(transform, "Title", "工坊", new Vector2(0.5f, 1f),
                new Vector2(0f, -72f), new Vector2(400f, 84f), 54, CraftView.Cocoa);
            title.fontStyle = FontStyle.Bold;
            CraftView.AddRim(title);

            if (db != null)
            {
                tabOn = new[] { db.dressupTabDressOn, db.dressupTabHairOn, db.dressupTabWingsOn };
                tabOff = new[] { db.dressupTabDressOff, db.dressupTabHairOff, db.dressupTabWingsOff };
            }
            for (int i = 0; i < TabNames.Length; i++)
            {
                if (tabOff[i] != null)
                {
                    Image sign = UiKit.Icon(transform, "Tab_" + TabNames[i], tabOff[i], new Vector2(0.5f, 1f),
                        new Vector2((i - 1) * 290f, TabsY), new Vector2(240f, 150f));
                    tabFaces[i] = sign;
                    tabButtons[i] = MakeHit(sign);
                    continue;
                }
                Button tab = UiKit.Button(transform, TabNames[i], new Vector2(0.5f, 1f),
                    new Vector2((i - 1) * 300f, TabsY), new Vector2(270f, 96f), Chip.White, 38, () => { });
                tabButtons[i] = tab;
                tabFaces[i] = tab.targetGraphic as Image;
                tabLabels[i] = tab.GetComponentInChildren<Text>();
            }

            var vpGo = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D),
                typeof(ScrollRect));
            vpGo.transform.SetParent(transform, false);
            var vp = (RectTransform)vpGo.transform;
            vp.anchorMin = new Vector2(0.5f, 0f);
            vp.anchorMax = new Vector2(0.5f, 1f);
            vp.pivot = new Vector2(0.5f, 1f);
            vp.sizeDelta = new Vector2(CardW * 2f + GapX + 24f, 0f);
            vp.offsetMin = new Vector2(vp.offsetMin.x, ListBottom);
            vp.offsetMax = new Vector2(vp.offsetMax.x, -ListTop);
            vpGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            content = UiKit.Rect(vp, "Cards", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(vp.sizeDelta.x, 0f));
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            scroll = vpGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            emptyLabel = UiKit.Label(vp, "Empty", "", new Vector2(0.5f, 0.6f), Vector2.zero,
                new Vector2(900f, 160f), 36, CraftView.Cocoa);
            CraftView.AddRim(emptyLabel);

            BuildStock(db);

            toastLabel = UiKit.Label(transform, "Toast", "", new Vector2(0.5f, 0f),
                new Vector2(0f, ListBottom - 30f), new Vector2(900f, 56f), 32, CraftView.Cocoa);
            CraftView.AddRim(toastLabel);
        }

        void BuildStock(GameDatabase db)
        {
            Image plate = UiKit.Slice(transform, "Stock", UiKit.SpriteOf(Chip.White), new Vector2(0.5f, 0f),
                new Vector2(0f, 140f), new Vector2(1020f, 190f), Color.white);
            if (db != null && db.rewardPlate != null)
                UiKit.FitPill(plate, db.rewardPlate, 190f);
            UiKit.Label(plate.transform, "Caption", "材料库存", new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(300f, 40f), 26, CraftView.Cocoa);
            const float pitch = 122f;
            for (int m = 0; m < CraftCatalog.MatCount; m++)
            {
                float x = (m - (CraftCatalog.MatCount - 1) * 0.5f) * pitch;
                stockIcons[m] = UiKit.Icon(plate.transform, "Icon_" + m, CraftView.MatIcon(db, m),
                    new Vector2(0.5f, 0.5f), new Vector2(x, -4f), new Vector2(78f, 78f));
                stockLabels[m] = UiKit.Label(plate.transform, "Count_" + m, "0", new Vector2(0.5f, 0.5f),
                    new Vector2(x, -62f), new Vector2(pitch, 36f), 28, CraftView.Cocoa);
                stockLabels[m].fontStyle = FontStyle.Bold;
            }
        }

        public void SetTab(int index)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool on = i == index;
                if (tabFaces[i] != null && tabOff[i] != null)
                {
                    tabFaces[i].sprite = on && tabOn[i] != null ? tabOn[i] : tabOff[i];
                    tabFaces[i].rectTransform.localScale = Vector3.one * (on ? 1.08f : 0.94f);
                    continue;
                }
                if (tabFaces[i] != null)
                    tabFaces[i].sprite = UiKit.SpriteOf(i == index ? Chip.Teal : Chip.White);
                if (tabLabels[i] != null)
                    tabLabels[i].color = UiKit.TextOn(i == index ? Chip.Teal : Chip.White);
            }
        }

        public void SetStock(WardrobeService wardrobe)
        {
            for (int m = 0; m < CraftCatalog.MatCount; m++)
            {
                if (stockLabels[m] != null)
                    stockLabels[m].text = wardrobe.MaterialCount((CraftMat)m).ToString();
            }
        }

        public void ShowToast(string message)
        {
            if (toastLabel != null)
                toastLabel.text = message;
        }

        public void Wire(UnityAction onBack, UnityAction<int> onTab)
        {
            Bind(backButton, onBack);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                Bind(tabButtons[i], () => onTab?.Invoke(index));
            }
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
    }
}
