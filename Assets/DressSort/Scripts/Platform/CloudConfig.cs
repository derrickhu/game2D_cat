namespace DressSort
{
    /// <summary>
    /// 后端是 CloudBase 云函数 cloudfunctions/dresssort-api，和 black-rosa 同一个环境。
    /// GameKey、云函数目录名、云端 GAME_KEY 环境变量三处必须一致。
    /// 真机要把 BaseUrl 的域名加进小游戏后台的 request 合法域名。
    /// </summary>
    public static class CloudConfig
    {
        public const string GameKey = "dresssort";
        public const string BaseUrl = "https://rosa-env-d7grf78r5dbd37323.service.tcloudbase.com";
        const string Prefix = "/" + GameKey + "-api";

        public const string LoginPath = Prefix + "/login";
        public const string RankSubmitPath = Prefix + "/rank/submit";
        public const string RankListPath = Prefix + "/rank/list";
        public const string GameClubDailyPath = Prefix + "/gameclub/daily";
        public const int RankListLimit = 50;

        public const int RequestTimeoutSec = 10;

        public const string TokenKey = GameKey + "_token";
        public const string AnonKey = GameKey + "_anon_id";
        public const string ProfileKey = GameKey + "_wx_profile";
        public const string RankSentKey = GameKey + "_rank_sent";
    }
}
