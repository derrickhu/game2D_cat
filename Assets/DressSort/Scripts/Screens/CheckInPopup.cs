using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>七日签到。断签一天从第 1 天重来，七天签满下一天也从头开始。</summary>
    public class CheckInPopup : PopupView
    {
        public Image[] cards = new Image[WardrobeService.CheckInDays];
        public Text[] amounts = new Text[WardrobeService.CheckInDays];
        public Image[] stamps = new Image[WardrobeService.CheckInDays];
        public Text[] dayLabels = new Text[WardrobeService.CheckInDays];
        public Color labelIdle = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        public Sprite cardIdle;
        public Sprite cardToday;
        public Sprite cardDone;
        public Sprite wideIdle;
        public Sprite wideToday;

        public Button signButton;
        public Text signLabel;
        public Sprite signReady;
        public Sprite signDone;

        protected override void OnOpen()
        {
            signButton.onClick.AddListener(OnSign);
            Refresh();
        }

        void Refresh()
        {
            WardrobeService w = app.Wardrobe;
            int run = w.CheckInRun;
            bool done = w.CheckedInToday;
            int today = done ? run : run + 1;

            for (int i = 0; i < WardrobeService.CheckInDays; i++)
            {
                int day = i + 1;
                bool wide = i == WardrobeService.CheckInDays - 1;
                bool isToday = !done && day == today;
                bool claimed = day <= run;
                if (cards[i] != null)
                    cards[i].sprite = wide
                        ? (isToday ? wideToday : wideIdle)
                        : (isToday ? cardToday : claimed && cardDone != null ? cardDone : cardIdle);
                if (amounts[i] != null)
                    amounts[i].text = "×" + WardrobeService.CheckInRewards[i];
                if (stamps[i] != null)
                    stamps[i].gameObject.SetActive(claimed);
                if (!wide && dayLabels[i] != null)
                    dayLabels[i].color = isToday ? Color.white : labelIdle;
            }

            SetCapsule(signButton, signLabel, done ? "明天再来" : "签到", done ? signDone : signReady, !done, !done);
        }

        void OnSign()
        {
            int day = app.Wardrobe.CheckIn();
            if (day <= 0)
            {
                Toast("今天已经签过了");
                return;
            }
            Toast("签到成功，体力 +" + WardrobeService.CheckInRewards[day - 1]);
            changed?.Invoke();
            Refresh();
        }
    }
}
