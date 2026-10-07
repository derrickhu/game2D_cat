using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    public class HomePanel : Panel
    {
        HomeHud hud;
        PaperDoll doll;
        Coroutine toastRoutine;
        GameObject gmLayer;
        Text gmNumber;
        int gmPick = 1;

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

            hud.ApplyChromeLayout();
            hud.Wire(OnStart, () => app.Show(ScreenId.DressUp), OnEnergyPlus, OnWipe, OpenGm, OnSide);

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
            if (hud != null)
            {
                LevelDef next = app.NextLevel;
                hud.SetStartCaption(next != null ? "第" + next.index + "关" : "已通关");
            }
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

        void OpenGm()
        {
            if (gmLayer != null) return;
            LevelDef next = app.NextLevel;
            gmPick = next != null ? next.index : 1;
            RectTransform layer = root.parent as RectTransform ?? root;
            var go = new GameObject("GmJump", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(layer, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            var dim = go.GetComponent<Image>();
            dim.sprite = UiKit.SoftRect;
            dim.type = Image.Type.Sliced;
            dim.color = new Color(0.2f, 0.12f, 0.14f, 0.45f);
            dim.raycastTarget = true;
            gmLayer = go;

            Image board = UiKit.Icon(go.transform, "Board", UiKit.Rounded, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(680f, 560f));
            board.type = Image.Type.Sliced;
            board.color = new Color(1f, 0.97f, 0.93f, 1f);
            board.raycastTarget = true;

            UiKit.Label(board.transform, "Title", "指定关卡", new Vector2(0.5f, 1f),
                new Vector2(0f, -48f), new Vector2(400f, 56f), 36, Palette.Ink);
            gmNumber = UiKit.Label(board.transform, "Number", "", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 36f), new Vector2(280f, 72f), 48, Palette.Ink);
            PaintGmNumber();

            int[] steps = { -100, -10, -1, 1, 10, 100 };
            for (int i = 0; i < steps.Length; i++)
            {
                int step = steps[i];
                float x = -250f + i * 100f;
                string caption = step > 0 ? "+" + step : step.ToString();
                UiKit.Button(board.transform, caption, new Vector2(0.5f, 0.5f),
                    new Vector2(x, -50f), new Vector2(92f, 64f), Chip.White, 26, () => ShiftGm(step));
            }

            UiKit.Button(board.transform, "开放工坊", new Vector2(0.5f, 0f),
                new Vector2(0f, 148f), new Vector2(440f, 72f), Chip.Pink, 32, OpenWorkshop);
            UiKit.Button(board.transform, "进入", new Vector2(0.5f, 0f),
                new Vector2(-120f, 56f), new Vector2(200f, 72f), Chip.Teal, 32, EnterGm);
            UiKit.Button(board.transform, "关闭", new Vector2(0.5f, 0f),
                new Vector2(120f, 56f), new Vector2(200f, 72f), Chip.White, 32, CloseGm);
        }

        void ShiftGm(int step)
        {
            gmPick = Mathf.Clamp(gmPick + step, 1, app.LevelCount);
            PaintGmNumber();
        }

        void PaintGmNumber()
        {
            if (gmNumber != null)
                gmNumber.text = "第 " + gmPick + " 关";
        }

        void EnterGm()
        {
            LevelDef level = app.LevelAt(gmPick);
            if (level == null)
            {
                Toast("没有第 " + gmPick + " 关");
                return;
            }
            CloseGm();
            app.StartGm(level);
        }

        void OpenWorkshop()
        {
            app.Wardrobe.UnlockWorkshop();
            CloseGm();
            app.Show(ScreenId.Workshop);
        }

        void CloseGm()
        {
            if (gmLayer == null) return;
            if (Application.isPlaying)
                Destroy(gmLayer);
            else
                DestroyImmediate(gmLayer);
            gmLayer = null;
            gmNumber = null;
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
