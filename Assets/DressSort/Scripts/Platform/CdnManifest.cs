// 由 Tools/cdn_build.py 生成，别手改。
using System.Collections.Generic;

namespace DressSort
{
    public static class CdnManifest
    {
        public const string BaseUrl = "https://726f-rosa-env-d7grf78r5dbd37323-1414200063.tcb.qcloud.la/dresssort/StreamingAssets/";

        // 逻辑名 -> 云上相对路径
        public static readonly Dictionary<string, string> Files =
            new Dictionary<string, string>
            {
                { "Audio/bgm_event", "Audio/bgm_event_e2382b6b9cec45ac635d525b7a0ff8a1.mp3" },
                { "Audio/bgm_lobby", "Audio/bgm_lobby_b0b8c122648b30d41ff1b1bf501fad46.mp3" },
                { "Audio/bgm_play", "Audio/bgm_play_6844523a5638fef0840cb2af8ad28cce.mp3" },
                { "Portraits/body_black_ribbon", "Portraits/body_black_ribbon_e0bfd5fe17bd6068a75d74c10f3a2ee6.png" },
                { "Portraits/body_blueberry", "Portraits/body_blueberry_2a3d620c30ce78936f00fa1af20ff2ef.png" },
                { "Portraits/body_cherry_red", "Portraits/body_cherry_red_ea0fce4061f6ea7dd40d601dfcaa3a5c.png" },
                { "Portraits/body_coral_sun", "Portraits/body_coral_sun_887bda1af69e6ca374dbf0da460ccec0.png" },
                { "Portraits/body_cream_cloud", "Portraits/body_cream_cloud_9872f567080285f56bda520669f30f9e.png" },
                { "Portraits/body_grape_school", "Portraits/body_grape_school_0e55fb0f4d0b9da90a828d21a427a26b.png" },
                { "Portraits/body_honey_bow", "Portraits/body_honey_bow_8353d77483c5f737a36d47815b0b1083.png" },
                { "Portraits/body_ivory_lace", "Portraits/body_ivory_lace_462210bb84f33babe9248ca1f5cfb6b7.png" },
                { "Portraits/body_lavender_star", "Portraits/body_lavender_star_1959ddf06a20771042452bec58ff60d9.png" },
                { "Portraits/body_lemon_print", "Portraits/body_lemon_print_bc5e909bfe7aa10f2e4aeaa678c10afd.png" },
                { "Portraits/body_lilac_lace", "Portraits/body_lilac_lace_c51b716bb4d52a5446a2af9d49dab788.png" },
                { "Portraits/body_matcha_pinafore", "Portraits/body_matcha_pinafore_57bb8058efb69f49c89550d410f6bd28.png" },
                { "Portraits/body_mint_bow", "Portraits/body_mint_bow_b63fc37f76f93b785368d9b7ea9990c3.png" },
                { "Portraits/body_navy_stripe", "Portraits/body_navy_stripe_1911b092f2627610699df9a366a2c2d3.png" },
                { "Portraits/body_orange_slice", "Portraits/body_orange_slice_f8d2d29d7a8cca775daabf843c10e1fd.png" },
                { "Portraits/body_peach_puff", "Portraits/body_peach_puff_bf079a0ee2c8a3dd83f39f442d56b950.png" },
                { "Portraits/body_pink_gingham", "Portraits/body_pink_gingham_6e152a526bb73a680752a6dd60ad8924.png" },
                { "Portraits/body_rose_velvet", "Portraits/body_rose_velvet_bbaf74ea8a3aa1db19cafb865fad7ed0.png" },
                { "Portraits/body_sky_dot", "Portraits/body_sky_dot_9e1e01afd82815b303a86c33f6a77f29.png" },
                { "Portraits/body_strawberry", "Portraits/body_strawberry_c7dd22ca9d6eb500b7eae3d1861928e3.png" },
                { "Portraits/body_teal_sailor", "Portraits/body_teal_sailor_4a9f3c76051c89dc0229a0b148148c4f.png" },
                { "Portraits/hairback_milktea_long", "Portraits/hairback_milktea_long_ae7c769bd8a98cdb79b21e34be5f4280.png" },
                { "Portraits/head_apricot", "Portraits/head_apricot_2dd4a6cd1fb4859348011beae47431f2.png" },
                { "Portraits/head_baguette", "Portraits/head_baguette_cdaf80f8c58ad907b2000d2bf5363af6.png" },
                { "Portraits/head_caramel", "Portraits/head_caramel_163390e880193dcaaf704617362c509b.png" },
                { "Portraits/head_denim", "Portraits/head_denim_d304002b0d9715236836298f64914f27.png" },
                { "Portraits/head_milktea", "Portraits/head_milktea_184451c0dfa7f715a3064895571055d9.png" },
                { "Portraits/head_milktea_long", "Portraits/head_milktea_long_636d050f5e8b6314bada4405d38483e8.png" },
                { "Portraits/head_pink", "Portraits/head_pink_a211a4829bc0ec247bbeff5c34bacfe4.png" },
                { "Portraits/head_wisteria", "Portraits/head_wisteria_f1d4872181697741d5ff7a1d75536f51.png" },
            };

        public static string Url(string name)
        {
            string rel;
            return Files.TryGetValue(name, out rel) ? BaseUrl + rel : null;
        }
    }
}
