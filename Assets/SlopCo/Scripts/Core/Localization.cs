using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlopCo.Core
{
    public enum Language { English = 0, Korean = 1, Japanese = 2, Chinese = 3 }

    /// <summary>
    /// Tiny key→string localization table for the four supported languages (EN/KO/JA/ZH). UI text reads
    /// <see cref="Get"/>; <see cref="LocalizedText"/> components re-apply on <see cref="OnLanguageChanged"/>.
    /// Language is persisted in PlayerPrefs. (CJK glyphs render via the OS dynamic-font fallback; a shipping
    /// build should bundle a Noto Sans CJK font.)
    /// </summary>
    public static class Localization
    {
        const string Key = "opt_language";
        public static Language Current { get; private set; } = Language.English;
        public static event Action OnLanguageChanged;

        public static readonly string[] LanguageNames = { "English", "한국어", "日本語", "中文" };

        public static void Load()
        {
            int v = PlayerPrefs.GetInt(Key, (int)Language.English);
            Current = (Language)Mathf.Clamp(v, 0, 3);
        }

        public static void SetLanguage(Language lang)
        {
            Current = lang;
            PlayerPrefs.SetInt(Key, (int)lang);
            OnLanguageChanged?.Invoke();
        }

        public static void Cycle(int dir)
        {
            int n = LanguageNames.Length;
            SetLanguage((Language)(((int)Current + dir + n) % n));
        }

        /// <summary>Localized string for the current language (falls back to English, then the key).</summary>
        public static string Get(string key)
        {
            if (T.TryGetValue(key, out var arr))
            {
                int i = (int)Current;
                if (i < arr.Length && !string.IsNullOrEmpty(arr[i])) return arr[i];
                if (arr.Length > 0 && !string.IsNullOrEmpty(arr[0])) return arr[0];
            }
            return key;
        }

        // index order: { English, Korean, Japanese, Chinese }
        static readonly Dictionary<string, string[]> T = new()
        {
            // ── Main menu ──
            { "menu.subtitle", new[]{ "co-op delivery chaos", "협동 배달 카오스", "協力配達カオス", "合作快递大乱斗" } },
            { "menu.solo",     new[]{ "PLAY SOLO", "솔로 플레이", "ソロプレイ", "单人游戏" } },
            { "menu.online",   new[]{ "PLAY ONLINE", "온라인 플레이", "オンライン", "在线游戏" } },
            { "menu.tutorial", new[]{ "TUTORIAL", "튜토리얼", "チュートリアル", "教程" } },
            { "menu.options",  new[]{ "OPTIONS", "옵션", "オプション", "选项" } },
            { "menu.controls", new[]{ "CONTROLS", "조작법", "操作方法", "操作说明" } },
            { "menu.quit",     new[]{ "QUIT", "종료", "終了", "退出" } },
            { "menu.footer",   new[]{ "vertical slice", "버티컬 슬라이스", "バーティカルスライス", "垂直切片" } },

            // ── Options ──
            { "opt.title",      new[]{ "OPTIONS", "옵션", "オプション", "选项" } },
            { "opt.audio",      new[]{ "AUDIO", "오디오", "オーディオ", "音频" } },
            { "opt.master",     new[]{ "Master", "마스터", "マスター", "主音量" } },
            { "opt.music",      new[]{ "Music", "음악", "音楽", "音乐" } },
            { "opt.sfx",        new[]{ "SFX", "효과음", "効果音", "音效" } },
            { "opt.display",    new[]{ "DISPLAY", "디스플레이", "ディスプレイ", "显示" } },
            { "opt.fullscreen", new[]{ "Fullscreen", "전체화면", "フルスクリーン", "全屏" } },
            { "opt.vsync",      new[]{ "V-Sync", "수직동기화", "垂直同期", "垂直同步" } },
            { "opt.quality",    new[]{ "Quality", "품질", "品質", "画质" } },
            { "opt.resolution", new[]{ "Resolution", "해상도", "解像度", "分辨率" } },
            { "opt.language",   new[]{ "Language", "언어", "言語", "语言" } },
            { "opt.back",       new[]{ "BACK", "뒤로", "戻る", "返回" } },

            // ── Pause ──
            { "pause.title",  new[]{ "PAUSED", "일시정지", "ポーズ", "暂停" } },
            { "pause.resume", new[]{ "RESUME", "계속하기", "再開", "继续" } },
            { "pause.leave",  new[]{ "LEAVE TO MENU", "메뉴로 나가기", "メニューへ", "返回菜单" } },

            // ── Controls ──
            { "controls.title", new[]{ "CONTROLS", "조작법", "操作方法", "操作说明" } },
            { "controls.body",  new[]{
                "WASD / Left Stick     Move\n\nE / Right Bumper     Grab & hold  (carry big items WITH a friend)\n\nLeft Mouse / Right Trigger     Throw  (hold to charge)\n\nSpace / A     Jump\n\nESC     Pause",
                "WASD / 왼쪽 스틱     이동\n\nE / RB     잡기 (큰 짐은 친구와 함께)\n\n마우스 좌클릭 / RT     던지기 (꾹 눌러 차지)\n\nSpace / A     점프\n\nESC     일시정지",
                "WASD / 左スティック     移動\n\nE / RB     つかむ（大きい荷物は仲間と一緒に）\n\n左クリック / RT     投げる（長押しでチャージ）\n\nSpace / A     ジャンプ\n\nESC     ポーズ",
                "WASD / 左摇杆     移动\n\nE / RB     抓取（大件物品需与队友合搬）\n\n左键 / RT     投掷（长按蓄力）\n\n空格 / A     跳跃\n\nESC     暂停" } },

            // ── Lobby ──
            { "lobby.host",  new[]{ "HOST", "호스트", "ホスト", "创建房间" } },
            { "lobby.join",  new[]{ "JOIN", "참가", "参加", "加入" } },
            { "lobby.start", new[]{ "START ROUND", "라운드 시작", "ラウンド開始", "开始回合" } },
            { "lobby.leave", new[]{ "LEAVE", "나가기", "退出", "离开" } },
            { "lobby.menu",  new[]{ "◀ MENU", "◀ 메뉴", "◀ メニュー", "◀ 菜单" } },
            { "lobby.hosting",   new[]{ "HOSTING", "호스팅 중", "ホスト中", "正在主持" } },
            { "lobby.connected", new[]{ "CONNECTED", "접속됨", "接続済み", "已连接" } },
            { "lobby.offline",   new[]{ "OFFLINE", "오프라인", "オフライン", "离线" } },

            // ── HUD phase banner ──
            { "phase.lobby",    new[]{ "LOBBY", "로비", "ロビー", "大厅" } },
            { "phase.briefing", new[]{ "GET READY", "준비", "準備", "准备" } },
            { "phase.hauling",  new[]{ "HAUL!", "운반!", "運べ！", "搬运！" } },
            { "phase.payout",   new[]{ "PAYOUT", "정산", "精算", "结算" } },
            { "phase.gameover", new[]{ "FIRED.", "해고.", "クビ。", "被炒了。" } },

            // ── Results (game-over actions) ──
            { "results.restart", new[]{ "PLAY AGAIN", "다시 시작", "もう一度", "再玩一次" } },
            { "results.menu",    new[]{ "MAIN MENU", "타이틀로", "タイトルへ", "返回标题" } },

            // ── Fuse ──
            { "fuse.label",    new[]{ "FUSE", "도화선", "導火線", "导火索" } },
            { "fuse.critical", new[]{ "!! FUSE !!", "!! 도화선 !!", "!! 導火線 !!", "!! 导火索 !!" } },

            // ── Tutorial ──
            { "tut.step",    new[]{ "STEP", "단계", "ステップ", "步骤" } },
            { "tut.complete",new[]{ "TUTORIAL  ✓", "튜토리얼  ✓", "チュートリアル  ✓", "教程  ✓" } },
            { "tut.move",    new[]{ "Move with WASD / Left Stick", "WASD / 왼쪽 스틱으로 움직여 보세요", "WASD / 左スティックで動いてみよう", "用 WASD / 左摇杆 移动" } },
            { "tut.grab",    new[]{ "Get close to the red bomb and hold E to grab it", "빨간 폭탄 가까이 가서  E (꾹)  으로 집으세요", "赤い爆弾に近づいて E長押しでつかもう", "靠近红色炸弹，长按 E 抓起" } },
            { "tut.deliver", new[]{ "Carry it to the yellow van before the fuse runs out!", "도화선이 다 타기 전에  노란 밴까지 옮겨 배달!", "導火線が燃え尽きる前に黄色いバンへ運べ！", "在导火索烧完前搬到黄色货车！" } },
            { "tut.done",    new[]{ "Done! Now for real — deliver the bomb safely.", "완료! 이제 진짜다 — 폭탄을 안전하게 배달하세요.", "完了！ここからが本番——爆弾を無事に届けよう。", "完成！接下来是真的——把炸弹安全送达。" } },

            // ── Tutorial hint (in-round one-liner) ──
            { "hint.line", new[]{
                "Hold  E  to grab   ·   carry BIG cargo WITH a friend   ·   deliver to the VAN before time!",
                "E (꾹) 잡기   ·   큰 짐은 친구와 함께   ·   시간 내 밴까지 배달!",
                "E長押しでつかむ   ·   大きい荷物は仲間と   ·   時間内にバンへ届けよう！",
                "长按 E 抓取   ·   大件物品与队友合搬   ·   在时间内送到货车！" } },
        };
    }
}
