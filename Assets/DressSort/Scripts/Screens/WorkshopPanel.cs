using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 工坊：按页签列出拿到的图纸，每张写着要哪些材料、现在有多少，够了就能做。
    /// 外观在 Resources/Prefabs/WorkshopScreen，图纸卡随存档运行时生成。
    /// </summary>
    public class WorkshopPanel : Panel
    {
        static readonly ItemSlot[] Slots = { ItemSlot.Dress, ItemSlot.Hair, ItemSlot.Wings };

        WorkshopHud hud;
        int tab;
        readonly List<RectTransform> cards = new List<RectTransform>();
        Coroutine toastRoutine;

        protected override void Build()
        {
            var backdrop = transform.Find("Backdrop")?.GetComponent<Image>();
            if (backdrop != null && app.Database.craftBg != null)
                backdrop.sprite = app.Database.craftBg;

            GameObject prefab = Resources.Load<GameObject>("Prefabs/WorkshopScreen");
            hud = prefab != null
                ? Instantiate(prefab, root).GetComponent<WorkshopHud>()
                : WorkshopHud.Assemble(root, app.Database);
            if (hud == null)
            {
                Debug.LogError("[叠叠裙] 工坊预制没有 WorkshopHud");
                return;
            }
            var rect = (RectTransform)hud.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            hud.Wire(() => app.Show(ScreenId.Home), OnTab);
        }

        public override void OnShow()
        {
            if (hud == null) return;
            tab = FirstTabWithWork();
            hud.ShowToast("");
            Refresh();
        }

        public override void OnHide()
        {
            StopAllCoroutines();
            toastRoutine = null;
        }

        void OnTab(int index)
        {
            tab = index;
            Refresh();
        }

        /// <summary>打开时直接停在有能做的图纸的那一页。</summary>
        int FirstTabWithWork()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                foreach (ItemDef item in app.Wardrobe.Blueprints(Slots[i]))
                {
                    if (app.Wardrobe.CanCraft(item)) return i;
                }
            }
            return tab;
        }

        void Refresh()
        {
            hud.SetTab(tab);
            hud.SetStock(app.Wardrobe);
            foreach (RectTransform card in cards)
                UiKit.Discard(card);
            cards.Clear();

            List<ItemDef> list = app.Wardrobe.Blueprints(Slots[tab]);
            hud.emptyLabel.text = list.Count == 0
                ? (Slots[tab] == ItemSlot.Wings ? "翅膀图纸很稀有，逢 50 关才有机会拿到" : "还没有图纸\n继续闯关就有机会拿到")
                : "";

            for (int i = 0; i < list.Count; i++)
            {
                int col = i % 2;
                int row = i / 2;
                float x = (col - 0.5f) * (WorkshopHud.CardW + WorkshopHud.GapX);
                float y = -12f - WorkshopHud.CardH * 0.5f - row * (WorkshopHud.CardH + WorkshopHud.GapY);
                cards.Add(BuildCard(list[i], new Vector2(x, y)));
            }
            int rows = (list.Count + 1) / 2;
            hud.content.sizeDelta = new Vector2(hud.content.sizeDelta.x,
                24f + rows * (WorkshopHud.CardH + WorkshopHud.GapY));
            hud.content.anchoredPosition = Vector2.zero;
        }

        RectTransform BuildCard(ItemDef item, Vector2 at)
        {
            var size = new Vector2(WorkshopHud.CardW, WorkshopHud.CardH);
            Image face = UiKit.Icon(hud.content, "Card_" + item.id, hud.cardSprite, new Vector2(0.5f, 1f), at, size);
            face.preserveAspect = false;
            if (face.sprite == null)
            {
                face.sprite = UiKit.CardSprite();
                face.type = Image.Type.Sliced;
            }
            RectTransform card = face.rectTransform;

            bool owned = app.Wardrobe.IsUnlocked(item);
            Image icon = UiKit.Icon(card, "Icon", item.ResolveIcon(), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(200f, 200f));
            if (owned)
                icon.color = new Color(1f, 1f, 1f, 0.55f);

            Text name = UiKit.Label(card, "Name", item.displayName, new Vector2(0.5f, 1f),
                new Vector2(0f, -276f), new Vector2(WorkshopHud.CardW - 40f, 50f), 34, CraftView.Cocoa);
            name.fontStyle = FontStyle.Bold;
            CraftView.AddRim(name);

            int[] need = CraftCatalog.RecipeOf(item.id) ?? new int[CraftCatalog.MatCount];
            int shown = 0;
            for (int m = 0; m < CraftCatalog.MatCount; m++)
            {
                if (need[m] <= 0) continue;
                int col = shown % 2;
                int row = shown / 2;
                float x = col == 0 ? -100f : 100f;
                float y = -340f - row * 62f;
                UiKit.Icon(card, "Need_" + m, CraftView.MatIcon(app.Database, m), new Vector2(0.5f, 1f),
                    new Vector2(x - 52f, y), new Vector2(56f, 56f));
                int have = app.Wardrobe.MaterialCount((CraftMat)m);
                bool enough = owned || have >= need[m];
                Text count = UiKit.Label(card, "Have_" + m, (owned ? need[m] : Mathf.Min(have, 999)) + "/" + need[m],
                    new Vector2(0.5f, 1f), new Vector2(x + 30f, y - 2f), new Vector2(120f, 44f), 28,
                    enough ? CraftView.Cocoa : CraftView.Short, TextAnchor.MiddleLeft);
                count.fontStyle = FontStyle.Bold;
                CraftView.AddRim(count);
                shown++;
            }

            bool ready = app.Wardrobe.CanCraft(item);
            string caption = owned ? "试穿" : ready ? "制作" : "材料不足";
            GameDatabase db = app.Database;
            Sprite skin = owned ? db.dressupBtnPeach : ready ? db.craftBtnMint : db.dressupBtnCream;
            Color ink = ready && !owned ? Color.white : CraftView.Cocoa;
            const float buttonH = 92f;
            Button button = UiKit.Button(card, caption, new Vector2(0.5f, 0f), new Vector2(0f, 66f),
                new Vector2(270f, buttonH), skin, ink, 36, () => OnCard(item, card));
            UiKit.FitPill(button.targetGraphic as Image, skin, buttonH);
            Text label = button.GetComponentInChildren<Text>();
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            if (ready && !owned)
                CraftView.AddRim(label, new Color(0.18f, 0.42f, 0.28f));
            return card;
        }

        void OnCard(ItemDef item, RectTransform card)
        {
            if (app.Wardrobe.IsUnlocked(item))
            {
                Sfx.Play(SfxId.Equip);
                app.Wardrobe.Equip(item);
                app.Show(ScreenId.DressUp);
                return;
            }
            if (!app.Wardrobe.Craft(item))
            {
                Sfx.Play(SfxId.Deny);
                Toast("还差一些材料，继续闯关攒一攒");
                return;
            }
            Sfx.Play(SfxId.Craft);
            Refresh();
            Toast("做好了「" + item.displayName + "」，已放进衣柜");
            RectTransform made = cards.Find(c => c.name == "Card_" + item.id);
            if (made != null)
                StartCoroutine(Pop(made));
        }

        IEnumerator Pop(RectTransform card)
        {
            float t = 0f;
            while (t < 0.32f && card != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.32f);
                card.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.08f);
                yield return null;
            }
            if (card != null)
                card.localScale = Vector3.one;
        }

        void Toast(string message)
        {
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        IEnumerator ToastRoutine(string message)
        {
            hud.ShowToast(message);
            yield return new WaitForSeconds(1.8f);
            hud.ShowToast("");
            toastRoutine = null;
        }
    }
}
