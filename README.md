Server/                                          # 服务端根目录
├── RogueGameServer/                             # ASP.NET Core 服务端项目（MySQL + JWT）
│   ├── Controllers/                             # —— REST 控制器 ——
│   │   ├── AuthController.cs                    # 注册/登录接口，PBKDF2 密码哈希，登录失败 5 次锁定 1 分钟
│   │   ├── CloudSaveController.cs               # 云存档写入接口，金币/角色拥有/本局快照/铭文解锁与装备（accountId 从 JWT 解析防越权）
│   │   ├── ProfileController.cs                 # 玩家档案接口，一次拉取完整云存档（AsSplitQuery 避免笛卡尔积）
│   │   └── PurchaseController.cs                # 消费与结算接口，行锁+事务保证原子扣费与单局结算加币
│   ├── Models/                                  # —— 数据库实体 ——
│   │   └── Entities.cs                          # EF Core 实体映射：Account（含金币/封禁/未结算快照）、角色拥有、铭文解锁、铭文装备
│   ├── Data/                                    # —— 数据库上下文 ——
│   │   └── AppDbContext.cs                      # EF Core 上下文，声明复合主键与唯一索引，表结构由人工建表 SQL 管理、不用 EF Migration
│   ├── Services/                                # —— 安全与鉴权服务 ——
│   │   ├── TokenService.cs                      # JWT 签发服务，HS256 对称签名，密钥只在服务端防伪造
│   │   ├── PasswordHasher.cs                    # 密码哈希工具，PBKDF2-HMACSHA256 + 每账号独立随机盐，固定时间比较防时序攻击
│   │   └── LoginThrottle.cs                     # 登录暴力破解防护，同一用户名失败 5 次锁定 60 秒（内存计数）
│   ├── Common/                                  # —— 通用契约 ——
│   │   └── ApiResult.cs                         # 统一响应码 ApiCode 与统一响应体 ApiResult<T>（code/message/data）
│   ├── Dtos/                                    # —— 接口传输模型 ——
│   │   ├── Dtos.cs                              # 鉴权与档案 DTO：注册/登录请求、AuthData、ProfileDto 等
│   │   ├── SaveRequests.cs                      # 云存档写入 DTO：金币、角色拥有、铭文解锁/装备、本局快照
│   │   └── SpendDtos.cs                         # 消费与结算 DTO：SpendRequest 订单、SpendResult、SettleRunRequest
│   ├── Properties/
│   │   └── launchSettings.json                  # 开发调试启动配置（端口、profile）
│   ├── bin/ obj/                                # 编译输出与中间产物
│   ├── appsettings.json                         # 生产配置：MySQL 连接串与 JWT 密钥
│   ├── appsettings.Development.json             # 开发环境配置
│   ├── RogueGameServer.csproj                   # 项目文件（NuGet 依赖）
│   ├── RogueGameServer.http                     # HTTP 请求调试文件
│   └── Program.cs                               # 应用入口，MySQL 连接、JWT 鉴权、CORS 白名单、camelCase 契约、/healthz 健康检查
├── 放行防火墙5000端口.bat                        # Windows 防火墙放行 5000 端口脚本
├── 启动云存档后端.bat                            # 启动服务端进程的快捷脚本
└── start_server_dev.bat                         # 开发模式启动脚本


Assets/Scripts/
├── AudioManager.cs                            # 全局音频管理单例，管理音效与背景音乐播放
├── Boomerangs/
│   └── Boomerang.cs                           # 回旋镖投射物，飞出后返回并对沿途敌人造成伤害
├── Character/
│   ├── Character.cs                           # 角色基类，管理生命、经验、移动与护甲减伤
│   ├── MainCharacter.cs                       # 玩家主角色类，当前仅继承 Character
│   └── Abilities/                             # —— 技能系统 ——
│       ├── Ability.cs                         # 技能抽象基类，定义升级、冷却与描述
│       ├── AbilityManager.cs                  # 技能管理器，持有已拥有技能及升级数值注册
│       ├── AOEUpgradeAbility.cs               # 全局提升技能范围（AOE）的升级技能
│       ├── ArmorUpgradeAbility.cs             # 全局提升护甲的升级技能
│       ├── BazookaGunAbility.cs               # 火箭炮技能，悬停炮台发射爆炸火箭弹
│       ├── Book.cs                            # 环绕之书实体，旋转并碰撞伤害怪物
│       ├── BookAbility.cs                     # 环绕书本技能，多本绕体旋转攻击
│       ├── BoomerangAbility.cs                # 回旋镖技能，定期投掷可升级的回旋镖
│       ├── CooldownUpgradeAbility.cs          # 缩短武器冷却的升级技能
│       ├── DaggerAbility.cs                   # 短匕技能，刺击附加持续流血伤害
│       ├── DamageUpgradeAbility.cs            # 全局提升伤害的升级技能
│       ├── FixedDirectionStabAbility.cs       # 固定方向刺击技能（StabAbility 变体）
│       ├── FloatUpgradeAbility.cs             # 浮点升级技能泛型基类，提升数值属性
│       ├── GarlicAbility.cs                   # 大蒜光环技能，环绕范围持续伤害敌人
│       ├── GravityWellAbility.cs              # 引力井技能，投掷重力武器吸引敌人
│       ├── GrenadeThrowableAbility.cs         # 手雷投掷技能，投出爆炸并散射弹片
│       ├── GunAbility.cs                      # 枪械技能基类，按弹夹射速连发子弹
│       ├── IceSkatesAbility.cs                # 冰鞋技能，提升角色移动速度
│       ├── IntUpgradeAbility.cs               # 整数升级技能泛型基类，提升数值属性
│       ├── KnockbackUpgradeAbility.cs         # 提升击退力的升级技能
│       ├── LifestealAbility.cs                # 吸血技能，造成伤害时概率回复生命
│       ├── MachineGunAbility.cs               # 机枪技能，环身旋转枪台持续连发
│       ├── MeleeAbility.cs                    # 近战技能抽象基类，冷却驱动近战攻击
│       ├── MolotovAbility.cs                  # 燃烧瓶技能，投掷留下持续燃烧火圈
│       ├── ProjectileAbility.cs               # 投射技能基类，冷却发射子弹射向最近敌人
│       ├── ProjectileCountUpgradeAbility.cs   # 增加投射物数量的升级技能
│       ├── ProjectileSpeedAbilityUpgrade.cs   # 提升投射物速度的升级技能
│       ├── RecoveryAbility.cs                 # 恢复技能，按冷却周期定时回复生命
│       ├── ShurikenAbility.cs                 # 手里剑技能，连续投出多枚飞镖
│       ├── SlashAbility.cs                    # 挥砍技能，扇形扫击命中范围内敌人
│       ├── StabAbility.cs                     # 刺击技能基类，朝最近敌人突刺伤害
│       └── ThrowableAbility.cs                # 投掷技能基类，冷却投出可升级投掷物
├── Collectables/                              # —— 可拾取物 ——
│   ├── Collectable.cs                         # 可拾取物抽象基类，处理生成、吸附与收集动画
│   ├── Bomb.cs                                # 炸弹拾取物，收集后对全屏敌人造成伤害
│   ├── Coin.cs                                # 金币拾取物，分铜银金等面值收集加钱
│   ├── CoinPouch.cs                           # 金币袋占位组件，仅初始化玩家引用
│   ├── ExpGem.cs                              # 经验宝石拾取物，收集后为角色增加经验
│   ├── Health.cs                              # 回血拾取物，收集后立即回复生命
│   ├── Magnet.cs                              # 磁铁拾取物，收集后吸附全场金币与宝石
│   └── RedPotion.cs                           # 红药水拾取物，在持续时间内缓慢回复生命
├── Data/                                      # —— 数据存储 / 后端接入 ——
│   ├── ApiClient.cs                           # 服务端 REST 客户端，协程发 HTTP 请求
│   ├── ApiDataStore.cs                        # 联网数据存储，本地镜像+服务端权威扣费
│   ├── ApiWireModels.cs                       # 服务端通信的 JSON 线格式请求/响应模型
│   ├── CloudSaveBootstrap.cs                  # 启动时自动恢复云端登录会话
│   ├── DataStoreLocator.cs                    # 全局数据存储入口，支持切换实现
│   ├── IDataStore.cs                          # 玩家永久数据存储的抽象接口
│   ├── PlayerPrefsDataStore.cs                # 本地 PlayerPrefs 存储，离线兜底实现
│   ├── PlayerProfile.cs                       # 玩家档案纯数据模型（金币/角色/铭文）
│   ├── RunState.cs                            # 单局临时数据，跨场景存活、局末清空
│   ├── ServerSession.cs                       # 登录态管理，token 保存与恢复
│   └── SpendOrder.cs                          # 一次消费订单，描述扣费与所获权益
├── Gameplay/                                  # —— 核心玩法 ——
│   ├── IDamageable.cs                         # 可受伤实体抽象基类接口
│   ├── LevelManager.cs                        # 关卡主控，计时刷怪、相位切换与结算流程
│   ├── Chest.cs                               # 宝箱实体，开箱生成并收集掉落物
│   ├── AbilityCard.cs                         # 升级三选一弹窗中的技能选项卡片
│   ├── GameTimer.cs                           # 游戏计时文本显示（按秒刷新）
│   ├── PointBar.cs                            # 通用进度条（血条/经验条），带延迟条动画
│   ├── StatsManager.cs                        # 统计击杀、金币、伤害并显示在 HUD
│   ├── Engravings/
│   │   └── EngravingManager.cs                # 铭文系统全局管理，解锁/选择/倍率结算
│   ├── Inventory/
│   │   ├── Inventory.cs                       # 单局物品背包，按类型管理槽位
│   │   └── InventorySlot.cs                   # 背包单类物品槽位 UI 与堆叠计数
│   └── Pools/                                 # —— 对象池 ——
│       ├── Pool.cs                            # 对象池抽象基类，持实体管理器引用
│       ├── MonsterPool.cs                     # 怪物对象池，回收时重置精英状态
│       ├── ProjectilePool.cs                  # 投射物对象池
│       ├── BoomerangPool.cs                   # 回旋镖对象池
│       ├── ThrowablePool.cs                   # 投掷物对象池
│       ├── ExplosiveProjectilePool.cs         # 爆炸投射物对象池
│       ├── GravityProjectilePool.cs           # 重力投射物对象池
│       ├── ChestPool.cs                       # 宝箱对象池
│       ├── CoinPool.cs                        # 金币对象池
│       ├── ExpGemPool.cs                      # 经验宝石对象池
│       └── DamageTextPool.cs                  # 伤害飘字对象池
├── Main Menu/                                 # —— 主菜单（UI Toolkit） ——
│   ├── MainMenu.cs                            # 主菜单枢纽，调度角色页/强化页入口
│   ├── CrossSceneData.cs                      # 跨场景传递角色蓝图等数据的静态容器
│   ├── ShopSession.cs                         # 单局商店购买记录与限购规则裁决
│   ├── HomePageUITKController.cs              # UI Toolkit 首页，三入口与账户区
│   ├── LoginPageUITKController.cs             # UI Toolkit 登录页，登录切换数据源
│   ├── RegisterPageUITKController.cs          # UI Toolkit 注册页，注册即登录
│   ├── ShopPageUITKController.cs              # UI Toolkit 商店页，购买与单局规则展示
│   ├── CharacterPageUITKController.cs         # UI Toolkit 角色选择页控制器
│   └── Engravings/
│       └── EngravingPageUITKController.cs     # UI Toolkit 铭文强化页，解锁装备与槽位管理
├── Monsters/                                  # —— 怪物 ——
│   ├── Monster.cs                             # 怪物基类，含血量、移动、精英词缀状态管理
│   ├── EntityManager.cs                       # 怪物/宝箱/掉落物统一管理与刷怪、对象池调度
│   ├── MonsterSpawnTable.cs                   # 按时间插值控制刷怪率与怪物选取的刷怪表
│   ├── MeleeMonster.cs                        # 近战怪物，追向玩家近身攻击
│   ├── RangedMonster.cs                       # 远程怪物，靠近后停下射击弹幕
│   ├── ThrowingMonster.cs                     # 投掷怪物，靠近后投掷可抛物道具
│   ├── BoomerangMonster.cs                    # 回旋镖怪物，朝玩家投掷回旋镖
│   ├── BossMonster.cs                         # Boss 怪物，装配技能并按评分循环行动
│   ├── EliteMonsterData.cs                    # 精英词缀枚举、生成参数与调参常量定义
│   ├── EliteHealthBar.cs                      # 精英头顶血条，含护盾条显示
│   ├── AfterimageTrail.cs                     # 精英疾行词缀的黑色残影对象池特效
│   ├── DeathExplosionEffect.cs                # 精英死亡爆炸预警圈与延迟伤害结算
│   ├── DamageText.cs                          # 伤害数字飘字动画组件
│   └── Boss Abilities/                        # —— Boss 技能 ——
│       ├── BossAbility.cs                     # Boss 技能抽象基类，含激活与评分接口
│       ├── WalkBossAbility.cs                 # Boss 朝玩家移动的填充技能
│       ├── ChargeBossAbility.cs               # Boss 蓄力冲锋攻击技能
│       ├── BulletHellBossAbility.cs           # Boss 多圈环形弹幕技能
│       ├── ShotgunBossAbility.cs              # Boss 扇形霰弹齐射技能
│       └── GrenadeBossAbility.cs              # Boss 多发投掷手雷技能
├── Projectiles/                               # —— 投射物 ——
│   ├── Projectile.cs                          # 投射物基类，直线飞行命中造成伤害与击退
│   ├── ExplosiveProjectile.cs                 # 爆炸投射物，命中或落空时产生范围爆炸
│   └── GravityProjectile.cs                   # 重力投射物，命中后产生吸引敌人的引力场
├── ScriptableObjects/                         # —— 配置资产蓝图 ——
│   ├── CharacterBlueprint.cs                  # 角色属性蓝图，含解锁与价格
│   ├── LevelBlueprint.cs                      # 关卡蓝图，配置刷怪表、精英与宝箱参数
│   ├── ChestBlueprint.cs                      # 宝箱蓝图，外观贴图与掉落表
│   ├── CoinBlueprint.cs                       # 金币蓝图，各币种贴图配置
│   ├── ExpGemBlueprint.cs                     # 经验宝石蓝图，各等级宝石外观
│   ├── ShopItemBlueprint.cs                   # 商店道具蓝图基类，订单与购买逻辑
│   ├── ShopStatBoost.cs                       # 商店属性强化道具基类，写入单局加成
│   ├── ShopHPBoost.cs                         # 商店道具：提升生命上限
│   ├── ShopArmorBoost.cs                      # 商店道具：提升护甲值
│   ├── ShopSpeedBoost.cs                      # 商店道具：提升移动速度
│   ├── ShopLuckBoost.cs                       # 商店道具：提升运气值
│   ├── ShopConsumable.cs                      # 商店金币回购与消耗品礼包道具
│   ├── ShopCoinBuyback100.cs                  # 商店道具：获得 100 金币
│   ├── ShopCoinBuyback500.cs                  # 商店道具：获得 500 金币
│   ├── CollectableTypes/
│   │   ├── CollectableType.cs                 # 拾取物类型基类，关联预制体与堆叠上限
│   │   ├── BombCollectable.cs                 # 炸弹拾取物类型资产
│   │   ├── CoinCollectable.cs                 # 金币拾取物类型资产
│   │   ├── ExpGemCollectable.cs               # 经验宝石拾取物类型资产
│   │   ├── HealthCollectable.cs               # 回血拾取物类型资产
│   │   ├── MagnetCollectable.cs               # 磁铁拾取物类型资产
│   │   └── RedPotionCollectable.cs            # 红药水拾取物类型资产
│   ├── Engravings/
│   │   ├── EngravingBlueprint.cs              # 铭文蓝图，正向效果与诅咒属性
│   │   └── EngravingDatabase.cs               # 全局铭文库资产，汇总所有铭文
│   └── Monsters/
│       ├── MonsterBlueprint.cs                # 怪物属性蓝图基类，含掉落表
│       ├── MeleeMonsterBlueprint.cs           # 近战怪蓝图，攻击层配置
│       ├── RangedMonsterBlueprint.cs          # 远程怪蓝图，弹幕预制体与射程
│       ├── ThrowingMonsterBlueprint.cs        # 投掷怪蓝图，投掷物与射程配置
│       ├── BoomerangMonsterBlueprint.cs       # 回旋镖怪蓝图，回旋镖参数配置
│       └── BossMonsterBlueprint.cs            # Boss 蓝图，技能预制体与近战参数
├── Spatial/
│   └── SpatialHashGrid.cs                     # 空间哈希网格，快速范围查询周围对象
├── Testing/
│   └── MiscTesting.cs                         # 测试脚本，按键触发收币/伤害/加经验等调试
├── Throwables/                                # —— 投掷物 ——
│   ├── Throwable.cs                           # 投掷物基类，抛物飞行弹跳并造成伤害
│   ├── GravityThrowable.cs                    # 重力井投掷物，落地展开引力场吸引敌人
│   ├── GrenadeThrowable.cs                    # 手雷投掷物，爆炸后向四周散射弹片
│   ├── MolotovThrowable.cs                    # 燃烧瓶投掷物，落地生成持续燃烧火焰
│   └── MolotovFire.cs                         # 燃烧火焰区域，周期性灼烧范围内的敌人
├── UI/                                        # —— UI ——
│   ├── AbilitySelectionDialog.cs              # 升级时弹出技能三选一对话框
│   ├── DialogBox.cs                           # 对话框基类，带淡入淡出与缩放动画
│   ├── GameOverDialog.cs                      # 结算对话框，显示通关/失败战果
│   ├── PauseMenu.cs                           # 暂停菜单，切换 Time.timeScale 暂停恢复
│   ├── UIAnimator.cs                          # UI Toolkit 动效工具：入场淡入/数字滚动/抖动
│   └── HUD/                                   # —— 局内 HUD ——
│       ├── InGameHud.cs                       # 局内 HUD 总装，自动补齐缺失部件
│       ├── HudTheme.cs                        # HUD 设计令牌，集中配色与动效参数
│       ├── HudUiFactory.cs                    # 运行时构建 uGUI 部件的小工具工厂
│       ├── AbilityHud.cs                      # 局内技能栏，图标+冷却扇形+等级角标
│       ├── BossHealthBar.cs                   # Boss 血条，主条即时+延迟条显示掉血
│       ├── HudFeedback.cs                     # 局内视觉反馈：受击泛红/升级闪光/拾取飘字
│       ├── LevelProgressBar.cs                # 关卡进度条，含 miniBoss 刻度与相位标记
│       └── Editor/
│           └── HudSceneBaker.cs               # 编辑器工具，把 HUD 烘焙成真实场景物体
└── Utilities/                                 # —— 通用工具 ——
    ├── CameraShake.cs                         # 相机屏幕震动，只抖 xy 不影响世界坐标
    ├── CharacterCamera.cs                     # 按固定宽度设置正交相机尺寸
    ├── CharacterSetGenerator.cs               # 编辑器工具，生成字体字符集文件
    ├── CoinDisplay.cs                         # 金币文本显示，带数值滚动动画
    ├── CoroutineQueue.cs                      # 协程队列，按顺序逐个执行协程
    ├── DescriptionUtils.cs                    # 生成升级属性描述文本的工具
    ├── EasingUtils.cs                         # 缓动函数库（回弹/抛物线/弹性等）
    ├── EnumDataContainer.cs                   # 以枚举为索引的可序列化数据容器
    ├── EscapeToQuit.cs                        # 按 Esc 键退出游戏
    ├── FastList.cs                            # 带字典索引的快速增删查列表
    ├── GoToTransform.cs                       # 每帧将自身位置同步到目标 Transform
    ├── InfiniteBackground.cs                  # 无限滚动背景，随玩家移动重置 UV
    ├── Loot.cs                                # 单条掉落物数据（物品+掉率）
    ├── LootTable.cs                           # 泛型掉落表，按概率随机掉落
    ├── PhaseTransitionFX.cs                   # 相位切换演出，渐黑下沉显示提示语
    ├── PhysicsConstants.cs                    # 物理常量（重力加速度 g）
    ├── ResManager.cs                          # Addressables 资源管理，引用计数与句柄去重
    ├── Rotator.cs                             # 让物体绕自身持续旋转
    ├── SafeArea.cs                            # UI 适配手机屏幕安全区
    ├── ScaleParticlesToScreen.cs              # 按屏幕尺寸缩放粒子系统覆盖范围
    ├── ScaleToScreen.cs                       # 按屏幕世界尺寸缩放物体
    ├── SpriteAnimator.cs                      # 简易帧序列 Sprite 动画播放器
    ├── UpgradeableValues.cs                   # 技能可升级属性体系（浮点/整数升级链）
    └── ZPositioner.cs                         # 按 y 坐标调整 z 值实现伪深度排序
