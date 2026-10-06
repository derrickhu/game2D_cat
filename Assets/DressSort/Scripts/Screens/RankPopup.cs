using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 通关排行榜：前三名挂金银铜花结，自己那一行钉在底部，列表滚到哪都看得见。
    /// 真机还没授权昵称时，底部多一个「使用微信昵称头像上榜」，上面盖微信原生按钮。
    /// </summary>
    public class RankPopup : PopupView
    {
        public RectTransform viewport;
        public RectTransform content;
        public float authSpace = 190f;
        public RankRowView rowPrefab;
        public RankRowView mineRow;
        public float rowStep = 150f;
        public Text statusLabel;
        public Button retryButton;
        public Button authButton;
        public Sprite[] medals = new Sprite[3];
        public Sprite rowIdle;
        public Sprite rowMine;

        Font nameFont;
        RankService.Board data;
        bool failed;
        bool authWait;

        protected override void OnOpen()
        {
            if (rowPrefab != null) rowPrefab.gameObject.SetActive(false);
            retryButton.onClick.AddListener(Reload);
            bool askProfile = WxBridge.CanAskProfile && !RankService.HasProfile;
            authButton.gameObject.SetActive(askProfile);
            if (!askProfile && viewport != null)
            {
                // 不用授权时把自己那行和列表一起往下挪，底部不留空。
                var mine = (RectTransform)mineRow.transform;
                mine.anchoredPosition -= new Vector2(0f, authSpace);
                viewport.offsetMin -= new Vector2(0f, authSpace);
            }
            if (askProfile)
            {
                authButton.onClick.AddListener(OnAuthTap);
                // 点开排行榜这一下就要把隐私弹窗调起来，晚了就不算用户点击。
                WxBridge.EnsurePrivacy(null);
                StartCoroutine(PlaceWhenReady());
            }

            nameFont = UiKit.Font;
            if (Application.isPlaying)
            {
                WxBridge.SystemFont(f =>
                {
                    if (this == null || f == null || f == nameFont) return;
                    nameFont = f;
                    Fill();
                });
            }
            Reload();
        }

        void OnDestroy()
        {
            WxBridge.HideProfileButton();
        }

        void SetStatus(string text, bool retry)
        {
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            statusLabel.text = text;
            retryButton.gameObject.SetActive(retry);
        }

        void Reload()
        {
            data = null;
            failed = false;
            SetStatus("排行榜加载中…", false);
            FillMine(null);
            if (!Application.isPlaying) return;
            RankService.Load(app.Wardrobe.LevelsCleared, (board, err) =>
            {
                if (this == null) return;
                if (board == null)
                {
                    failed = true;
                    Debug.LogWarning("[Rank] " + err);
                    SetStatus("网络不太好，排行榜没拉下来", true);
                }
                data = board;
                Fill();
            });
        }

        /// <summary>编辑器截图自检用：不走网络，直接塞一份榜。</summary>
        public void ShowPreview(RankRow[] list, RankRow mine)
        {
            data = new RankService.Board { List = list, Mine = mine };
            Fill();
        }

        void Fill()
        {
            if (data == null)
            {
                if (failed) FillMine(null);
                return;
            }
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                if (rowPrefab != null && child == rowPrefab.gameObject) continue;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            RankRow[] list = data.List;
            content.sizeDelta = new Vector2(content.sizeDelta.x, list.Length * rowStep + 16f);
            for (int i = 0; i < list.Length; i++)
            {
                RankRowView row = Instantiate(rowPrefab, content);
                row.gameObject.name = "Row" + (i + 1);
                row.gameObject.SetActive(true);
                var rect = (RectTransform)row.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -(8f + i * rowStep));
                row.Bind(list[i], false, medals, rowIdle, rowMine, nameFont);
            }
            SetStatus(list.Length == 0 ? "还没有人上榜，快去抢第一" : "", false);
            FillMine(data.Mine);
        }

        void FillMine(RankRow mine)
        {
            if (mine == null)
            {
                mine = new RankRow
                {
                    rank = 0,
                    cleared = app.Wardrobe.LevelsCleared,
                    displayName = RankService.HasProfile ? RankService.Nick : "我",
                    avatarUrl = RankService.Avatar,
                    isMe = true,
                };
            }
            mineRow.Bind(mine, true, medals, rowIdle, rowMine, nameFont);
        }

        void OnAuthTap()
        {
            if (WxBridge.PrivacyAgreed) return;
            WxBridge.EnsurePrivacy(null);
            StartCoroutine(PlaceWhenReady());
        }

        IEnumerator PlaceWhenReady()
        {
            if (authWait) yield break;
            authWait = true;
            if (!WxBridge.PrivacyAgreed)
            {
                bool settled = false, ok = false;
                WxBridge.EnsurePrivacy(agreed => { ok = agreed; settled = true; });
                float until = Time.realtimeSinceStartup + 20f;
                while (!settled && Time.realtimeSinceStartup < until) yield return null;
                if (!ok || this == null)
                {
                    authWait = false;
                    if (this != null) Toast("需同意隐私协议后才能用微信昵称上榜");
                    yield break;
                }
            }
            authWait = false;
            yield return null;
            if (authButton == null) yield break;
            WxBridge.ShowProfileButton(WxBridge.ScreenRect((RectTransform)authButton.transform), OnProfile);
        }

        void OnProfile(string nick, string avatar, string err)
        {
            if (this == null) return;
            if (string.IsNullOrEmpty(nick))
            {
                AuthFailed(err ?? "");
                return;
            }
            if (!RankService.SetProfile(nick, avatar))
            {
                Toast("昵称保存失败，请稍后再试");
                return;
            }
            WxBridge.HideProfileButton();
            authButton.gameObject.SetActive(false);
            Reload();
        }

        // 微信的失败分三种：之前点过拒绝、后台隐私指引没声明「用户信息」、其它。
        void AuthFailed(string err)
        {
            string e = err.ToLowerInvariant();
            if (e.Contains("privacy") || e.Contains("not declared") || e.StartsWith("112 "))
            {
                Toast("隐私指引未声明「用户信息」，暂时不能用昵称上榜");
                return;
            }
            if (e.Contains("deny") || e.Contains("auth"))
            {
                WxBridge.AskOpenSetting("之前拒绝过使用微信昵称和头像，去设置里打开「用户信息」后就能上榜。", ok =>
                {
                    if (this == null) return;
                    Toast(ok ? "已允许，再点一次就能上榜" : "没有打开「用户信息」，榜上会显示默认名字");
                });
                return;
            }
            Toast("没有拿到微信昵称");
        }
    }
}
