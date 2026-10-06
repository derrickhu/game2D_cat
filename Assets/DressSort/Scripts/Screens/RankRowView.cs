using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>排行榜的一行。头像先用名字首字垫着，图拉下来再盖上。</summary>
    public class RankRowView : MonoBehaviour
    {
        public Image plate;
        public Image medal;
        public Image numberPlate;
        public Text numberLabel;
        public Image avatarPicture;
        public Text avatarInitial;
        public Text nameLabel;
        public Text tipLabel;
        public Text countLabel;
        public GameObject meStamp;

        public void Bind(RankRow r, bool pinned, Sprite[] medals, Sprite idlePlate, Sprite minePlate, Font nameFont)
        {
            bool podium = r.rank >= 1 && r.rank <= 3 && medals != null && medals.Length >= 3;
            plate.sprite = pinned || r.isMe ? minePlate : idlePlate;
            medal.gameObject.SetActive(podium);
            if (podium) medal.sprite = medals[r.rank - 1];
            numberPlate.gameObject.SetActive(!podium);
            numberLabel.text = r.rank <= 0 ? "-" : r.rank.ToString();
            numberLabel.fontSize = r.rank >= 100 ? 24 : 32;

            string display = string.IsNullOrEmpty(r.displayName) ? "裙友" : r.displayName;
            nameLabel.text = Clip(display, 8);
            if (nameFont != null)
            {
                nameLabel.font = nameFont;
                avatarInitial.font = nameFont;
            }
            avatarInitial.text = FirstChar(display);
            avatarInitial.gameObject.SetActive(true);
            avatarPicture.gameObject.SetActive(false);

            bool waiting = pinned && r.rank <= 0;
            tipLabel.gameObject.SetActive(waiting);
            if (waiting)
                tipLabel.text = r.cleared > 0 ? "上榜中，稍后再看" : "通关第一关即可上榜";
            RectTransform nameRect = nameLabel.rectTransform;
            nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, waiting ? 20f : 0f);

            countLabel.text = r.cleared + " 关";
            if (meStamp != null) meStamp.SetActive(r.isMe || pinned);

            if (!Application.isPlaying) return;
            RankService.AvatarOf(r.avatarUrl, sprite =>
            {
                if (sprite == null || this == null || avatarPicture == null) return;
                avatarPicture.sprite = sprite;
                avatarPicture.gameObject.SetActive(true);
                avatarInitial.gameObject.SetActive(false);
            });
        }

        static string Clip(string s, int max)
        {
            var info = new StringInfo(s);
            if (info.LengthInTextElements <= max) return s;
            return info.SubstringByTextElements(0, max - 1) + "…";
        }

        static string FirstChar(string s)
        {
            return new StringInfo(s).SubstringByTextElements(0, 1);
        }
    }
}
