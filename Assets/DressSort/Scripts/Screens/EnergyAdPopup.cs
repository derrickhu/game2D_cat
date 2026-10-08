using System;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>体力不够时弹出。看完广告恢复 1 点，够开一关。</summary>
    public class EnergyAdPopup : PopupView
    {
        public Button watchButton;

        public Action Follow { get; set; }

        protected override void OnOpen()
        {
            if (watchButton != null)
                watchButton.onClick.AddListener(OnWatch);
        }

        void OnWatch()
        {
            Sfx.Play(SfxId.Tap);
            if (watchButton != null)
                watchButton.interactable = false;
            if (!string.IsNullOrEmpty(WxBridge.RewardedAdUnitId))
                Analytics.AdRequest("energy", WxBridge.RewardedAdUnitId);
            WxBridge.ShowRewarded(() =>
            {
                if (!string.IsNullOrEmpty(WxBridge.RewardedAdUnitId))
                {
                    Analytics.AdShow("energy", WxBridge.RewardedAdUnitId);
                    Analytics.AdClose("energy", WxBridge.RewardedAdUnitId, true);
                }
                app.Wardrobe.GainEnergy(WardrobeService.EnergyPerLevel);
                Sfx.Play(SfxId.Heart);
                changed?.Invoke();
                Action follow = Follow;
                Close(false);
                follow?.Invoke();
            }, () =>
            {
                if (!string.IsNullOrEmpty(WxBridge.RewardedAdUnitId))
                    Analytics.AdError("energy", WxBridge.RewardedAdUnitId, "not_ended");
                if (watchButton != null)
                    watchButton.interactable = true;
                Sfx.Play(SfxId.Deny);
                Toast("广告没看完");
            });
        }
    }
}
