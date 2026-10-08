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

        static readonly Color Cream = new Color(1f, 0.95f, 0.84f);
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

            Image plateImage = UiKit.Slice(transform, "Plate", db != null ? db.rewardPlate : null,
                new Vector2(0.5f, 0.205f), Vector2.zero, new Vector2(840f, 140f), Color.white);
            UiKit.FitPill(plateImage, db != null ? db.rewardPlate : null, 140f);
            plate = plateImage.rectTransform;
            nameLabel = UiKit.Label(plate, "Name", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 6f),
                new Vector2(760f, 96f), 48, CraftView.Cocoa);
            nameLabel.fontStyle = FontStyle.Bold;
            FitText(nameLabel, 30, 48);

            hintLabel = UiKit.Label(transform, "Hint", "", new Vector2(0.5f, 0.148f), Vector2.zero,
                new Vector2(960f, 64f), 34, Cream);
            FitText(hintLabel, 24, 34);
            CraftView.AddRim(hintLabel, Wine, 2f);

            const float buttonH = 140f;
            takeButton = UiKit.Button(transform, "收下并试穿", new Vector2(0.5f, 0.075f), Vector2.zero,
                new Vector2(580f, buttonH), db != null ? db.rewardBtn : null, Color.white, 54, () => { });
            UiKit.FitPill(takeButton.targetGraphic as Image, db != null ? db.rewardBtn : null, buttonH);
            takeLabel = takeButton.GetComponentInChildren<Text>();
            takeLabel.fontStyle = FontStyle.Bold;
            takeLabel.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            CraftView.AddRim(takeLabel, Amber, 3f);

            fxLayer = UiKit.Stretch(transform, "Confetti");
            popperLeft = UiKit.Icon(transform, "PopperL", db != null ? db.rewardPopper : null,
                new Vector2(0f, 0.27f), new Vector2(130f, 0f), new Vector2(210f, 212f));
            popperRight = UiKit.Icon(transform, "PopperR", db != null ? db.rewardPopper : null,
                new Vector2(1f, 0.27f), new Vector2(-130f, 0f), new Vector2(210f, 212f));
            popperRight.transform.SetAsLastSibling();
            popperLeft.transform.SetAsLastSibling();
        }

        public void Wire(UnityAction onTap, UnityAction onTake)
        {
            Sfx.BindSilent(tapButton, onTap);
            Sfx.BindSilent(takeButton, onTake);
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
