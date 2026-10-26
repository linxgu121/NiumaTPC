namespace NiumaTPC.Character.Event
{
    /// <summary>
    /// 角色面部表情反馈事件
    /// </summary>
    public enum PlayerFacialEvent
    {
        None = 0,

        //基础反馈
        Attack,
        Jump,
        Land,
        Hurt,
        Death,

        //快捷表情
        QuickExpression1,
        QuickExpression2, 
        QuickExpression3,
        QuickExpression4,  


    }
}