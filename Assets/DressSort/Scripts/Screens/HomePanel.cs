using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    public class HomePanel : Panel
    {
        HomeHud hud;
        PaperDoll doll;
        Coroutine toastRoutine;
        static readonly ItemSlot[] GmSlots = { ItemSlot.Dress, ItemSlot.Hair, ItemSlot.Wings };
        static readonly string[] GmSlotNames = { "裙子", "发型", "翅膀" };

        GameObject gmLayer;
        GameObject gmUnlock;
        RectTransform gmRows;
        Text gmNumber;
        int gmPick = 1;
        int gmSlot;

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
                Vector2.zero, new Vector2(680f, 760f));
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

            UiKit.Button(board.transform, "解锁装饰", new Vector2(0.5f, 0f),
                new Vector2(0f, 236f), new Vector2(440f, 72f), Chip.Teal, 32, OpenUnlock);
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

        void OpenUnlock()
        {
            if (gmUnlock != null) return;
            RectTransform layer = root.parent as RectTransform ?? root;
            var go = new GameObject("GmUnlock", typeof(RectTransform), typeof(Image));
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
            gmUnlock = go;

            Image board = UiKit.Icon(go.transform, "Board", UiKit.Rounded, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(860f, 1280f));
            board.type = Image.Type.Sliced;
            board.color = new Color(1f, 0.97f, 0.93f, 1f);
            board.raycastTarget = true;

            UiKit.Label(board.transform, "Title", "解锁装饰", new Vector2(0.5f, 1f),
                new Vector2(0f, -56f), new Vector2(400f, 64f), 40, Palette.Ink);
            for (int i = 0; i < GmSlotNames.Length; i++)
            {
                int index = i;
                UiKit.Button(board.transform, GmSlotNames[i], new Vector2(0.5f, 1f),
                    new Vector2((i - 1) * 220f, -150f), new Vector2(200f, 64f), Chip.White, 28,
                    () => ShowUnlockSlot(index));
            }
            UiKit.Button(board.transform, "本页全解锁", new Vector2(0.5f, 1f),
                new Vector2(0f, -230f), new Vector2(360f, 64f), Chip.Pink, 28, UnlockPage);

            var vpGo = new GameObject("Items", typeof(RectTransform), typeof(Image), typeof(RectMask2D),
                typeof(ScrollRect));
            vpGo.transform.SetParent(board.transform, false);
            var vp = (RectTransform)vpGo.transform;
            vp.anchorMin = new Vector2(0.5f, 1f);
            vp.anchorMax = new Vector2(0.5f, 1f);
            vp.pivot = new Vector2(0.5f, 1f);
            vp.sizeDelta = new Vector2(780f, 860f);
            vp.anchoredPosition = new Vector2(0f, -280f);
            var hit = vpGo.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            hit.raycastTarget = true;

            gmRows = UiKit.Rect(vp, "Rows", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(780f, 0f));
            gmRows.pivot = new Vector2(0.5f, 1f);
            var scroll = vpGo.GetComponent<ScrollRect>();
            scroll.content = gmRows;
            scroll.viewport = vp;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            UiKit.Button(board.transform, "关闭", new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(280f, 72f), Chip.White, 32, CloseUnlock);
            ShowUnlockSlot(gmSlot);
        }

        void ShowUnlockSlot(int index)
        {
            gmSlot = Mathf.Clamp(index, 0, GmSlots.Length - 1);
            RebuildUnlockRows();
        }

        void RebuildUnlockRows()
        {
            if (gmRows == null) return;
            for (int i = gmRows.childCount - 1; i >= 0; i--)
                UiKit.Discard(gmRows.GetChild(i));
            List<ItemDef> list = app.Database.ItemsInSlot(GmSlots[gmSlot]);
            const float rowH = 76f;
            for (int i = 0; i < list.Count; i++)
            {
                ItemDef item = list[i];
                float y = -12f - rowH * 0.5f - i * rowH;
                UiKit.Label(gmRows, "Name", item.displayName, new Vector2(0f, 1f),
                    new Vector2(220f, y), new Vector2(420f, 64f), 30, Palette.Ink, TextAnchor.MiddleLeft);
                if (app.Wardrobe.IsUnlocked(item))
                {
                    UiKit.Label(gmRows, "Owned", "已有", new Vector2(1f, 1f),
                        new Vector2(-110f, y), new Vector2(160f, 56f), 28, Palette.Caption);
                }
                else
                {
                    UiKit.Button(gmRows, "解锁", new Vector2(1f, 1f),
                        new Vector2(-110f, y), new Vector2(160f, 56f), Chip.Teal, 28, () => UnlockOne(item));
                }
            }
            gmRows.sizeDelta = new Vector2(780f, Mathf.Max(24f, 24f + list.Count * rowH));
            gmRows.anchoredPosition = Vector2.zero;
        }

        void UnlockOne(ItemDef item)
        {
            if (item == null || !app.Wardrobe.Unlock(item)) return;
            PaintUnlockCount();
            RebuildUnlockRows();
            Toast("已解锁「" + item.displayName + "」");
        }

        void UnlockPage()
        {
            int added = app.Wardrobe.GrantUnlocked(app.Database.ItemsInSlot(GmSlots[gmSlot]));
            PaintUnlockCount();
            RebuildUnlockRows();
            Toast(added == 0 ? "本页都已解锁" : "解锁了 " + added + " 件");
        }

        void PaintUnlockCount()
        {
            if (hud != null && hud.progressLabel != null)
                hud.progressLabel.text = $"已解锁 {app.Wardrobe.UnlockedCount} / {app.Wardrobe.TotalCount}";
        }

        void CloseUnlock()
        {
            if (gmUnlock == null) return;
            UiKit.Discard(gmUnlock.transform);
            gmUnlock = null;
            gmRows = null;
        }

        void CloseGm()
        {
            CloseUnlock();
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
