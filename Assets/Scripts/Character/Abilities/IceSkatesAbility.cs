public class IceSkatesAbility : FloatUpgradeAbility<UpgradeableMovementSpeed>
{
    /// <summary>
    /// 选中后直接提升移速并播放音效。
    /// </summary>
    public override void Select()
    {
        base.Select();
        playerCharacter.UpdateMoveSpeed();
    }

    /// <summary>
    /// 移速尚未达到上限时才可出现。
    /// </summary>
    public override bool RequirementsMet()
    {
        bool baseRequirementsMet = base.RequirementsMet();
        bool movementSpeedInUse = abilityManager.MovementSpeedUpgradeablesCount > 0;
        return baseRequirementsMet && movementSpeedInUse;
    }
}
