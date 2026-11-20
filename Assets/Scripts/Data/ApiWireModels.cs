using System;
/// <summary>
/// 与服务端通信的线格式模型。
/// 注意：项目没有 Newtonsoft.Json，用的是 Unity 自带的 JsonUtility，
/// 它要求字段名与 JSON 完全一致，而服务端已统一配置 camelCase，
/// 所以这里字段名刻意小写开头，不是写错了，别改成 PascalCase。
/// </summary>

// ---- 请求体 ----
[Serializable] public class AuthPayload { public string username; public string password; }
[Serializable] public class CoinsPayload { public int coins; }
[Serializable] public class CharacterOwnedPayload { public string characterUid; public bool owned; }
[Serializable] public class EngravingUnlockPayload { public string engravingId; }
[Serializable] public class EngravingSelectPayload { public string[] engravingIds; }

/// <summary>未结算的本局进度快照；空串表示清空（一局已结束）。</summary>
[Serializable] public class SessionSnapshotPayload { public string snapshot; }

// ---- 响应体：统一 { code, message, data } ----
[Serializable] public class AuthEnvelope { public int code; public string message; public AuthData data; }
[Serializable] public class AuthData { public long accountId; public string username; public string accessToken; public long expiresIn; }

[Serializable] public class ProfileEnvelope { public int code; public string message; public ProfileData data; }
[Serializable]
public class ProfileData
{
    public long accountId;
    public string username;
    public int coins;
    public CharacterEntry[] characters;
    public EngravingSection engravings;
    /// <summary>未结算的本局进度快照（RunState 序列化 JSON），没有则为空串。</summary>
    public string sessionSnapshot;
}
[Serializable] public class CharacterEntry { public string uid; public bool owned; }
[Serializable] public class EngravingSection { public string[] unlocked; public string[] selected; }

[Serializable] public class CoinsEnvelope { public int code; public string message; public CoinsData data; }
[Serializable] public class CoinsData { public int coins; }

[Serializable] public class BoolEnvelope { public int code; public string message; public bool data; }

// ---- P4：服务端权威扣费 / 结算 ----
[Serializable]
public class SpendPayload
{
    public string itemId;
    public int cost;
    public string grantsCharacterUid;
    public string unlocksEngraving;
    public int grantsCoins;
}
[Serializable] public class SettleRunPayload { public int coinsGained; }

// ---- 本地配置（不是网络线格式，放这里是因为同样走 JsonUtility 反序列化） ----
[Serializable]
public class ServerConfig
{
    public string baseUrl;
}

[Serializable] public class SpendEnvelope { public int code; public string message; public SpendData data; }
[Serializable]
public class SpendData
{
    public int coins;
    public bool grantedCharacter;
    public bool unlockedEngraving;
}
