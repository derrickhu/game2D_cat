using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>七日签到。断签一天从第 1 天重来，七天签满下一天也从头开始。</summary>
    public class CheckInPopup : PopupView
    {
        public Image[] cards = new Image[WardrobeService.CheckInDays];
        public Text[] amounts = new Text[WardrobeService.CheckInDays];
        public Image[] stamps = new Image[WardrobeService.CheckInDays];
        public Text[] dayLabels = new Text[WardrobeService.CheckInDays];
        public Color labelIdle = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        public Sprite cardIdle;
        public Sprite cardToday;
        public Sprite cardDone;
        public Sprite wideIdle;
        public Sprite wideToday;

        public Button signButton;
        public Text signLabel;
        public Sprite signReady;
        public Sprite signDone;

        Button doubleButton;
        bool adBusy;

        protected override void OnOpen()
        {
            signButton.onClick.AddListener(OnSign);
            Text sub = board != null ? board.Find("Sub")?.GetComponent<Text>() : null;
            if (sub != null)
                sub.text = "连续签到领材料，断签从第 1 天重来";
            Refresh();
        }

        void Refresh()
        {
            WardrobeService w = app.Wardrobe;
            int run = w.CheckInRun;
            bool done = w.CheckedInToday;
            int today = done ? run : run + 1;

            for (int i = 0; i < WardrobeService.CheckInDays; i++)
            {
                int day = i + 1;
                bool wide = i == WardrobeService.CheckInDays - 1;
                bool isToday = !done && day == today;
                bool claimed = day <= run;
                if (cards[i] != null)
                    cards[i].sprite = wide
                        ? (isToday ? wideToday : wideIdle)
                        : (isToday ? cardToday : claimed && cardDone != null ? cardDone : cardIdle);
                PaintReward(i, w.PreviewCheckIn(day));
                if (stamps[i] != null)
                    stamps[i].gameObject.SetActive(claimed);
                if (!wide && dayLabels[i] != null)
                    dayLabels[i].color = isToday ? Color.white : labelIdle;
            }

            SetCapsule(signButton, signLabel, done ? "明天再来" : "签到", done ? signDone : signReady, !done, !done);
            RefreshDouble(w, done, today);
        }

        /// <summary>签完才出现。第一次第 7 天的头发图纸不翻倍。</summary>
        void RefreshDouble(WardrobeService w, bool done, int today)
        {
            bool hair = done && w.PreviewCheckIn(today).blueprint;
            bool show = done && !hair && !w.CheckInDoubledToday;
            EnsureDouble();
            if (doubleButton == null) return;
            doubleButton.gameObject.SetActive(show);
            doubleButton.interactable = show && !adBusy;
            if (show)
                doubleButton.transform.SetAsLastSibling();
        }

        void EnsureDouble()
        {
            if (doubleButton != null || signButton == null) return;
            RectTransform sign = signButton.transform as RectTransform;
            doubleButton = UiKit.Button(sign.parent, "看广告 翻倍", sign.anchorMin,
                sign.anchoredPosition + new Vector2(0f, -sign.sizeDelta.y * 0.5f - 64f),
                new Vector2(sign.sizeDelta.x, 108f),
                AdButton.Face(app.Database), Color.white, 32, OnDouble);
            RectTransform rect = doubleButton.transform as RectTransform;
            rect.anchorMax = sign.anchorMax;
            rect.pivot = sign.pivot;
            AdButton.Apply(doubleButton, app.Database);
            Text label = doubleButton.GetComponentInChildren<Text>();
            if (label != null)
                label.fontStyle = FontStyle.Bold;
        }

        void OnDouble()
        {
            if (adBusy || app.Wardrobe == null || !app.Wardrobe.CheckedInToday) return;
            if (app.Wardrobe.CheckInDoubledToday || app.Wardrobe.PreviewCheckIn(app.Wardrobe.CheckInRun).blueprint)
            {
                Sfx.Play(SfxId.Deny);
                return;
            }
            adBusy = true;
            if (doubleButton != null) doubleButton.interactable = false;
            const string scene = "checkin_double";
            Analytics.AdRequest(scene, WxBridge.CheckInAdUnitId);
            WxBridge.ShowRewarded(WxBridge.CheckInAdUnitId, () =>
            {
                adBusy = false;
                if (this == null || app == null || app.Wardrobe == null) return;
                if (!app.Wardrobe.DoubleCheckIn())
                {
                    Refresh();
                    return;
                }
                Analytics.AdShow(scene, WxBridge.CheckInAdUnitId);
                Analytics.AdClose(scene, WxBridge.CheckInAdUnitId, true);
                FlyGrant(app.Wardrobe.CheckInRun, app.Wardrobe.LastCheckIn);
                changed?.Invoke();
                Refresh();
                Toast("材料已翻倍");
            }, () =>
            {
                adBusy = false;
                if (this == null) return;
                Analytics.AdError(scene, WxBridge.CheckInAdUnitId, "not_ended");
                Sfx.Play(SfxId.Deny);
                Refresh();
                Toast("广告没看完");
            });
        }

        void PaintReward(int index, WardrobeService.CheckInGrant grant)
        {
            Image icon = cards[index] != null ? cards[index].transform.Find("Energy")?.GetComponent<Image>() : null;
            Image mark = icon != null ? icon.transform.Find("Mark")?.GetComponent<Image>() : null;
            bool wide = index == WardrobeService.CheckInDays - 1;
            if (grant.blueprint)
            {
                if (icon == null) return;
                icon.sprite = Resources.Load<Sprite>("Ui/blueprint_paper");
                icon.preserveAspect = false;
                icon.color = Color.white;
                icon.rectTransform.sizeDelta = new Vector2(72f, 96f);
                if (wide)
                    icon.rectTransform.anchoredPosition = new Vector2(-48f, 2f);
                if (mark == null)
                    mark = UiKit.Icon(icon.transform, "Mark", null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
                ItemDef hair = app.Database.Find(WardrobeService.CheckInPrizeId);
                mark.sprite = hair != null ? hair.ResolveIcon() : null;
                mark.preserveAspect = true;
                mark.gameObject.SetActive(mark.sprite != null);
                RectTransform picture = mark.rectTransform;
                picture.anchorMin = new Vector2(0.06f, 0.04f);
                picture.anchorMax = new Vector2(0.94f, 0.96f);
                picture.offsetMin = Vector2.zero;
                picture.offsetMax = Vector2.zero;
                if (amounts[index] != null)
                {
                    string name = hair != null && !string.IsNullOrEmpty(hair.displayName) ? hair.displayName : "粉红马尾";
                    amounts[index].gameObject.SetActive(true);
                    amounts[index].text = name + "图纸";
                    amounts[index].fontSize = 32;
                    amounts[index].color = new Color32(0x4A, 0x26, 0x2C, 0xFF);
                    amounts[index].rectTransform.anchoredPosition = new Vector2(128f, 2f);
                    amounts[index].rectTransform.sizeDelta = new Vector2(250f, 56f);
                }
                return;
            }

            if (mark != null)
                mark.gameObject.SetActive(false);
            if (amounts[index] != null)
            {
                amounts[index].gameObject.SetActive(true);
                amounts[index].text = "×" + grant.count;
                amounts[index].fontSize = wide ? 52 : 34;
                amounts[index].color = wide ? new Color32(0xD0, 0x40, 0x5E, 0xFF) : new Color32(0x3A, 0x1C, 0x35, 0xFF);
                if (wide)
                {
                    amounts[index].rectTransform.anchoredPosition = new Vector2(90f, 8f);
                    amounts[index].rectTransform.sizeDelta = new Vector2(160f, 70f);
                }
            }
            if (icon == null) return;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.sprite = CraftView.MatIcon(app.Database, grant.mat);
            icon.rectTransform.sizeDelta = wide ? new Vector2(64f, 100f) : new Vector2(60f, 94f);
            if (wide)
                icon.rectTransform.anchoredPosition = new Vector2(-20f, 10f);
        }

        void OnSign()
        {
            int day = app.Wardrobe.CheckIn();
            if (day <= 0)
            {
                Sfx.Play(SfxId.Deny);
                Toast("今天已经签过了");
                return;
            }
            Analytics.CheckIn(day);
            FlyGrant(day, app.Wardrobe.LastCheckIn);
            changed?.Invoke();
            Refresh();
        }

        void FlyGrant(int day, WardrobeService.CheckInGrant grant)
        {
            int index = day - 1;
            RectTransform from = null;
            Sprite sprite = null;
            if (cards != null && index >= 0 && index < cards.Length && cards[index] != null)
            {
                Transform energy = cards[index].transform.Find("Energy");
                if (grant.blueprint && energy != null)
                {
                    Transform mark = energy.Find("Mark");
                    if (mark is RectTransform markRect && mark.gameObject.activeInHierarchy)
                    {
                        from = markRect;
                        Image markImage = mark.GetComponent<Image>();
                        if (markImage != null) sprite = markImage.sprite;
                    }
                }
                if (from == null && energy is RectTransform energyRect)
                {
                    from = energyRect;
                    Image image = energy.GetComponent<Image>();
                    if (image != null) sprite = image.sprite;
                }
            }
            if (sprite == null)
            {
                if (grant.blueprint)
                {
                    ItemDef hair = app.Database != null ? app.Database.Find(WardrobeService.CheckInPrizeId) : null;
                    if (hair != null) sprite = hair.ResolveIcon();
                }
                else
                    sprite = CraftView.MatIcon(app.Database, grant.mat);
            }
            RewardFly.Play(this, new[]
            {
                new RewardFly.Bit
                {
                    sprite = sprite,
                    count = grant.blueprint ? 0 : grant.count,
                    world = from != null ? from.position : transform.position,
                },
            }, app.Database);
        }
    }
}
