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
        const float TabW = 300f;
        const float TabH = 108f;
        const float TabPitch = 324f;
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

            for (int i = 0; i < TabNames.Length; i++)
            {
                Button tab = UiKit.Button(transform, TabNames[i], new Vector2(0.5f, 1f),
                    new Vector2((i - 1) * TabPitch, TabsY), new Vector2(TabW, TabH), Chip.White, 40, () => { });
                tabButtons[i] = tab;
                tabFaces[i] = tab.targetGraphic as Image;
                tabLabels[i] = tab.GetComponentInChildren<Text>();
            }
            ApplyTabs();

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

        /// <summary>
        /// 换上工坊自己的胶囊页签。换装衣橱的挂牌和这间缝纫间不是一套，预制里先放着的圆角块也换掉。
        /// </summary>
        public void ApplyTabs()
        {
            Sprite on = WorkshopTabSprite(true);
            Sprite off = WorkshopTabSprite(false);
            if (on == null || off == null || tabFaces == null) return;
            if (tabOn == null || tabOn.Length != tabFaces.Length)
                tabOn = new Sprite[tabFaces.Length];
            if (tabOff == null || tabOff.Length != tabFaces.Length)
                tabOff = new Sprite[tabFaces.Length];
            for (int i = 0; i < tabFaces.Length; i++)
            {
                if (tabFaces[i] == null) continue;
                tabOn[i] = on;
                tabOff[i] = off;
                RectTransform rect = tabFaces[i].rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(TabW, TabH);
                rect.anchoredPosition = new Vector2((i - 1) * TabPitch, TabsY);
                rect.localScale = Vector3.one;
                Text label = tabLabels != null && i < tabLabels.Length ? tabLabels[i] : null;
                if (label == null) continue;
                label.fontStyle = FontStyle.Bold;
                label.fontSize = 40;
                label.color = CraftView.Cocoa;
                label.alignment = TextAnchor.MiddleCenter;
                label.rectTransform.anchoredPosition = new Vector2(0f, 8f);
                label.rectTransform.sizeDelta = new Vector2(TabW - 48f, TabH - 28f);
                if (label.GetComponent<Outline>() == null)
                    CraftView.AddRim(label);
            }
        }

        public void SetTab(int index)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool on = i == index;
                if (tabFaces[i] != null && tabOff != null && i < tabOff.Length && tabOff[i] != null)
                {
                    Sprite skin = on && tabOn != null && i < tabOn.Length && tabOn[i] != null ? tabOn[i] : tabOff[i];
                    UiKit.FitPill(tabFaces[i], skin, tabFaces[i].rectTransform.sizeDelta.y);
                    tabFaces[i].color = Color.white;
                    tabFaces[i].rectTransform.localScale = Vector3.one;
                    if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                        tabLabels[i].color = CraftView.Cocoa;
                    continue;
                }
                if (tabFaces[i] != null)
                    tabFaces[i].sprite = UiKit.SpriteOf(i == index ? Chip.Teal : Chip.White);
                if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                    tabLabels[i].color = UiKit.TextOn(i == index ? Chip.Teal : Chip.White);
            }
        }

        static Sprite WorkshopTabSprite(bool on)
        {
            string file = on ? "tab_on" : "tab_off";
            Sprite sprite = Resources.Load<Sprite>("Ui/" + file);
#if UNITY_EDITOR
            if (sprite == null)
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/DressSort/Art/Ui/Workshop/" + file + ".png");
#endif
            return sprite;
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
            Sfx.BindClick(button, action);
        }
    }
}
