using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 去游戏圈发一条帖子，回来领体力，每天一次。
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
        Coroutine poll;

        protected override void OnOpen()
        {
            WxBridge.EnsurePrivacy(null);
            goButton.onClick.AddListener(OnGo);
            claimButton.onClick.AddListener(OnClaim);
            if (rewardLabel != null)
                rewardLabel.text = "×" + WardrobeService.ClubReward;
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
            taskLabel.text = claimed ? "今日奖励已领取，明天再来" : "今日发帖  " + done + "/1";
            bool ready = !claimed && posts > 0;
            SetCapsule(claimButton, claimLabel, claimed ? "已领取" : "领取", ready ? claimReady : claimDone, ready,
                !claimed);
        }

        void OnGo()
        {
            // 真机上点到的是盖在上面的原生按钮，走不到这里。
            Toast("游戏圈要在微信里打开");
        }

        void OnClaim()
        {
            if (app.Wardrobe.ClubClaimedToday) { Toast("今天已经领过了"); return; }
            if (posts <= 0)
            {
                Toast(!string.IsNullOrEmpty(block) ? block : (asking ? "正在查询发帖记录…" : "先去游戏圈发一条帖子"));
                if (!asking) Ask();
                return;
            }
            if (!app.Wardrobe.ClaimClub()) return;
            Toast("已领取，体力 +" + WardrobeService.ClubReward);
            changed?.Invoke();
            Refresh();
        }
    }
}
