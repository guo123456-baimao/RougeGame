using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
/// <summary>
/// UI Toolkit 版角色页。数据源与 uGUI 版（CharacterSelector / CharacterCard）一致，
/// 都读同一批 CharacterBlueprint；解锁与扣币规则也保持一致。
/// </summary>
public class CharacterPageUITKController : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private CharacterBlueprint[] characterBlueprints;
    // 角色卡结构来自 UXML 模板（Assets/UI/CharacterCard.uxml），
    // 卡片样式在 UI Builder 里改模板即可，无需改代码。
    [SerializeField] private VisualTreeAsset cardTemplate;

#if UNITY_EDITOR
    /// <summary>
    /// 模板引用留空时自动补上，省去每个场景手动拖一次
    /// </summary>
    private void OnValidate()
    {
        if (cardTemplate == null)
            cardTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/CharacterCard.uxml");
    }
#endif

    private const int GameSceneIndex = 1;

    private class CardView
    {
        public CharacterBlueprint character;
        public VisualElement root;
        public Image portrait;
        public Label name;
        public Label tag;
        public Label hp;
        public Label armor;
        public Label movespeed;
        public Label luck;
        public VisualElement abilitiesRow;
        public Button action;
    }

    private readonly List<CardView> cards = new List<CardView>();

    private VisualElement grid;
    private Label coinsText;
    private Label ownedCountText;
    // UIDocument 在 GameObject 禁用后会拆掉可视树，重新激活时克隆一棵新树，
    // 所有缓存的元素引用都会失效；用 builtRoot + grid.panel 判断是否需要重建。
    private VisualElement builtRoot;

    public bool IsOpen => gameObject.activeSelf;

    // UIDocument 的 OnEnable 顺序与本组件没有保证，rootVisualElement 可能在本组件
    // 的 OnEnable 里还没准备好；下一帧再试一次，视觉上无感。
    private bool refreshScheduled;

    /// <summary>
    /// 组件启用时安排一次延迟刷新，避免可视树尚未就绪时读取界面。
    /// </summary>
    private void OnEnable()
    {
        ScheduleRefresh();
    }

    /// <summary>
    /// 组件停用时取消已排队的刷新并清除待刷新标记，防止重复执行。
    /// </summary>
    private void OnDisable()
    {
        CancelInvoke(nameof(TryRefresh));
        refreshScheduled = false;
    }

    /// <summary>
    /// 激活页面对象并安排一次刷新，用于从首页跳转到角色选择页。
    /// </summary>
    public void Open()
    {
        gameObject.SetActive(true);
        ScheduleRefresh();
    }

    /// <summary>
    /// 隐藏角色选择页并立即统一刷新各处金币显示。
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
        CoinDisplayManager.UpdateAll();
    }

    /// <summary>
    /// 在当前帧末安排一次刷新，同一时刻只保留一个待执行任务。
    /// </summary>
    private void ScheduleRefresh()
    {
        if (refreshScheduled) return;
        refreshScheduled = true;
        Invoke(nameof(TryRefresh), 0f);
    }

    /// <summary>
    /// 清除待刷新标记后真正执行一次界面刷新。
    /// </summary>
    private void TryRefresh()
    {
        refreshScheduled = false;
        Refresh();
    }

    /// <summary>
    /// 在可视树就绪后重建卡片、刷新卡片与顶部信息，并播放入场动画。
    /// </summary>
    public void Refresh()
    {
        if (document == null)
            document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null)
        {
            ScheduleRefresh();
            return;
        }

        var root = document.rootVisualElement;
        if (root.Q<VisualElement>("CardGrid") == null)
        {
            ScheduleRefresh();
            return;
        }

        Build(root);
        RefreshCards();
        RefreshHeader();

        // 树已建好才播入场动画；每次 SetActive 重新激活都会重播一次
        UIAnimator.PlayEnter(root);
    }

    /// <summary>
    /// 确认可视树有效后缓存界面元素引用、绑定返回按钮并生成全部角色卡。
    /// </summary>
    private void Build(VisualElement root)
    {
        if (builtRoot == root && grid != null && grid.panel != null)
            return;

        // 旧树已失效：清空缓存引用和卡片，重新查询并绑定
        builtRoot = root;
        cards.Clear();

        grid = root.Q<VisualElement>("CardGrid");
        coinsText = root.Q<Label>("CoinsText");
        ownedCountText = root.Q<Label>("OwnedCountText");

        var backButton = root.Q<Button>("BackButton");
        if (backButton != null)
        {
            backButton.clicked -= OnCloseClick; // 同一棵树上防重复绑定
            backButton.clicked += OnCloseClick;
        }

        BuildCards();
    }

    /// <summary>
    /// 按角色蓝图逐个克隆卡片模板，填充初始能力并挂上按钮回调。
    /// </summary>
    private void BuildCards()
    {
        var source = characterBlueprints;
        if (source == null || source.Length == 0)
        {
            Debug.LogError("[CharacterPageUITK] 角色列表为空！请给 characterBlueprints 赋值。");
            return;
        }

        if (cardTemplate == null)
        {
            Debug.LogError("[CharacterPageUITK] 卡片模板未赋值：请在 Inspector 里指定 CharacterCard.uxml！");
            return;
        }

        grid.Clear(); // 重建时清掉旧卡片，避免同一棵树上重复叠加

        foreach (var character in source)
        {
            if (character == null) continue;

            // 克隆 UXML 模板生成卡片，按 name 取元素绑定数据；
            // CardRoot 即 .lift--card（叠层伪造投影的底板）。
            var instance = cardTemplate.Instantiate();
            var card = instance.Q<VisualElement>("Card");
            if (card == null)
            {
                Debug.LogError("[CharacterPageUITK] 卡片模板缺少 name=\"Card\" 的元素！");
                return;
            }
            card.name = "Card_" + character.uid;

            // 初始能力图标由代码按蓝图动态填充
            var abilitiesRow = instance.Q<VisualElement>("AbilitiesRow");
            if (abilitiesRow != null)
                BuildAbilities(character, abilitiesRow);

            var view = new CardView
            {
                character = character,
                root = card,
                portrait = instance.Q<Image>("Portrait"),
                name = instance.Q<Label>("NameLabel"),
                tag = instance.Q<Label>("TagLabel"),
                hp = instance.Q<Label>("HpValue"),
                armor = instance.Q<Label>("ArmorValue"),
                movespeed = instance.Q<Label>("MovespeedValue"),
                luck = instance.Q<Label>("LuckValue"),
                abilitiesRow = abilitiesRow,
                action = instance.Q<Button>("ActionButton")
            };
            if (view.action != null)
                view.action.clicked += () => OnCardAction(view);

            grid.Add(instance.Q<VisualElement>("CardRoot"));
            cards.Add(view);
        }
    }

    /// <summary>
    /// 初始能力：有图标就画图标，没有图标或没配 prefab 就退回显示名字
    /// </summary>
    private static void BuildAbilities(CharacterBlueprint character, VisualElement row)
    {
        row.Clear();

        var abilities = character.startingAbilities;
        if (abilities == null || abilities.Length == 0)
        {
            var none = new Label("无");
            none.AddToClassList("card__ability-none");
            row.Add(none);
            return;
        }

        foreach (var prefab in abilities)
        {
            if (prefab == null) continue;

            var ability = prefab.GetComponent<Ability>();
            var sprite = ability != null ? ability.Image : null;

            if (sprite != null)
            {
                var box = new VisualElement();
                box.AddToClassList("card__ability");

                var icon = new Image { scaleMode = ScaleMode.ScaleToFit, sprite = sprite };
                icon.AddToClassList("card__ability-icon");
                icon.tooltip = string.IsNullOrEmpty(ability.Name) ? prefab.name : ability.Name;

                box.Add(icon);
                row.Add(box);
            }
            else
            {
                var fallback = new Label(ability != null && !string.IsNullOrEmpty(ability.Name)
                    ? ability.Name
                    : prefab.name);
                fallback.AddToClassList("card__ability-none");
                row.Add(fallback);
            }
        }
    }

    /// <summary>
    /// 按是否解锁与金币是否足够更新每张角色卡的属性、状态文字和按钮文案。
    /// </summary>
    private void RefreshCards()
    {
        foreach (var card in cards)
        {
            var character = card.character;
            bool owned = character.IsOwned;
            int cost = character.GetCost();
            bool affordable = DataStoreLocator.Store.GetCoins() >= cost;

            var sprite = character.walkSpriteSequence != null && character.walkSpriteSequence.Length > 0
                ? character.walkSpriteSequence[0]
                : null;
            card.portrait.sprite = sprite;
            card.portrait.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;

            card.name.text = string.IsNullOrEmpty(character.name) ? character.uid : character.name;
            card.tag.text = owned ? "已解锁" : "未解锁";
            card.tag.EnableInClassList("card__tag--locked", !owned);

            card.hp.text = character.hp.ToString();
            card.armor.text = character.armor.ToString();
            // 与 uGUI 版一致：移速按 1.15 为基准换算成百分比
            card.movespeed.text = Mathf.RoundToInt(character.movespeed / 1.15f * 100f) + "%";
            card.luck.text = character.luck.ToString();

            card.root.EnableInClassList("card--owned", owned);
            card.root.EnableInClassList("card--locked", !owned);
            card.action.EnableInClassList("btn--primary", owned || affordable);
            card.action.EnableInClassList("btn--ghost", !owned && !affordable);

            if (owned)
                card.action.text = "选择角色";
            else if (affordable)
                card.action.text = "购买 (" + cost + "金币)";
            else
                card.action.text = "金币不足 (需" + cost + ")";
        }
    }

    /// <summary>
    /// 显示当前金币数以及已解锁角色占全部角色的数量。
    /// </summary>
    private void RefreshHeader()
    {
        if (coinsText != null)
            coinsText.text = DataStoreLocator.Store.GetCoins().ToString();

        if (ownedCountText != null)
        {
            int owned = 0;
            foreach (var card in cards)
            {
                if (card.character != null && card.character.IsOwned)
                    owned++;
            }
            ownedCountText.text = owned + " / " + cards.Count;
        }
    }

    /// <summary>
    /// 音效缺失（如场景里没有 AudioManager）时不能让交互失败，否则会卡在页面上
    /// </summary>
    private static void PlayClickSafely()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();
    }

    /// <summary>
    /// 点击卡片时按状态决定解锁扣费还是进入游戏，并给出对应反馈。
    /// </summary>
    private void OnCardAction(CardView card)
    {
        PlayClickSafely();

        var character = card.character;
        if (character == null) return;

        if (!character.IsOwned)
        {
            int cost = character.GetCost();

            // 金币不足时保持原状，按钮上已经标了「金币不足」，直接不动
            if (DataStoreLocator.Store.GetCoins() < cost) return;

            // 扣费与"授予角色"由服务端在一个事务里完成，成功后才刷新界面
            DataStoreLocator.Store.SpendCoins(SpendOrder.ForCharacter(character.uid, cost), outcome =>
            {
                if (!outcome.Success)
                {
                    UnityEngine.Debug.LogWarning($"[CharacterPage] 解锁角色失败：{outcome.Message}");
                    return;
                }

                CoinDisplayManager.UpdateAll();
                Refresh();
            });
            return;
        }

        StartGame(character);
    }

    /// <summary>
    /// 记录所选角色蓝图并加载游戏场景开始一局新游戏。
    /// </summary>
    private void StartGame(CharacterBlueprint character)
    {
        CrossSceneData.CharacterBlueprint = character;
        SceneManager.LoadScene(GameSceneIndex);
    }

    /// <summary>
    /// 播放点击音效后关闭当前角色选择页。
    /// </summary>
    private void OnCloseClick()
    {
        PlayClickSafely();
        Close();
    }
}
