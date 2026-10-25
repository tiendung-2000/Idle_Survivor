using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mô phỏng cả một lượt chơi dựa trên các asset THẬT của project (PlayerStats, PlayerStatUpgrades, WaveSettings,
/// EconomySettings, prefab Enemy, EnemyManager) để kiểm tra cân bằng sau mỗi lần chỉnh số.
///   Tools > Balance > Simulate Run    In bảng tóm tắt các lối chơi và diễn biến từng wave ra Console.
///
/// Mô hình bám theo code gameplay: quái sinh ngoài mép màn hình dọc, đi thẳng tới Player, đánh theo cooldown;
/// Player bắn quái gần nhất trong tầm, đạn bay theo tốc độ đạn, chí mạng ngẫu nhiên; giáp, hồi máu, boss, tiền thưởng.
/// "Người chơi mẫu" mua nâng cấp tham lam theo (lợi ích / giá). Kết quả là ước lượng để so sánh, không phải con số tuyệt đối.
/// </summary>
public static class BalanceSimulator
{
    private const float Dt = 0.05f;
    private const int Seeds = 6;
    private const int MaxWave = 150;

    // Khung hình dọc 9:16 với camera ortho (giống Game view trên điện thoại).
    private const float ScreenAspect = 9f / 16f;

    private struct Style
    {
        public string Name; public float Offense, Defense, Economy;
        public Style(string n, float o, float d, float e) { Name = n; Offense = o; Defense = d; Economy = e; }
    }

    private static readonly Style[] Styles =
    {
        new Style("Cân bằng", 1f, 0.75f, 0.55f),
        new Style("Thiên công", 1f, 0.3f, 0.3f),
        new Style("Thiên thủ", 0.6f, 1f, 0.4f),
        new Style("Không đầu tư vàng", 1f, 0.75f, 0f),
    };

    // ================================================================== Cấu hình đọc từ asset
    private class Config
    {
        public PlayerStatsData Player;
        public StatUpgradeData Upgrades;
        public WaveSettings Waves;
        public EconomySettings Economy;
        public float EnemyHealth, EnemyDamage, EnemyCooldown, EnemySpeed, EnemyStop;
        public int MaxEnemies;
        public float SpawnDistance, HalfWidth, HalfHeight;
    }

    private static Config LoadConfig()
    {
        var c = new Config
        {
            Player = FindAsset<PlayerStatsData>(),
            Upgrades = FindAsset<StatUpgradeData>(),
            Waves = FindAsset<WaveSettings>(),
            Economy = FindAsset<EconomySettings>(),
        };

        EnemyManager manager = UnityEngine.Object.FindAnyObjectByType<EnemyManager>();
        if (c.Player == null || c.Upgrades == null || c.Waves == null || manager == null || manager.EnemyPrefab == null)
        {
            Debug.LogError("Balance Simulator: thiếu PlayerStatsData / StatUpgradeData / WaveSettings hoặc EnemyManager trong scene đang mở.");
            return null;
        }

        var managerSo = new SerializedObject(manager);
        c.MaxEnemies = managerSo.FindProperty("maxEnemies").intValue;
        c.SpawnDistance = managerSo.FindProperty("spawnDistance").floatValue;

        var enemySo = new SerializedObject(manager.EnemyPrefab);
        c.EnemyDamage = enemySo.FindProperty("damage").floatValue;
        c.EnemyCooldown = enemySo.FindProperty("attackCooldown").floatValue;
        c.EnemySpeed = enemySo.FindProperty("moveSpeed").floatValue;
        c.EnemyStop = enemySo.FindProperty("stopDistance").floatValue;
        c.EnemyHealth = manager.EnemyPrefab.GetComponent<EnemyHealth>().MaxHealth;

        Camera cam = Camera.main;
        c.HalfHeight = cam != null && cam.orthographic ? cam.orthographicSize : 5f;
        c.HalfWidth = c.HalfHeight * ScreenAspect;
        return c;
    }

    private static T FindAsset<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    // ================================================================== Chỉ số người chơi (cùng công thức với Stat)
    private static float Value(Config c, int[] levels, StatType stat)
    {
        float flat = 0f, percentAdd = 0f, percentMult = 1f;
        StatUpgradeConfig u = c.Upgrades.Get(stat);
        if (u != null)
        {
            int level = levels[(int)stat];
            switch (u.growthType)
            {
                case StatGrowthType.Flat: flat += u.valuePerLevel * level; break;
                case StatGrowthType.PercentOfBase: percentAdd += u.valuePerLevel * level; break;
                default: percentMult *= Mathf.Pow(1f + u.valuePerLevel, level); break;
            }
        }
        PlayerStats.GetLimits(stat, out float min, out float max);
        return Mathf.Clamp((c.Player.GetBaseValue(stat) + flat) * (1f + percentAdd) * percentMult, min, max);
    }

    private static int Cost(Config c, StatUpgradeConfig u, int level)
    {
        int cost = u.GetCost(level);
        return c.Economy != null ? c.Economy.ApplyCostMultiplier(cost) : cost;
    }

    // Điểm sức mạnh dùng cho "người chơi mẫu" chọn nâng cấp
    private static void Power(Config c, int[] lv, out double offense, out double defense, out double economy)
    {
        float dmg = Value(c, lv, StatType.Damage), aps = Value(c, lv, StatType.AttackSpeed);
        float cc = Value(c, lv, StatType.CritChance), cm = Value(c, lv, StatType.CritMultiplier);
        float range = Mathf.Min(Value(c, lv, StatType.AttackRange), MaxSpawnDistance(c));
        float projectile = Mathf.Min(Value(c, lv, StatType.ProjectileSpeed), 25f);
        offense = dmg * aps * (1f + cc * (cm - 1f)) * Math.Pow(range / MaxSpawnDistance(c), 0.6) * Math.Pow(projectile / 25f, 0.1);

        float hp = Value(c, lv, StatType.MaxHealth), armor = Value(c, lv, StatType.Armor), regen = Value(c, lv, StatType.HealthRegen);
        defense = hp * (100f + armor) / 100f * (1f + regen * 8f / Mathf.Max(hp, 1f));
        economy = Value(c, lv, StatType.GoldMultiplier) * (1f + Value(c, lv, StatType.GoldBonusChance));
    }

    private static float MaxSpawnDistance(Config c) =>
        Mathf.Sqrt(c.HalfWidth * c.HalfWidth + c.HalfHeight * c.HalfHeight) + c.SpawnDistance;

    private static StatUpgradeConfig BestBuy(Config c, int[] lv, int coins, Style style, out int price)
    {
        Power(c, lv, out double o0, out double d0, out double e0);
        StatUpgradeConfig best = null; double bestScore = 0; price = 0;
        foreach (StatUpgradeConfig u in c.Upgrades.Upgrades)
        {
            int level = lv[(int)u.stat];
            int cost = Cost(c, u, level);
            if (cost < 0 || cost > coins) continue;

            lv[(int)u.stat] = level + 1;
            Power(c, lv, out double o1, out double d1, out double e1);
            lv[(int)u.stat] = level;

            double gain = style.Offense * Math.Log(o1 / o0) + style.Defense * Math.Log(d1 / d0) + style.Economy * Math.Log(e1 / e0);
            double score = gain / Math.Max(cost, 1);
            if (best == null || score > bestScore) { best = u; bestScore = score; price = cost; }
        }
        return best;
    }

    // ================================================================== Mô phỏng
    private class Enemy { public Vector2 Pos; public float Hp, Dmg, NextAttack, Reward, Speed; }
    private struct Shot { public float HitTime; public Enemy Target; public float Damage; }

    public struct WaveRow
    {
        public int Wave, Enemies, Coins; public float Seconds, EnemyHealth, EnemyDamage, MinHpPercent, Dps; public bool Died; public string Levels;
    }

    private static int Run(Config c, bool buy, Style style, int seed, out float totalSeconds, List<WaveRow> rows)
    {
        var rng = new System.Random(seed);
        int statCount = Enum.GetValues(typeof(StatType)).Length;
        var lv = new int[statCount];
        int coins = 0; double fraction = 0;
        float hp = Value(c, lv, StatType.MaxHealth);
        totalSeconds = 0f;
        WaveSettings ws = c.Waves;
        int coinsPerKill = c.Economy != null ? c.Economy.CoinsPerKill : 1;

        var enemies = new List<Enemy>();
        var shots = new List<Shot>();

        for (int wave = 1; wave <= MaxWave; wave++)
        {
            bool bossWave = ws.IsBossWave(wave);
            int count = ws.GetEnemyCount(wave);
            float interval = ws.GetSpawnInterval(wave);
            float ehp = c.EnemyHealth * ws.GetHealthMultiplier(wave);
            float edmg = c.EnemyDamage * ws.GetDamageMultiplier(wave);
            float reward = ws.GetRewardMultiplier(wave);
            float speed = c.EnemySpeed * ws.GetSpeedMultiplier(wave);

            enemies.Clear(); shots.Clear();
            int spawned = 0, waveCoins = 0;
            float t = 0f, nextSpawn = 0f, nextFire = 0f, minHp = hp;

            while (spawned < count || enemies.Count > 0)
            {
                // Mua nâng cấp
                while (buy)
                {
                    StatUpgradeConfig u = BestBuy(c, lv, coins, style, out int price);
                    if (u == null) break;
                    float oldMax = Value(c, lv, StatType.MaxHealth);
                    lv[(int)u.stat]++; coins -= price;
                    if (u.stat == StatType.MaxHealth) hp += Value(c, lv, StatType.MaxHealth) - oldMax;
                }

                // Sinh quái
                if (spawned < count && t >= nextSpawn)
                {
                    if (enemies.Count < c.MaxEnemies)
                    {
                        bool boss = bossWave && spawned == count - 1;
                        enemies.Add(new Enemy
                        {
                            Pos = SpawnPosition(c, rng),
                            Hp = ehp * (boss ? ws.BossHealthMultiplier : 1f),
                            Dmg = edmg * (boss ? ws.BossDamageMultiplier : 1f),
                            Reward = reward * (boss ? ws.BossRewardMultiplier : 1f),
                            Speed = speed * (boss ? ws.BossSpeedMultiplier : 1f),
                        });
                        spawned++; nextSpawn = t + interval;
                    }
                    else nextSpawn = t + 0.2f;
                }

                // Quái di chuyển và đánh
                float armorFactor = PlayerHealth.ArmorConstant / (PlayerHealth.ArmorConstant + Value(c, lv, StatType.Armor));
                foreach (Enemy e in enemies)
                {
                    float d = e.Pos.magnitude;
                    if (d > c.EnemyStop) e.Pos = Vector2.MoveTowards(e.Pos, e.Pos.normalized * c.EnemyStop, e.Speed * Dt);
                    else if (t >= e.NextAttack) { hp -= e.Dmg * armorFactor; e.NextAttack = t + c.EnemyCooldown; }
                }
                float maxHp = Value(c, lv, StatType.MaxHealth);
                hp = Mathf.Min(maxHp, hp + Value(c, lv, StatType.HealthRegen) * Dt);
                minHp = Mathf.Min(minHp, hp);

                if (hp <= 0f)
                {
                    rows?.Add(MakeRow(c, lv, wave, count, t, ehp, edmg, waveCoins, 0f, true));
                    totalSeconds += t;
                    return wave;
                }

                // Bắn quái gần nhất trong tầm
                if (t >= nextFire)
                {
                    float range = Value(c, lv, StatType.AttackRange);
                    Enemy target = null; float best = float.MaxValue;
                    foreach (Enemy e in enemies)
                    {
                        float d = e.Pos.magnitude;
                        if (d <= range && d < best) { best = d; target = e; }
                    }
                    if (target != null)
                    {
                        float dmg = Value(c, lv, StatType.Damage);
                        if (rng.NextDouble() < Value(c, lv, StatType.CritChance)) dmg *= Value(c, lv, StatType.CritMultiplier);
                        shots.Add(new Shot { HitTime = t + best / Value(c, lv, StatType.ProjectileSpeed), Target = target, Damage = dmg });
                        nextFire = t + 1f / Value(c, lv, StatType.AttackSpeed);
                    }
                }

                // Đạn trúng: trừ máu, quái chết thì cộng tiền (cùng công thức với CoinWallet)
                for (int i = shots.Count - 1; i >= 0; i--)
                {
                    Shot shot = shots[i];
                    if (t < shot.HitTime) continue;
                    shots.RemoveAt(i);
                    Enemy e = shot.Target;
                    if (!enemies.Contains(e)) continue;   // đã chết trước khi đạn tới

                    e.Hp -= shot.Damage;
                    if (e.Hp > 0f) continue;

                    enemies.Remove(e);
                    double gained = coinsPerKill * e.Reward * Value(c, lv, StatType.GoldMultiplier);
                    if (rng.NextDouble() < Value(c, lv, StatType.GoldBonusChance)) gained *= 2.0;
                    fraction += gained;
                    int whole = (int)Math.Floor(fraction);
                    fraction -= whole; coins += whole; waveCoins += whole;
                }
                t += Dt;
            }

            hp = Mathf.Min(Value(c, lv, StatType.MaxHealth), hp + Value(c, lv, StatType.HealthRegen) * ws.TimeBetweenWaves);
            rows?.Add(MakeRow(c, lv, wave, count, t, ehp, edmg, waveCoins, minHp / Mathf.Max(1f, Value(c, lv, StatType.MaxHealth)) * 100f, false));
            totalSeconds += t + ws.TimeBetweenWaves;
        }
        return MaxWave + 1;
    }

    private static Vector2 SpawnPosition(Config c, System.Random rng)
    {
        float along = (float)rng.NextDouble() * 2f - 1f;
        switch (rng.Next(4))
        {
            case 0: return new Vector2(along * c.HalfWidth, c.HalfHeight + c.SpawnDistance);
            case 1: return new Vector2(along * c.HalfWidth, -c.HalfHeight - c.SpawnDistance);
            case 2: return new Vector2(c.HalfWidth + c.SpawnDistance, along * c.HalfHeight);
            default: return new Vector2(-c.HalfWidth - c.SpawnDistance, along * c.HalfHeight);
        }
    }

    private static WaveRow MakeRow(Config c, int[] lv, int wave, int count, float seconds, float ehp, float edmg, int coins, float minHp, bool died)
    {
        float dps = Value(c, lv, StatType.Damage) * Value(c, lv, StatType.AttackSpeed) *
                    (1f + Value(c, lv, StatType.CritChance) * (Value(c, lv, StatType.CritMultiplier) - 1f));
        var sb = new StringBuilder();
        foreach (StatUpgradeConfig u in c.Upgrades.Upgrades)
        {
            int l = lv[(int)u.stat];
            if (l > 0) sb.Append(u.displayName).Append(' ').Append(l).Append(", ");
        }
        return new WaveRow { Wave = wave, Enemies = count, Seconds = seconds, EnemyHealth = ehp, EnemyDamage = edmg, Coins = coins, MinHpPercent = minHp, Dps = dps, Died = died, Levels = sb.ToString() };
    }

    // ================================================================== Báo cáo
    [MenuItem("Tools/Balance/Simulate Run", priority = 1)]
    public static void SimulateFromMenu()
    {
        Config c = LoadConfig();
        if (c == null) return;

        var report = new StringBuilder("<b>MÔ PHỎNG CÂN BẰNG</b> (" + Seeds + " lần chạy mỗi lối chơi, tối đa wave " + MaxWave + ")\n");

        var noBuy = new List<int>();
        for (int s = 0; s < Seeds; s++) noBuy.Add(Run(c, false, Styles[0], s, out _, null));
        report.Append("Không nâng cấp: chết ở wave ").Append(Summary(noBuy, null)).Append('\n');

        foreach (Style style in Styles)
        {
            var waves = new List<int>(); var minutes = new List<float>();
            for (int s = 0; s < Seeds; s++)
            {
                waves.Add(Run(c, true, style, s, out float seconds, null));
                minutes.Add(seconds / 60f);
            }
            report.Append(style.Name).Append(": chết ở wave ").Append(Summary(waves, minutes)).Append('\n');
        }

        var rows = new List<WaveRow>();
        Run(c, true, Styles[0], 0, out _, rows);
        report.Append("\n<b>Diễn biến (lối chơi cân bằng)</b>\nwave | giây | quái | máu quái | ST quái | tiền | DPS | HP thấp nhất\n");
        foreach (WaveRow r in rows)
        {
            if (!(r.Wave <= 2 || r.Wave % 5 == 0 || r.Died)) continue;
            report.Append($"{r.Wave,4} | {r.Seconds,4:0}s | {r.Enemies,3} | {NumberFormatter.Format(r.EnemyHealth),7} | {NumberFormatter.Format(r.EnemyDamage),6} | {r.Coins,6} | {NumberFormatter.Format(r.Dps),7} | ")
                  .Append(r.Died ? "CHẾT" : r.MinHpPercent.ToString("0") + "%").Append(c.Waves.IsBossWave(r.Wave) ? "  BOSS" : "").Append('\n');
        }
        if (rows.Count > 0) report.Append("Cấp lúc kết thúc: ").Append(rows[rows.Count - 1].Levels);
        Debug.Log(report.ToString());
    }

    private static string Summary(List<int> waves, List<float> minutes)
    {
        waves.Sort();
        string s = "[" + string.Join(", ", waves) + "] trung vị " + waves[waves.Count / 2];
        if (minutes != null) { minutes.Sort(); s += " (" + minutes[minutes.Count / 2].ToString("0.0") + " phút)"; }
        return s;
    }
}
