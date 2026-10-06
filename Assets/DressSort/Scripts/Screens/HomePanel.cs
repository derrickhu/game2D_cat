using System;
using System.Collections;
using UnityEngine;

namespace DressSort
{
    public class HomePanel : Panel
    {
        HomeHud hud;
        PaperDoll doll;
        Coroutine toastRoutine;

        protected override void Build()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/HomeScreen");
            if (prefab != null)
            {
                hud = Instantiate(prefab, root).GetComponent<HomeHud>();
            }
            else
            {
                hud = HomeHud.Assemble(root, app.Database);
            }

            if (hud == null)
            {
                Debug.LogError("[叠叠裙] 首页预制没有 HomeHud");
                return;
            }

            hud.Wire(OnStart, () => app.Show(ScreenId.DressUp), OnEnergyPlus, OnWipe, OnSide);
            hud.ApplyChromeLayout();

            if (root.parent is RectTransform frame)
                hud.BindStage(frame);

            if (hud.dollSlot != null)
                doll = PaperDoll.CreateFill(hud.dollSlot);
            else
                doll = PaperDoll.Create(root, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(640f, 780f));
        }

        public override void OnShow()
        {
            if (doll != null)
                doll.Show(app.Wardrobe.EquippedDress, app.Wardrobe.EquippedWings,
                    app.Wardrobe.EquippedHair);
            if (hud != null)
                hud.SetEnergy(app.Wardrobe.RecoverEnergy(), WardrobeService.MaxEnergy);
            if (hud != null && hud.progressLabel != null)
                hud.progressLabel.text = $"已解锁 {app.Wardrobe.UnlockedCount} / {app.Wardrobe.TotalCount}";
            if (!string.IsNullOrEmpty(app.Notice))
            {
                Toast(app.Notice);
                app.Notice = null;
            }
            if (app.OfferEnergyAd)
            {
                app.OfferEnergyAd = false;
                OpenEnergy(null);
            }
        }

        void OnStart()
        {
            LevelDef next = app.NextLevel;
            if (next == null)
            {
                Toast("全部 " + app.LevelCount + " 关都通关啦");
                return;
            }
            if (app.Wardrobe.RecoverEnergy() < WardrobeService.EnergyPerLevel)
            {
                OpenEnergy(() => app.StartLevel(next));
                return;
            }
            if (!app.StartLevel(next))
                OpenEnergy(() => app.StartLevel(next));
        }

        void OnEnergyPlus()
        {
            OpenEnergy(null);
        }

        void OpenEnergy(Action follow)
        {
            RectTransform layer = root.parent as RectTransform ?? root;
            EnergyAdPopup popup = PopupView.Open<EnergyAdPopup>("EnergyAdPopup", layer, app, OnShow);
            if (popup != null)
                popup.Follow = follow;
        }

        void OnWipe()
        {
            app.Wardrobe.Wipe();
            OnShow();
        }

        void OnSide(string id, string title)
        {
            RectTransform layer = root.parent as RectTransform ?? root;
            switch (id)
            {
                case "checkin":
                    PopupView.Open<CheckInPopup>("CheckInPopup", layer, app, OnShow);
                    break;
                case "rank":
                    PopupView.Open<RankPopup>("RankPopup", layer, app, OnShow);
                    break;
                case "circle":
                    PopupView.Open<ClubPopup>("ClubPopup", layer, app, OnShow);
                    break;
                case "event":
                    app.Show(ScreenId.Pack);
                    break;
                case "workshop":
                    if (app.Wardrobe.WorkshopOpen)
                        app.Show(ScreenId.Workshop);
                    else
                        Toast("通关第 " + CraftCatalog.WorkshopLevel + " 关后开放工坊");
                    break;
                case "quest":
                    PopupView.Open<QuestPopup>("QuestPopup", layer, app, OnShow);
                    break;
                default:
                    Toast(title + " 即将开放");
                    break;
            }
        }

        void Toast(string message)
        {
            if (hud == null) return;
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        IEnumerator ToastRoutine(string message)
        {
            hud.ShowToast(message);
            yield return new WaitForSeconds(1.4f);
            hud.ShowToast("");
            toastRoutine = null;
        }
    }
}
