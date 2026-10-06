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
            if (watchButton != null)
                watchButton.interactable = false;
            WxBridge.ShowRewarded(() =>
            {
                app.Wardrobe.GainEnergy(WardrobeService.EnergyPerLevel);
                changed?.Invoke();
                Action follow = Follow;
                Close();
                follow?.Invoke();
            }, () =>
            {
                if (watchButton != null)
                    watchButton.interactable = true;
                Toast("广告没看完");
            });
        }
    }
}
