namespace NiumaTPC.Character.Combat
{
    /// <summary>
    /// 为公共近战模拟与命中层提供当前权威攻击配置
    /// 实现者可以读取玩家武器或怪物自身攻击能力
    /// </summary>
    public interface IMeleeAttackSource
    {
        /// <summary>
        /// 检查指定攻击通道能否生成当前固定Tick使用的攻击定义
        /// 无效配置、未知通道和未确认来源不能返回成功
        /// </summary>
        bool TryGetAttackDefinition(MeleeAttackType attackType,float tickDeltaTime, out MeleeAttackDefinition definition);
    }
}