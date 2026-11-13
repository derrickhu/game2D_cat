using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 去游戏圈发一条帖子，回来领今天抽好的两格奖励，每天一次。转发可以换一次。
    /// 「去游戏圈」上盖着微信原生的透明按钮，点它直接进游戏圈；切回来时重查发帖数。
    /// </summary>
    public class ClubPopup : PopupView
    {
        static readonly float[] ShowPolls = { 0f, 1.5f, 4f, 8f };

        public Text taskLabel;
        public Text rewardLabel;
        public Button goButton;
        public Button claimButton;
        public Text claimLabel;
        public Sprite claimReady;
        public Sprite claimDone;

        string block;
        int posts = -1;
        bool asking;
        bool shareBusy;
        Coroutine poll;
        Coroutine shareWatch;
        Button refreshButton;

        protected override void OnOpen()
        {
            WxBridge.EnsurePrivacy(null);
            goButton.onClick.AddListener(OnGo);
            claimButton.onClick.AddListener(OnClaim);
            Text sub = board != null ? board.Find("Sub")?.GetComponent<Text>() : null;
            if (sub != null)
                sub.text = "每天发一条帖子，领取今天的奖励";
            PlaceChrome();
            PaintReward();
            Refresh();
            Ask();
            if (WxBridge.CanUseClub) StartCoroutine(PlaceClubButton());
        }

        void OnEnable()
        {
            GameClubService.Hook();
            GameClubService.Shown += OnBack;
        }

        void OnDisable()
        {
            GameClubService.Shown -= OnBack;
        }

        void OnDestroy()
        {
            WxBridge.HideClubButton();
        }

        IEnumerator PlaceClubButton()
        {
            yield return null;
            if (goButton == null) yield break;
            WxBridge.ShowClubButton(WxBridge.ScreenRect((RectTransform)goButton.transform));
        }

        // 发帖后游戏圈那边的统计有延迟，切回来多问几次。
        void OnBack()
        {
            if (this == null) return;
            if (poll != null) StopCoroutine(poll);
            poll = StartCoroutine(PollAfterShow());
        }

        IEnumerator PollAfterShow()
        {
            float last = 0f;
            foreach (float at in ShowPolls)
            {
                if (app.Wardrobe.ClubClaimedToday || posts > 0) yield break;
                if (at > last) yield return new WaitForSecondsRealtime(at - last);
                last = at;
                Ask();
            }
        }

        void Ask()
        {
            if (asking || app.Wardrobe.ClubClaimedToday) return;
            asking = true;
            GameClubService.DailyPosts((count, err) =>
            {
                asking = false;
                if (this == null) return;
                block = null;
                if (err != null)
                {
                    Debug.LogWarning("[GameClub] " + err);
                    if (err.IndexOf("隐私", StringComparison.Ordinal) >= 0)
                        block = "需同意隐私协议后才能同步发帖";
                }
                else posts = Mathf.Max(posts, count);
                Refresh();
            });
        }

        void Refresh()
        {
            bool claimed = app.Wardrobe.ClubClaimedToday;
            if (!claimed && posts <= 0 && !string.IsNullOrEmpty(block))
            {
                taskLabel.text = block;
                SetCapsule(claimButton, claimLabel, "领取", claimDone, false, true);
                return;
            }
            int done = claimed ? 1 : Mathf.Clamp(posts, 0, 1);
            taskLabel.text = claimed ? "" : "今日发帖  " + done + "/1";
            bool ready = !claimed && posts > 0;
            SetCapsule(claimButton, claimLabel, claimed ? "已领取" : "领取", ready ? claimReady : claimDone, ready,
                !claimed);
        }

        void PlaceChrome()
        {
            SetY(taskLabel != null ? taskLabel.rectTransform : null, -268f);
            SetY(goButton != null ? (RectTransform)goButton.transform : null, -368f);
            SetY(claimButton != null ? (RectTransform)claimButton.transform : null, -368f);
            if (refreshButton != null || board == null) return;
            refreshButton = UiKit.Button(board, "转发换一次", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -190f), new Vector2(420f, 84f), Chip.Pink, 30, OnRefresh);
        }

        static void SetY(RectTransform rect, float y)
        {
            if (rect == null) return;
            Vector2 pos = rect.anchoredPosition;
            pos.y = y;
            rect.anchoredPosition = pos;
        }

        void PaintReward()
        {
            WardrobeService.ClubOffer offer = app.Wardrobe.EnsureClubOffer();
            if (rewardLabel != null)
                rewardLabel.gameObject.SetActive(false);
            Transform card = rewardLabel != null ? rewardLabel.transform.parent : null;
            if (card == null) return;
            Transform oldEnergy = card.Find("Energy");
            if (oldEnergy != null) oldEnergy.gameObject.SetActive(false);
            Transform old = card.Find("ClubIcons");
            if (old != null) UiKit.Discard(old);
            if (!offer.ready)
            {
                SyncRefresh(offer);
                return;
            }

            var host = new GameObject("ClubIcons", typeof(RectTransform));
            host.transform.SetParent(card, false);
            var hostRect = (RectTransform)host.transform;
            hostRect.anchorMin = new Vector2(0.5f, 0.5f);
            hostRect.anchorMax = new Vector2(0.5f, 0.5f);
            hostRect.pivot = new Vector2(0.5f, 0.5f);
            hostRect.anchoredPosition = new Vector2(0f, -8f);
            hostRect.sizeDelta = new Vector2(210f, 160f);

            DrawOfferSlot(host.transform, -52f, offer.mat0, offer.count0);
            DrawOfferSlot(host.transform, 52f, offer.mat1, offer.count1);
            SyncRefresh(offer);
        }

        void DrawOfferSlot(Transform parent, float x, int mat, int count)
        {
            Sprite sprite = mat < 0
                ? (app.Database != null ? app.Database.iconHomeEnergy : null)
                : CraftView.MatIcon(app.Database, mat);
            DrawSlot(parent, x, sprite, count);
        }

        static void DrawSlot(Transform parent, float x, Sprite sprite, int count)
        {
            Image icon = UiKit.Icon(parent, "Icon", sprite, new Vector2(0.5f, 0.5f),
                new Vector2(x, 18f), new Vector2(76f, 76f));
            if (icon.sprite == null)
            {
                icon.sprite = UiKit.Circle;
                icon.color = new Color32(0xF2, 0xC1, 0x4A, 0xFF);
            }
            UiKit.Label(parent, "Count", "×" + count, new Vector2(0.5f, 0.5f),
                new Vector2(x, -36f), new Vector2(70f, 36f), 28, new Color32(0x3A, 0x1C, 0x35, 0xFF));
        }

        void SyncRefresh(WardrobeService.ClubOffer offer)
        {
            if (refreshButton == null) return;
            bool claimed = app.Wardrobe.ClubClaimedToday;
            refreshButton.gameObject.SetActive(!claimed);
            refreshButton.interactable = !claimed && !offer.refreshed && !shareBusy;
            Text label = refreshButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = offer.refreshed ? "已经换过" : "转发换一次";
        }

        void OnRefresh()
        {
            if (shareBusy || app.Wardrobe.ClubClaimedToday || app.Wardrobe.ClubRefreshUsed)
            {
                Sfx.Play(SfxId.Deny);
                if (app.Wardrobe.ClubRefreshUsed) Toast("今天已经换过了");
                return;
            }
            shareBusy = true;
            if (refreshButton != null) refreshButton.interactable = false;
            ShareCard card = ShareCards.Pick();
            if (shareWatch != null) StopCoroutine(shareWatch);
            shareWatch = StartCoroutine(UnlockShareLater());
            WxBridge.Share(card.title, card.image, () =>
            {
                shareBusy = false;
                if (shareWatch != null) StopCoroutine(shareWatch);
                if (this == null) return;
                if (!app.Wardrobe.RefreshClubOffer())
                {
                    PaintReward();
                    return;
                }
                PaintReward();
                Sfx.Play(SfxId.Heart);
            }, "gameclub_refresh");
        }

        IEnumerator UnlockShareLater()
        {
            yield return new WaitForSecondsRealtime(25f);
            shareBusy = false;
            if (this == null) yield break;
            PaintReward();
        }

        void OnGo()
        {
            // 真机上点到的是盖在上面的原生按钮，走不到这里。
            Toast("游戏圈要在微信里打开");
        }

        void OnClaim()
        {
            if (app.Wardrobe.ClubClaimedToday) { Sfx.Play(SfxId.Deny); Toast("今天已经领过了"); return; }
            if (posts <= 0)
            {
                Sfx.Play(SfxId.Deny);
                Toast(!string.IsNullOrEmpty(block) ? block : (asking ? "正在查询发帖记录…" : "先去游戏圈发一条帖子"));
                if (!asking) Ask();
                return;
            }
            WardrobeService.ClubOffer offer = app.Wardrobe.EnsureClubOffer();
            if (!app.Wardrobe.ClaimClub()) return;
            FlyOffer(offer);
            PaintReward();
            changed?.Invoke();
            Refresh();
        }

        void FlyOffer(WardrobeService.ClubOffer offer)
        {
            var bits = new RewardFly.Bit[2];
            Transform host = rewardLabel != null ? rewardLabel.transform.parent.Find("ClubIcons") : null;
            int seen = 0;
            if (host != null)
            {
                for (int i = 0; i < host.childCount && seen < 2; i++)
                {
                    Transform child = host.GetChild(i);
                    if (child.name != "Icon") continue;
                    bits[seen].world = child.position;
                    Image image = child.GetComponent<Image>();
                    if (image != null) bits[seen].sprite = image.sprite;
                    seen++;
                }
            }
            FillBit(ref bits[0], offer.mat0, offer.count0, seen > 0);
            FillBit(ref bits[1], offer.mat1, offer.count1, seen > 1);
            RewardFly.Play(this, bits, app.Database);
        }

        void FillBit(ref RewardFly.Bit bit, int mat, int count, bool placed)
        {
            bit.count = count;
            bit.energy = mat < 0;
            if (!placed) bit.world = transform.position;
            if (bit.sprite != null) return;
            bit.sprite = mat < 0
                ? (app.Database != null ? app.Database.iconHomeEnergy : null)
                : CraftView.MatIcon(app.Database, mat);
        }
    }
}
