using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    public class LevelMapPanel : Panel
    {
        readonly List<Image> tiles = new List<Image>();
        readonly List<Text> captions = new List<Text>();
        readonly List<Image> locks = new List<Image>();
        readonly List<Image> icons = new List<Image>();
        Text starLabel;
        Text energyLabel;
        Text toast;

        const int Columns = 3;
        const float Tile = 280f;
        const float Gap = 36f;

        protected override void Build()
        {
            UiKit.Button(root, "返回", new Vector2(0f, 1f), new Vector2(130f, -90f),
                new Vector2(180f, 88f), Chip.White, 40,
                () => app.Show(ScreenId.Home));

            Image title = UiKit.ChipPlate(root, "Title", Chip.White, new Vector2(0.5f, 1f),
                new Vector2(0f, -90f), new Vector2(360f, 88f));
            UiKit.Label(title.transform, "Text", "选择关卡", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(360f, 80f), 48, Palette.Ink);

            UiKit.Icon(root, "Star", app.Database.iconStar, new Vector2(1f, 1f),
                new Vector2(-160f, -90f), new Vector2(56f, 56f));
            starLabel = UiKit.Label(root, "Stars", "0",
                new Vector2(1f, 1f), new Vector2(-80f, -90f), new Vector2(80f, 56f), 36, Palette.Ink);
            energyLabel = UiKit.Label(root, "Energy", "体力 5/5",
                new Vector2(1f, 1f), new Vector2(-80f, -160f), new Vector2(240f, 48f), 32, Palette.Ink,
                TextAnchor.MiddleRight);
            toast = UiKit.Label(root, "Toast", "", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -80f), new Vector2(640f, 64f), 36, Palette.RoseDark);

            int count = app.LevelCount;
            for (int i = 0; i < count; i++)
            {
                int levelIndex = i + 1;
                int row = i / Columns;
                int col = i % Columns;
                float x = (col - (Columns - 1) * 0.5f) * (Tile + Gap);
                float y = -340f - row * (Tile + Gap) - Tile * 0.5f;

                Image tile = UiKit.Slice(root, "Level_" + levelIndex, UiKit.CardSprite(),
                    new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(Tile, Tile), Color.white);

                LevelDef level = app.LevelAt(levelIndex);
                Sprite preview = level != null && level.reward != null ? level.reward.ResolveIcon()
                    : app.Database.iconMystery;
                Image icon = UiKit.Icon(tile.transform, "Icon", preview, new Vector2(0.5f, 1f),
                    new Vector2(0f, -30f), new Vector2(150f, 150f));

                Text caption = UiKit.Label(tile.transform, "Text", "第 " + levelIndex + " 关",
                    new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(Tile, 70f), 44, Palette.Ink);

                Image padlock = UiKit.Icon(tile.transform, "Lock", app.Database.iconLock,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(110f, 110f));

                UiKit.HitArea(tile.transform, "Hit", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(Tile, Tile), () => OnPick(levelIndex));

                tiles.Add(tile);
                icons.Add(icon);
                captions.Add(caption);
                locks.Add(padlock);
            }
        }

        public override void OnShow()
        {
            if (starLabel != null)
                starLabel.text = app.Wardrobe.Stars.ToString();
            if (energyLabel != null)
                energyLabel.text = "体力 " + app.Wardrobe.RecoverEnergy() + "/" + WardrobeService.MaxEnergy;
            if (toast != null)
                toast.text = "";

            for (int i = 0; i < tiles.Count; i++)
            {
                bool playable = app.Wardrobe.IsLevelPlayable(i + 1);
                bool cleared = app.Wardrobe.LevelsCleared >= i + 1;

                tiles[i].color = playable ? Color.white : Palette.Locked;
                icons[i].color = playable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                locks[i].enabled = !playable;
                captions[i].text = cleared ? "第 " + (i + 1) + " 关 ✓" : "第 " + (i + 1) + " 关";
                captions[i].color = playable ? Palette.Ink : new Color(0.42f, 0.44f, 0.48f);
            }
        }

        void OnPick(int levelIndex)
        {
            if (!app.Wardrobe.IsLevelPlayable(levelIndex)) return;
            LevelDef level = app.LevelAt(levelIndex);
            if (level == null) return;
            if (!app.StartLevel(level))
            {
                if (toast != null)
                    toast.text = "体力不足，过一会儿再来";
                if (energyLabel != null)
                    energyLabel.text = "体力 " + app.Wardrobe.Energy + "/" + WardrobeService.MaxEnergy;
            }
        }
    }
}
