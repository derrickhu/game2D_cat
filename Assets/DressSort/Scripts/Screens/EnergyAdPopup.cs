using System;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>体力不够时弹出。看完一条体力广告加 5 点。</summary>
    public class EnergyAdPopup : PopupView
    {
        public Button watchButton;

        public Action Follow { get; set; }

        protected override void OnOpen()
        {
            if (watchButton != null)
                watchButton.onClick.AddListener(OnWatch);
            AdButton.Apply(watchButton, app != null ? app.Database : null);
            Text[] labels = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].gameObject.name != "Body") continue;
                labels[i].text = "看完一条广告，恢复 " + WardrobeService.EnergyAdReward + " 点体力";
            }
        }

        void OnWatch()
        {
            Sfx.Play(SfxId.Tap);
            if (watchButton != null)
                watchButton.interactable = false;
            Analytics.AdRequest("energy", WxBridge.EnergyAdUnitId);
            WxBridge.ShowRewarded(WxBridge.EnergyAdUnitId, () =>
            {
                Analytics.AdShow("energy", WxBridge.EnergyAdUnitId);
                Analytics.AdClose("energy", WxBridge.EnergyAdUnitId, true);
                app.Wardrobe.GainEnergy(WardrobeService.EnergyAdReward);
                FlyEnergy();
                changed?.Invoke();
                Action follow = Follow;
                Close(false);
                follow?.Invoke();
            }, () =>
            {
                Analytics.AdError("energy", WxBridge.EnergyAdUnitId, "not_ended");
                if (watchButton != null)
                    watchButton.interactable = true;
                Sfx.Play(SfxId.Deny);
                Toast("广告没看完");
            });
        }

        void FlyEnergy()
        {
            Sprite sprite = app.Database != null ? app.Database.iconHomeEnergy : null;
            Vector3 world = watchButton != null ? watchButton.transform.position : transform.position;
            RewardFly.Play(this, new[]
            {
                new RewardFly.Bit
                {
                    sprite = sprite,
                    count = WardrobeService.EnergyAdReward,
                    world = world,
                    energy = true,
                },
            }, app.Database);
        }
    }
}
