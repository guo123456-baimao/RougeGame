using UnityEngine;
[System.Serializable]
public class MonsterSpawnTable
{
    public SpawnRateKeyframe[] spawnRateKeyframes;
    public SpawnChanceKeyframe[] spawnChanceKeyframes;
    public HPMultiplierKeyframe[] hpMultiplierKeyframes;

    /// <summary>
    /// 按归一化时间 t 在刷怪速率关键帧之间线性插值，得到当前每秒刷怪数。
    /// </summary>
    public float GetSpawnRate(float t)
    {
        if (t == 0)
            return spawnRateKeyframes[0].spawnRate;
        for (int i = 1; i < spawnRateKeyframes.Length; i++)
        {
            if (spawnRateKeyframes[i].t >= t)
                return Mathf.Lerp(spawnRateKeyframes[i-1].spawnRate, spawnRateKeyframes[i].spawnRate, Remap01(spawnRateKeyframes[i-1].t, spawnRateKeyframes[i].t, t));
        }
        return 0;
    }

    /// <summary>
    /// 按 t 抽取怪物下标，并同时返回该怪物对应的血量倍率。
    /// </summary>
    public (int, float) SelectMonsterWithHPMultiplier (float t)
    {
        if (t == 0)
        {
            int monsterIdx = SelectMonster(1, t);
            float hpBuff = hpMultiplierKeyframes.Length > 0 ? hpMultiplierKeyframes[0].healthBuffs[monsterIdx] : 0;
            return (monsterIdx, hpBuff); 
        }
        for (int i = 0; i < spawnChanceKeyframes.Length; i++)
        {
            if (spawnChanceKeyframes[i].t >= t)
            {
                int monsterIdx = SelectMonster(i, t);
                for (i = 0; i < hpMultiplierKeyframes.Length; i++)
                {
                    if (hpMultiplierKeyframes[i].t >= t)
                    {
                        return (monsterIdx, GetHPBuff(i, monsterIdx, t));
                    }
                }
                return (-1, 0);
            }
        }
        return (-1, 0);
    }

    /// <summary>
    /// 按 t 在刷怪概率关键帧中抽取一个怪物下标。
    /// </summary>
    public int SelectMonster (float t)
    {
        if (t == 0)
            return SelectMonster(1, t);
        for (int i = 0; i < spawnChanceKeyframes.Length; i++)
        {
            if (spawnChanceKeyframes[i].t >= t)
            {
                return SelectMonster(i, t);
            }
        }
        return -1;
    }

    /// <summary>
    /// 在两个相邻概率关键帧之间插值后按权重随机抽取怪物下标。
    /// </summary>
    private int SelectMonster(int i, float t)
    {
        float rand = Random.Range(0f, 1.0f);
        float cumulative = 0;
        float tLerp = Remap01(spawnChanceKeyframes[i-1].t, spawnChanceKeyframes[i].t, t);
        for (int j = 0; j < spawnChanceKeyframes[i-1].spawnChances.Length; j++)
        {
            cumulative += Mathf.Lerp(spawnChanceKeyframes[i-1].spawnChances[j], spawnChanceKeyframes[i].spawnChances[j], tLerp);
            if (rand < cumulative)
                return j;
        }
        return -1;
    }

    /// <summary>
    /// 按 t 在两个相邻血量倍率关键帧之间插值，取第 j 只怪物的血量倍率。
    /// </summary>
    private float GetHPBuff(int i, int j, float t)
    {
        float tLerp = Remap01(hpMultiplierKeyframes[i-1].t, hpMultiplierKeyframes[i].t, t);
        return Mathf.Lerp(hpMultiplierKeyframes[i-1].healthBuffs[j], hpMultiplierKeyframes[i].healthBuffs[j], tLerp);
    }

    /// <summary>
    /// 把 t 从 [min, max] 区间映射到 [0, 1]。
    /// </summary>
    private float Remap01(float min, float max, float t)
    {
        return (t - min) / (max - min);
    }

    [System.Serializable]
    public class SpawnRateKeyframe
    {
        public float t;
        public float spawnRate;
    }

    [System.Serializable]
    public class SpawnChanceKeyframe
    {
        public float t;
        public float[] spawnChances;
    }

    [System.Serializable]
    public class HPMultiplierKeyframe
    {
        public float t;
        public float[] healthBuffs;
    }
}
