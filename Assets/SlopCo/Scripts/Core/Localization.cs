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
            { "menu.withai",   new[]{ "PLAY WITH AI", "AI와 플레이", "AIとプレイ", "与AI游玩" } },
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

            // ── Chaos Rating (shareable end-of-run grade + headline; ResultsUI substitutes {tokens}) ──
            { "grade.title",          new[]{ "CHAOS RATING", "카오스 등급", "カオス評価", "混乱评级" } },
            { "grade.flavor.chain",   new[]{ "x{chain} delivery chain — certified menace.", "x{chain} 배달 연쇄 — 공인 난동꾼.", "x{chain}連続配達 — 公認の暴れん坊。", "x{chain}连续送达 — 认证捣蛋鬼。" } },
            { "grade.flavor.demo",    new[]{ "Demolished ${destroyed} of cargo.", "화물 ${destroyed}어치 박살.", "貨物${destroyed}分を破壊。", "砸毁了价值${destroyed}的货物。" } },
            { "grade.flavor.survive", new[]{ "Survived {day} days of chaos.", "{day}일간 카오스 생존.", "{day}日間カオスを生き延びた。", "在混乱中存活了{day}天。" } },
            { "grade.flavor.hustle",  new[]{ "{deliveries} deliveries, ${delivered} earned.", "{deliveries}회 배달, ${delivered} 획득.", "{deliveries}回配達、${delivered}獲得。", "送达{deliveries}次，赚得${delivered}。" } },
            { "grade.flavor.rookie",  new[]{ "Rough day at SlopCo.", "SlopCo에서 험난한 하루.", "SlopCoでの厳しい一日。", "在SlopCo艰难的一天。" } },

            // ── Results body (stats / records / share) — ResultsUI substitutes {tokens}; rich-text composed outside ──
            { "res.cashquota",    new[]{ "CASH  ${cash}      QUOTA  ${quota}", "현금  ${cash}      쿼터  ${quota}", "現金  ${cash}      ノルマ  ${quota}", "现金  ${cash}      配额  ${quota}" } },
            { "res.day.survived", new[]{ "DAY {day} — SURVIVED", "{day}일차 — 생존", "{day}日目 — 生還", "第{day}天 — 幸存" } },
            { "res.day.short",    new[]{ "DAY {day} — SHORT!", "{day}일차 — 미달!", "{day}日目 — 未達！", "第{day}天 — 不足！" } },
            { "res.deliveries",   new[]{ "Deliveries:  {count}   (+${total})", "배달:  {count}회   (+${total})", "配達:  {count}回   (+${total})", "送达:  {count}次   (+${total})" } },
            { "res.destroyed",    new[]{ "Cargo destroyed:  ${val}", "파손 화물:  ${val}", "破壊した貨物:  ${val}", "损毁货物:  ${val}" } },
            { "res.smash",        new[]{ "Biggest single smash:  -${val}", "최대 단일 파손:  -${val}", "最大の単発破壊:  -${val}", "最大单次损失:  -${val}" } },
            { "res.bestchain",    new[]{ "Best chain:  x{chain}", "최고 연쇄:  x{chain}", "最高チェーン:  x{chain}", "最高连锁:  x{chain}" } },
            { "res.records",      new[]{ "RECORDS", "기록", "記録", "记录" } },
            { "res.best.day",     new[]{ "Best day:  Day {day}", "최고 기록:  {day}일차", "最高記録:  {day}日目", "最佳天数:  第{day}天" } },
            { "res.best.cash",    new[]{ "Best cash:  ${cash}", "최고 현금:  ${cash}", "最高現金:  ${cash}", "最高现金:  ${cash}" } },
            { "res.record.togo",  new[]{ "Record is Day {best} — {gap} to go!", "기록은 {best}일차 — {gap}일 남음!", "記録は{best}日目 — あと{gap}日！", "记录是第{best}天 — 还差{gap}天！" } },
            { "res.newrecord",    new[]{ "★ NEW RECORD!   {what}   ★", "★ 신기록!   {what}   ★", "★ 新記録！   {what}   ★", "★ 新纪录！   {what}   ★" } },
            { "res.what.rank",    new[]{ "RANK {letter}", "등급 {letter}", "ランク {letter}", "等级 {letter}" } },
            { "res.what.day",     new[]{ "DAY {day} SURVIVED", "{day}일차 생존", "{day}日目 生還", "第{day}天 幸存" } },
            { "res.what.chain",   new[]{ "x{chain} CHAIN", "x{chain} 연쇄", "x{chain} チェーン", "x{chain} 连锁" } },
            { "share.button",     new[]{ "SHARE", "공유", "シェア", "分享" } },
            { "share.copy",       new[]{ "COPY", "복사", "コピー", "复制" } },
            { "share.saved",      new[]{ "Saved to Clips ✓", "Clips에 저장됨 ✓", "Clipsに保存 ✓", "已保存到 Clips ✓" } },
            { "share.copied",     new[]{ "Copied! ✓", "복사됨! ✓", "コピーしました！ ✓", "已复制！ ✓" } },
            { "share.failed",     new[]{ "Save failed", "저장 실패", "保存失敗", "保存失败" } },

            // ── Daily cargo modifier (briefing banner) ──
            { "mod.rushhour",   new[]{ "RUSH HOUR — short fuse!", "러시아워 — 짧은 도화선!", "ラッシュアワー — 短い導火線！", "高峰期 — 导火索更短！" } },
            { "mod.doubleload", new[]{ "DOUBLE LOAD — extra cargo!", "더블 로드 — 화물 추가!", "ダブルロード — 貨物追加！", "双倍装载 — 货物加倍！" } },
            { "mod.hazardpay",  new[]{ "HAZARD PAY — risky, richer!", "위험 수당 — 위험한 만큼 짭짤!", "危険手当 — ハイリスク高報酬！", "危险津贴 — 高风险高回报！" } },
            { "mod.demolition", new[]{ "DEMOLITION DAY — bigger booms!", "철거의 날 — 더 큰 폭발!", "解体の日 — 大爆発！", "拆迁日 — 爆炸更猛！" } },
            { "mod.scenicroute",new[]{ "SCENIC ROUTE — slow fuse, lean pay", "여유로운 길 — 느린 도화선, 박한 보수", "のんびりルート — 遅い導火線・薄給", "悠闲路线 — 慢导火索，报酬微薄" } },
            { "mod.payday",     new[]{ "PAYDAY RUN — risky, big money!", "페이데이 — 위험하지만 한탕!", "ペイデイ — ハイリスク大金！", "发薪日 — 高风险大钞！" } },
            { "mod.greasy",     new[]{ "GREASY DAY — slippery cargo!", "미끄러운 날 — 화물이 주르륵!", "つるつるデー — 貨物が滑る！", "打滑日 — 货物滑溜溜！" } },
            { "mod.rubber",     new[]{ "RUBBER DAY — bouncy cargo!", "고무의 날 — 통통 튀는 화물!", "ラバーデー — 弾む貨物！", "橡胶日 — 货物弹跳！" } },

            // ── Per-cargo archetypes (market-fit-7 — intra-round variety) ──
            { "cargo.standard", new[]{ "STANDARD", "일반", "標準", "标准" } },
            { "cargo.volatile", new[]{ "VOLATILE — short fuse!", "불안정 — 짧은 도화선!", "不安定 — 短い導火線！", "易爆 — 导火索短！" } },
            { "cargo.slippery", new[]{ "SLIPPERY — greased up!", "미끌 — 저마찰!", "つるつる — 低摩擦！", "打滑 — 低摩擦！" } },
            { "cargo.heavy",    new[]{ "HEAVY — needs two!", "무거움 — 2인 운반!", "重い — 二人で！", "沉重 — 需两人！" } },

            // ── Crew rank (cumulative meta-progression, shown on the FIRED card) ──
            { "crew.title",    new[]{ "CREW", "크루", "クルー", "队伍" } },
            { "crew.line",     new[]{ "RANK {rank} — {name}", "랭크 {rank} — {name}", "ランク {rank} — {name}", "等级 {rank} — {name}" } },
            { "crew.progress", new[]{ "+{run} this run · {tonext} to next", "이번 런 +{run} · 다음까지 {tonext}", "今回 +{run} · 次まで {tonext}", "本局 +{run} · 距下一级 {tonext}" } },
            { "crew.maxed",    new[]{ "+{run} this run · MAX RANK", "이번 런 +{run} · 최고 등급", "今回 +{run} · 最高ランク", "本局 +{run} · 满级" } },
            { "crew.rankup",   new[]{ "★ RANK UP!   {name}   ★", "★ 승급!   {name}   ★", "★ ランクアップ！   {name}   ★", "★ 升级！   {name}   ★" } },
            { "rank.0", new[]{ "Intern", "인턴", "インターン", "实习生" } },
            { "rank.1", new[]{ "Driver", "드라이버", "ドライバー", "司机" } },
            { "rank.2", new[]{ "Hauler", "운반공", "運搬人", "搬运工" } },
            { "rank.3", new[]{ "Veteran Hauler", "베테랑 운반공", "ベテラン運搬人", "资深搬运工" } },
            { "rank.4", new[]{ "Slop Legend", "슬롭 전설", "スロップの伝説", "烂活传奇" } },

            // ── Crew MVP callout (per-player glory/shame on the end-of-run card; co-op replay hook) ──
            { "mvp.title",  new[]{ "CREW MVP", "크루 MVP", "クルー MVP", "团队MVP" } },
            { "mvp.hauler", new[]{ "MVP HAULER  —  ${val}", "운반왕  —  ${val}", "運搬王  —  ${val}", "运货王  —  ${val}" } },
            { "mvp.butter", new[]{ "BUTTERFINGERS  —  ${val} smashed", "버터핑거  —  ${val} 박살", "バターフィンガー  —  ${val} 破壊", "黄油手  —  砸了${val}" } },

            // ── Augments (roguelite shop) ──
            { "shop.title",      new[]{ "BUY AN AUGMENT", "증강 구매", "オーグメント購入", "购买增强" } },
            { "aug.light.name",  new[]{ "Lighter Load", "가벼운 짐", "軽い荷物", "轻装" } },
            { "aug.light.desc",  new[]{ "Carry 35% faster — but RATS chase you!", "운반 35% 빠름 — 대신 쥐가 쫓아옴!", "運搬35%速く — ただしネズミが追う！", "搬运快35% — 但老鼠会追你！" } },
            { "aug.adr.name",    new[]{ "Adrenaline", "아드레날린", "アドレナリン", "肾上腺素" } },
            { "aug.adr.desc",    new[]{ "Move 20% faster — fuse burns faster", "이동 20% 빠름 — 도화선이 빨리 탐", "移動20%速く — 導火線が早く燃える", "移动快20% — 导火索烧得更快" } },
            { "aug.hands.name",  new[]{ "Big Hands", "큰손", "ビッグハンド", "大手" } },
            { "aug.hands.desc",  new[]{ "+25% delivery pay — carry a bit slower", "배달 보수 +25% — 운반이 조금 느림", "配達報酬+25% — 運搬が少し遅い", "送货报酬+25% — 搬运略慢" } },
            { "aug.sprint.name", new[]{ "Sprinter", "스프린터", "スプリンター", "短跑者" } },
            { "aug.sprint.desc", new[]{ "Move 15% faster — slightly less pay", "이동 15% 빠름 — 보수 약간 감소", "移動15%速く — 報酬がやや減少", "移动快15% — 报酬略减" } },
            { "aug.steady.name",   new[]{ "Steady Hands", "안정된 손", "安定した手", "稳手" } },
            { "aug.steady.desc",   new[]{ "Fuse burns 25% slower — slightly less pay", "도화선 25% 느림 — 보수 약간 감소", "導火線が25%遅く — 報酬がやや減少", "导火索慢25% — 报酬略减" } },
            { "aug.insured.name",  new[]{ "Insured Cargo", "보험 화물", "保険付き荷物", "保险货物" } },
            { "aug.insured.desc",  new[]{ "Fuse 40% slower — carry a bit slower", "도화선 40% 느림 — 운반 조금 느림", "導火線40%遅く — 運搬が少し遅い", "导火索慢40% — 搬运略慢" } },
            { "aug.caffeine.name", new[]{ "Caffeine Rush", "카페인", "カフェイン", "咖啡因" } },
            { "aug.caffeine.desc", new[]{ "Move 30% faster — fuse burns faster", "이동 30% 빠름 — 도화선이 빨리 탐", "移動30%速く — 導火線が早く燃える", "移动快30% — 导火索烧得更快" } },
            { "aug.forklift.name", new[]{ "Forklift Certified", "지게차 면허", "フォークリフト免許", "叉车执照" } },
            { "aug.forklift.desc", new[]{ "Carry 50% faster — but slower on foot", "운반 50% 빠름 — 발이 느려짐", "運搬50%速く — 足が遅くなる", "搬运快50% — 但步速变慢" } },
            { "aug.hazard.name",   new[]{ "Hazard Bonus", "위험 수당", "危険手当", "危险津贴" } },
            { "aug.hazard.desc",   new[]{ "+40% delivery pay — but RATS chase you!", "배달 보수 +40% — 대신 쥐가 쫓아옴!", "配達報酬+40% — ただしネズミが追う！", "送货报酬+40% — 但老鼠会追你！" } },
            { "aug.marathon.name", new[]{ "Marathoner", "마라토너", "マラソンランナー", "马拉松者" } },
            { "aug.marathon.desc", new[]{ "Move 40% faster — carry slower", "이동 40% 빠름 — 운반 느림", "移動40%速く — 運搬が遅い", "移动快40% — 搬运变慢" } },
            { "aug.greased.name",  new[]{ "Greased Palms", "기름칠한 손", "袖の下", "油水" } },
            { "aug.greased.desc",  new[]{ "+30% pay — fuse burns faster", "보수 +30% — 도화선이 빨리 탐", "報酬+30% — 導火線が早く燃える", "报酬+30% — 导火索烧得更快" } },
            { "aug.mule.name",     new[]{ "Pack Mule", "짐말", "駄馬", "驮骡" } },
            { "aug.mule.desc",     new[]{ "Carry 30% faster — but slower on foot", "운반 30% 빠름 — 발이 느려짐", "運搬30%速く — 足が遅くなる", "搬运快30% — 但步速变慢" } },

            // ── Dash (stamina sprint) ──
            { "dash.label",     new[]{ "DASH", "대쉬", "ダッシュ", "冲刺" } },
            { "dash.exhausted", new[]{ "EXHAUSTED", "지침", "バテた", "力尽き" } },

            // ── Items (consumable + permanent) ──
            { "item.shoes.name",  new[]{ "Fancy Shoes", "멋쟁이 신발", "おしゃれな靴", "时髦鞋" } },
            { "item.shoes.desc",  new[]{ "Dash at high speed for a few seconds", "몇 초간 고속으로 질주", "数秒間高速でダッシュ", "数秒内高速冲刺" } },
            { "item.flag.name",   new[]{ "Cheer Flag", "응원 깃발", "応援旗", "加油旗" } },
            { "item.flag.desc",   new[]{ "Refill a teammate's stamina", "동료의 스태미나를 회복", "仲間のスタミナを回復", "恢复队友的体力" } },
            { "item.horn.name",   new[]{ "Hype Horn", "응원 나팔", "応援ラッパ", "加油喇叭" } },
            { "item.horn.desc",   new[]{ "Speed up a nearby teammate", "근처 동료의 속도를 높임", "近くの仲間を加速", "加速附近的队友" } },
            { "item.energy.name", new[]{ "Energy Drink", "에너지 드링크", "エナジードリンク", "能量饮料" } },
            { "item.energy.desc", new[]{ "Refill your own stamina", "자신의 스태미나를 회복", "自分のスタミナを回復", "恢复自身的体力" } },
            { "item.insole.name", new[]{ "Comfy Insole", "편한 깔창", "快適インソール", "舒适鞋垫" } },
            { "item.insole.desc", new[]{ "Permanent: short speed boost on use", "영구: 사용 시 짧은 속도 부스트", "恒久: 使用時に短い加速", "永久: 使用时短暂加速" } },
            { "item.flask.name",  new[]{ "Thermos", "보온병", "魔法瓶", "保温瓶" } },
            { "item.flask.desc",  new[]{ "Permanent: small stamina refill on use", "영구: 사용 시 소량 스태미나 회복", "恒久: 使用時に少量スタミナ回復", "永久: 使用时少量体力恢复" } },
            { "item.slot.empty",  new[]{ "Item: (empty)", "아이템: (없음)", "アイテム: (なし)", "道具: (空)" } },
            { "item.slot.none",   new[]{ "Perk: (none)", "특전: (없음)", "パーク: (なし)", "天赋: (无)" } },

            // ── Teammate loadout HUD ──
            { "hud.loadout.aug",  new[]{ "Augment", "증강", "オーグメント", "增强" } },
            { "hud.loadout.held", new[]{ "Holding", "보유", "所持", "持有" } },
            { "hud.loadout.used", new[]{ "Used", "사용", "使用", "已用" } },

            // ── Map select ──
            { "map.select", new[]{ "MAP", "맵", "マップ", "地图" } },
            { "map.random", new[]{ "RANDOM", "랜덤", "ランダム", "随机" } },
            { "map.0",      new[]{ "DEPOT", "디포", "デポ", "仓库" } },
            { "map.1",      new[]{ "YARD", "야드", "ヤード", "货场" } },

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
                "Grab the BOMB ( E )   ·   CARRY it to the yellow VAN before the fuse blows   ·   don't throw or drop it!",
                "폭탄을 집어 ( E )   ·   도화선이 다 타기 전에 노란 밴까지 \"옮겨\" 배달   ·   던지거나 떨어뜨리지 마!",
                "爆弾をつかむ ( E )   ·   導火線が燃え尽きる前に黄色いバンへ\"運ぶ\"   ·   投げたり落としたりしない！",
                "抓起炸弹 ( E )   ·   在导火索烧完前\"搬\"到黄色货车   ·   不要投掷或摔落！" } },

            // ── Ping / Emote wheel (co-op quick comms) ──
            { "ping.here",    new[]{ "Here",   "여기",   "ここ",       "这里" } },
            { "ping.danger",  new[]{ "Danger", "위험",   "危険",       "危险" } },
            { "ping.cargo",   new[]{ "Cargo",  "화물",   "貨物",       "货物" } },
            { "emote.thanks", new[]{ "Thanks", "고마워", "ありがとう", "谢谢" } },
            { "emote.yes",    new[]{ "Yes",    "좋아",   "OK",         "好" } },
            { "emote.no",     new[]{ "No",     "안돼",   "だめ",       "不行" } },
        };
    }
}
