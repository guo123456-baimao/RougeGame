using System.Collections.Generic;
using UnityEngine;
public class AbilitySelectionDialog : DialogBox
{
    [SerializeField] private Transform abilityCardsParent;
    [SerializeField] private GameObject abilityCardPrefab;
    [SerializeField] private PauseMenu pauseMenu;
    [SerializeField] private GameObject particles;
    [SerializeField] private ChestBlueprint failsafeChestBlueprint;
    [SerializeField] private float cardPopupDelay = 0.1f;
    private AbilityManager abilityManager;
    private EntityManager entityManager;
    private Character playerCharacter;
    private List<AbilityCard> abilityCards;
    private List<Ability> displayedAbilities;
    private bool menuOpen = false;
    public bool MenuOpen { get => menuOpen; }

    /// <summary>
    /// 注入技能管理器等依赖，供后续抽卡与选卡使用。
    /// </summary>
    public void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
    {
        this.abilityManager = abilityManager;
        this.entityManager = entityManager;
        this.playerCharacter = playerCharacter;
    }

    public void Open(bool failsafe = true)
    {
        base.Open();
        menuOpen = true;
        Time.timeScale = 0;
        pauseMenu.TimeIsFrozen = true;
        particles.SetActive(true);
        // 弹夹的连发音跟着游戏一起冻结，别在选升级时一直响
        if (AudioManager.Instance != null)
            AudioManager.Instance.PauseMachineGunLoop();

        // 抽取要展示的技能/升级选项
        displayedAbilities = abilityManager.SelectAbilities();
        if (displayedAbilities.Count > 0)
        {
            Populate(displayedAbilities);
        }
        // 已无技能可选时的兜底处理：关闭界面并改为生成一个宝箱
        else
        {
            if (failsafe)
                entityManager.SpawnChest(failsafeChestBlueprint, (Vector2)playerCharacter.transform.position + Vector2.up);
            Close();
        }
    }

    /// <summary>
    /// 按抽到的技能列表实例化卡片并排布到界面上。
    /// </summary>
    private void Populate(List<Ability> abilities)
    {
        if (abilityCards == null)
            abilityCards = new List<AbilityCard>();

        int i = 0;
        for (; i < abilities.Count; i++)
        {
            if (i >= abilityCards.Count)
                abilityCards.Add(Instantiate(abilityCardPrefab, abilityCardsParent).GetComponent<AbilityCard>());
            // 必须在 Init 之前激活：Init 会启动出现动画协程，未激活的物体上协程无法执行
            abilityCards[i].gameObject.SetActive(true);
            abilityCards[i].Init(this, abilities[i], cardPopupDelay*i);
        }
        for (; i < abilityCards.Count; i++)
        {
            abilityCards[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 关闭选卡界面并恢复游戏时间。
    /// </summary>
    public override void Close()
    {
        AudioManager.Instance.PlayClick();
        abilityManager.ReturnAbilities(displayedAbilities);
        menuOpen = false;
        Time.timeScale = 1;
        pauseMenu.TimeIsFrozen = false;
        particles.SetActive(false);
        if (AudioManager.Instance != null)
            AudioManager.Instance.ResumeMachineGunLoop();
        base.Close();
    }

    /// <summary>
    /// 判断是否还有技能可供选择。
    /// </summary>
    public bool HasAvailableAbilities()
    {
        return abilityManager.HasAvailableAbilities();
    }
}
