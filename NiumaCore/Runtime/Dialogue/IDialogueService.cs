namespace NiumaCore.Dialogue
{
    /// <summary>
    /// 定义对话服务接口，用于管理游戏中的对话系统
    /// 对话服务负责处理对话的启动、运行和结束逻辑，允许模块通过对话ID和上下文参数来启动对话
    /// 并提供强制关闭对话的功能，以便在特定情况下（例如玩家离开对话区域、游戏状态变化等）及时结束对话，确保游戏体验的连贯性和一致性。
    /// </summary>
    public interface IDialogueService
    {
        /// <summary>
        /// 对话是否正在运行
        /// </summary>
        bool IsDialogueRunning { get; }

        /// <summary>
        /// 启动对话，接受一个对话ID参数和一个可选的上下文参数，允许模块根据对话ID来启动对应的对话
        /// 并通过上下文参数传递必要的数据和信息，例如在启动角色对话时传递角色ID，在启动任务对话时传递任务ID等
        /// </summary>
        void StartDialogue(string dialogueId, object context = null);
        /// <summary>
        /// 强制关闭对话，允许模块在特定情况下（例如玩家离开对话区域、游戏状态变化等）及时结束对话，确保游戏体验的连贯性和一致性。
        /// </summary>
        void ForceCloseDialogue();
    }
}
