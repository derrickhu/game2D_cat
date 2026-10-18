using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 领奖页预制：红幕、礼盒、光芒、名字牌和收下按钮。
    /// 由「一裙又一裙/重建领奖预制」生成，在预制里改位置即可，RewardPanel 只播动画和填文案。
    /// </summary>
    public class RewardHud : MonoBehaviour
    {
        public const float PodiumY = 0.335f;
        public const float GiftW = 340f;
        public const float DressSize = 340f;

        static readonly Color Wine = new Color(0.5f, 0.1f, 0.14f);
        static readonly Color Amber = new Color(0.56f, 0.3f, 0.06f);

        public Image backdrop;
        public RectTransform stage;
        public RectTransform gift;
        public Image closed;
        public Image boxBase;
        public Image lid;
        public Image rays;
        public Image dress;
        public Image blueprintIcon;
        public RectTransform plate;
        public Text nameLabel;
        public Text hintLabel;
        public Button takeButton;
        public Text takeLabel;
        public Button continueButton;
        public Text continueLabel;
        public RectTransform fxLayer;
        public Image popperLeft;
        public Image popperRight;
        public Button tapButton;

        public static RewardHud Assemble(RectTransform parent, GameDatabase db)
        {
            var go = new GameObject("RewardScreen", typeof(RectTransform), typeof(RewardHud));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var hud = go.GetComponent<RewardHud>();
            hud.BuildChildren(db);
            return hud;
        }

        void BuildChildren(GameDatabase db)
        {
            backdrop = UiKit.Icon(transform, "Backdrop", db != null ? db.rewardBg : null,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1920f));
            var bg = backdrop.rectTransform;
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = true;

            Image ribbon = UiKit.Icon(transform, "Ribbon", db != null ? db.rewardRibbon : null,
                new Vector2(0.5f, 0.8f), Vector2.zero, new Vector2(780f, 232f));
            Text title = UiKit.Label(ribbon.rectTransform, "Title", "整理完成", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 14f), new Vector2(600f, 110f), 66, Color.white);
            title.fontStyle = FontStyle.Bold;
            CraftView.AddRim(title, Wine, 3f);

            stage = UiKit.Rect(transform, "Stage", new Vector2(0.5f, PodiumY), Vector2.zero, Vector2.zero);
            rays = UiKit.Icon(stage, "Rays", db != null ? db.rewardRays : null, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 300f), new Vector2(980f, 980f));

            gift = UiKit.Rect(stage, "Gift", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Sprite closedSprite = db != null ? db.rewardGiftClosed : null;
            float k = closedSprite != null ? GiftW / closedSprite.rect.width : 1f;
            Vector2 closedSize = SizeOf(closedSprite, k, new Vector2(GiftW, GiftW));
            Vector2 baseSize = SizeOf(db != null ? db.rewardGiftBase : null, k, new Vector2(GiftW, GiftW));
            Vector2 lidSize = SizeOf(db != null ? db.rewardGiftLid : null, k, new Vector2(GiftW, GiftW * 0.6f));
            boxBase = UiKit.Icon(gift, "Base", db != null ? db.rewardGiftBase : null, new Vector2(0.5f, 0.5f),
                new Vector2(0f, baseSize.y * 0.5f), baseSize);
            dress = UiKit.Icon(gift, "Dress", null, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(DressSize, DressSize));
            blueprintIcon = UiKit.Icon(dress.transform, "BlueprintIcon", null, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 6f), new Vector2(170f, 170f));
            closed = UiKit.Icon(gift, "Closed", closedSprite, new Vector2(0.5f, 0.5f),
                new Vector2(0f, closedSize.y * 0.5f), closedSize);
            lid = UiKit.Icon(gift, "Lid", db != null ? db.rewardGiftLid : null, new Vector2(0.5f, 0.5f),
                Vector2.zero, lidSize);

            tapButton = UiKit.HitArea(stage, "Tap", new Vector2(0.5f, 0.5f), new Vector2(0f, 300f),
                new Vector2(760f, 860f), () => { });

            // 名字是纯文字，不铺底牌，免得挡住裙子。位置在横幅下沿和裙子之间。
            Image plateImage = UiKit.Slice(transform, "Plate", null,
                new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(920f, 108f), Color.white);
            plateImage.raycastTarget = false;
            plateImage.enabled = false;
            plate = plateImage.rectTransform;
            nameLabel = UiKit.Label(plate, "Name", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 26f),
                new Vector2(900f, 64f), 52, Color.white);
            hintLabel = UiKit.Label(plate, "Hint", "", new Vector2(0.5f, 0.5f), new Vector2(0f, -28f),
                new Vector2(900f, 40f), 30, new Color(1f, 0.96f, 0.88f));
            StyleCaption();

            takeButton = MakeButton("收下并试穿", 0.128f, db, out takeLabel);
            continueButton = MakeButton("继续下一关", 0.052f, db, out continueLabel);

            fxLayer = UiKit.Stretch(transform, "Confetti");
            popperLeft = UiKit.Icon(transform, "PopperL", db != null ? db.rewardPopper : null,
                new Vector2(0f, 0.27f), new Vector2(130f, 0f), new Vector2(210f, 212f));
            popperRight = UiKit.Icon(transform, "PopperR", db != null ? db.rewardPopper : null,
                new Vector2(1f, 0.27f), new Vector2(-130f, 0f), new Vector2(210f, 212f));
            popperRight.transform.SetAsLastSibling();
            popperLeft.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 旧预制还是米色名牌。进页面时改成横幅下的纯文字，缺的继续按钮补上。
        /// </summary>
        public void ApplyChrome(GameDatabase db)
        {
            StyleCaption();
            PlaceButton(takeButton, 0.128f, db);
            if (continueButton == null)
                continueButton = MakeButton("继续下一关", 0.052f, db, out continueLabel);
            else
                PlaceButton(continueButton, 0.052f, db);
        }

        public void Wire(UnityAction onTap, UnityAction onTake, UnityAction onContinue)
        {
            Sfx.BindSilent(tapButton, onTap);
            Sfx.BindSilent(takeButton, onTake);
            Sfx.BindSilent(continueButton, onContinue);
        }

        void StyleCaption()
        {
            if (plate != null)
            {
                plate.anchorMin = new Vector2(0.5f, 0.70f);
                plate.anchorMax = new Vector2(0.5f, 0.70f);
                plate.anchoredPosition = Vector2.zero;
                plate.sizeDelta = new Vector2(920f, 108f);
                Image image = plate.GetComponent<Image>();
                if (image != null)
                {
                    image.enabled = false;
                    image.raycastTarget = false;
                }
            }
            if (hintLabel != null && plate != null && hintLabel.transform.parent != plate)
                hintLabel.transform.SetParent(plate, false);
            StyleLine(nameLabel, Color.white, 52, 32, new Vector2(0f, 26f), new Vector2(900f, 64f), 3f);
            StyleLine(hintLabel, new Color(1f, 0.96f, 0.88f), 30, 22, new Vector2(0f, -28f), new Vector2(900f, 40f), 2f);
        }

        void StyleLine(Text label, Color color, int size, int min, Vector2 offset, Vector2 box, float rim)
        {
            if (label == null) return;
            label.color = color;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = offset;
            label.rectTransform.sizeDelta = box;
            FitText(label, min, size);
            Outline outline = label.GetComponent<Outline>();
            if (outline == null)
                outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = Wine;
            outline.effectDistance = new Vector2(rim, -rim);
        }

        static void PlaceButton(Button button, float anchorY, GameDatabase db)
        {
            if (button == null) return;
            const float buttonH = 124f;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.5f, anchorY);
            rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(580f, buttonH);
            UiKit.FitPill(button.targetGraphic as Image, db != null ? db.rewardBtn : null, buttonH);
            Text label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            label.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            label.rectTransform.sizeDelta = new Vector2(540f, 100f);
            label.fontSize = 46;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 30;
            label.resizeTextMaxSize = 46;
        }

        Button MakeButton(string caption, float anchorY, GameDatabase db, out Text label)
        {
            const float buttonH = 124f;
            Button button = UiKit.Button(transform, caption, new Vector2(0.5f, anchorY), Vector2.zero,
                new Vector2(580f, buttonH), db != null ? db.rewardBtn : null, Color.white, 46, () => { });
            UiKit.FitPill(button.targetGraphic as Image, db != null ? db.rewardBtn : null, buttonH);
            label = button.GetComponentInChildren<Text>();
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            FitText(label, 30, 46);
            CraftView.AddRim(label, Amber, 3f);
            return button;
        }

        static Vector2 SizeOf(Sprite sprite, float k, Vector2 fallback) =>
            sprite != null ? sprite.rect.size * k : fallback;

        static void FitText(Text label, int min, int max)
        {
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = min;
            label.resizeTextMaxSize = max;
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
