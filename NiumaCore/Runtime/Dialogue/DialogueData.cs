using System;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaCore.Dialogue
{
    /// <summary>
    /// 定义对话数据结构，包括对话单元、对话行和对话选项
    /// 对话单元包含一个对话ID和一系列对话行，每个对话行包含一个行ID、说话人ID、说话人名称、对话文本、语音剪辑和一系列对话选项，每个对话选项包含一个选项ID、选项文本和下一行ID
    /// </summary>
    [Serializable]
    public sealed class DialogueUnit
    {
        /// <summary>
        /// 对话ID，用于标识和管理对话单元
        /// </summary>
        public string DialogueId;
        /// <summary>
        /// 对话行列表，包含对话单元中的所有对话行，每个对话行包含一个行ID、说话人ID、说话人名称、对话文本、语音剪辑和一系列对话选项
        /// </summary>
        public List<DialogueLine> Lines = new();
    }

    /// <summary>
    /// 定义对话行数据结构，包括行ID、说话人ID、说话人名称、对话文本、语音剪辑和对话选项
    /// 行ID用于标识和管理对话行，说话人ID和说话人名称用于显示对话界面中的说话人信息，对话文本用于显示对话内容，语音剪辑用于播放对话语音，对话选项用于提供玩家选择的分支路径
    /// </summary>
    [Serializable]
    public sealed class DialogueLine
    {
        /// <summary>
        /// 行ID，用于标识和管理对话行
        /// </summary>
        public string LineId;
        /// <summary>
        /// 说话人ID，用于标识和管理说话人信息，例如在对话界面中显示说话人头像、名称等信息时使用
        /// </summary>
        public string SpeakerId;
        /// <summary>
        /// 说话人名称，用于显示对话界面中的说话人信息，例如在对话界面中显示说话人名称时使用
        /// </summary>
        public string SpeakerName;
        /// <summary>
        /// 对话文本，用于显示对话内容，例如在对话界面中显示对话文本时使用
        /// </summary>
        [TextArea(2, 5)]
        public string Text;
        /// <summary>
        /// 语音剪辑，用于播放对话语音，例如在对话界面中播放对话语音时使用
        /// </summary>
        public AudioClip VoiceClip;
        /// <summary>
        /// 对话选项列表，包含当前对话行的所有对话选项，每个对话选项包含一个选项ID、选项文本和下一行ID，用于提供玩家选择的分支路径，例如在对话界面中显示对话选项时使用
        /// </summary>
        public List<DialogueChoice> Choices = new();
    }

    /// <summary>
    /// 定义对话选项数据结构，包括选项ID、选项文本和下一行ID
    /// 选项ID用于标识和管理对话选项，选项文本用于显示对话界面中的选项内容，下一行ID用于指示玩家选择该选项后应该跳转到哪个对话行，例如在对话界面中显示对话选项时使用
    /// </summary>
    [Serializable]
    public sealed class DialogueChoice
    {
        /// <summary>
        /// 选项ID，用于标识和管理对话选项，例如在对话界面中显示对话选项时使用
        /// </summary>
        public string ChoiceId;
        /// <summary>
        /// 选项文本，用于显示对话界面中的选项内容，例如在对话界面中显示对话选项时使用
        /// </summary>
        public string Text;
        /// <summary>
        /// 下一行ID，用于指示玩家选择该选项后应该跳转到哪个对话行，例如在对话界面中显示对话选项时使用
        /// </summary>
        public string NextLineId;
    }
}
