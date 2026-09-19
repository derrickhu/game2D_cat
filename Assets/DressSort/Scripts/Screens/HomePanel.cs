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

            hud.Wire(OnStart, () => app.Show(ScreenId.DressUp), OnGear, OnWipe, OnSide);
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
            if (hud != null && hud.starLabel != null)
                hud.starLabel.text = app.Wardrobe.Stars.ToString();
            if (hud != null && hud.progressLabel != null)
                hud.progressLabel.text = $"已解锁 {app.Wardrobe.UnlockedCount} / {app.Wardrobe.TotalCount}";
        }

        void OnStart()
        {
            if (app.LevelCount > 1)
            {
                app.Show(ScreenId.LevelMap);
                return;
            }

            LevelDef only = app.LevelAt(1);
            if (only != null)
                app.StartLevel(only);
        }

        void OnWipe()
        {
            app.Wardrobe.Wipe();
            OnShow();
        }

        void OnGear()
        {
            Toast("设置稍后开放");
        }

        void OnSide(string id, string title)
        {
            Toast(title + " 即将开放");
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
