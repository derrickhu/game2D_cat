using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 试衣间装扮页。外观全在 Resources/Prefabs/DressUpScreen 预制里，这里只管换装数据。
    /// </summary>
    public class DressUpPanel : Panel
    {
        DressUpHud hud;
        PaperDoll doll;
        ItemSlot activeSlot = ItemSlot.Dress;
        readonly List<ItemDef> shown = new List<ItemDef>();

        protected override void Build()
        {
            Transform frameBg = transform.Find("Backdrop");
            if (frameBg != null)
                frameBg.gameObject.SetActive(false);

            GameObject prefab = Resources.Load<GameObject>("Prefabs/DressUpScreen");
            hud = prefab != null
                ? Instantiate(prefab, transform).GetComponent<DressUpHud>()
                : DressUpHud.Assemble((RectTransform)transform, app.Database);
            if (hud == null)
            {
                Debug.LogError("[一裙又一裙] 装扮预制没有 DressUpHud，先跑「重建装扮预制」");
                return;
            }

            var rect = (RectTransform)hud.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            // 底图和衣柜面板铺满全屏，只有顶栏要躲开刘海。
            if (hud.topBar != null)
                hud.topBar.SetParent(root, false);

            doll = PaperDoll.CreateFill(hud.dollSlot);
            // 脚超出基准时会伸进衣柜上沿，人偶画在面板上面，蕾丝才不会把鞋盖住。
            hud.dollSlot.SetAsLastSibling();
            hud.Wire(() => app.Show(ScreenId.Home), () => app.Show(ScreenId.Home), SwitchTo, OnPick);
        }

        public override void OnShow()
        {
            if (hud == null) return;
            if (hud.starLabel != null)
                hud.starLabel.text = app.Wardrobe.Stars.ToString();
            SwitchTo(activeSlot);
        }

        void SwitchTo(ItemSlot slot)
        {
            activeSlot = slot;
            hud.ShowTab(slot);

            shown.Clear();
            foreach (ItemDef item in app.Database.ItemsInSlot(slot))
            {
                if (app.Wardrobe.IsUnlocked(item))
                    shown.Add(item);
            }
            hud.LayoutCards(shown.Count);
            PaintCards();
            Redraw();
        }

        void PaintCards()
        {
            for (int i = 0; i < hud.SlotCount; i++)
            {
                ItemDef item = i < shown.Count ? shown[i] : null;
                if (item == null)
                {
                    hud.PaintCard(i, null, false, false, true);
                    continue;
                }
                bool unlocked = app.Wardrobe.IsUnlocked(item);
                bool equipped = app.Wardrobe.Equipped(activeSlot) == item;
                hud.PaintCard(i, unlocked ? item.ResolveIcon() : null, unlocked, equipped, false);
            }
        }

        void OnPick(int index)
        {
            if (index < 0 || index >= shown.Count) return;
            ItemDef item = shown[index];
            if (item == null || !app.Wardrobe.IsUnlocked(item))
            {
                Sfx.Play(SfxId.Deny);
                return;
            }
            Sfx.Play(SfxId.Equip);

            if (activeSlot == ItemSlot.Wings && app.Wardrobe.EquippedWings == item)
                app.Wardrobe.Unequip(ItemSlot.Wings);
            else
                app.Wardrobe.Equip(item);

            PaintCards();
            Redraw();
            if (doll != null)
                doll.Pop();
        }

        void Redraw()
        {
            if (doll != null)
                doll.Show(app.Wardrobe.EquippedDress, app.Wardrobe.EquippedWings,
                    app.Wardrobe.EquippedHair);
        }

    }
}
