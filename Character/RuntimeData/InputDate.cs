using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NiumaTPC.Item.RuntimeData
{
    /// <summary>
    /// 原始输入数据（只记录硬件输入）
    /// </summary>
    public struct RawInputData
    {
        /// <summary>
        /// 记录移动输入轴
        /// </summary>
        public Vector2 MoveAxis;
        /// <summary>
        /// 记录视角移动轴
        /// </summary>
        public Vector2 LookAxis;
        /// <summary>
        /// 跳（长按）
        /// </summary>
        public bool JumpHeld;
        /// <summary>
        /// 闪避（长按）
        /// </summary>
        public bool DodgeHeld;
        /// <summary>
        /// 翻滚（长按）
        /// </summary>
        public bool RollHeld;
        /// <summary>
        /// 奔跑（长按）
        /// </summary>
        public bool SprintHeld;
        /// <summary>
        /// 走（长按）
        /// </summary>
        public bool WalkHeld;
        /// <summary>
        /// 瞄准（长按）
        /// </summary>
        public bool AimHeld;
        /// <summary>
        /// 交互（长按）
        /// </summary>
        public bool InteractHeld;
        /// <summary>
        /// 开火(长按)
        /// </summary>
        public bool FireHeld;
        
        /// <summary>
        /// 表情1（长按）
        /// </summary>
        public bool Expression1Held;
        /// <summary>
        /// 表情2（长按）
        /// </summary>
        public bool Expression2Held;
        /// <summary>
        /// 表情3（长按）
        /// </summary>
        public bool Expression3Held;
        /// <summary>
        /// 表情4（长按）
        /// </summary>
        public bool Expression4Held;
        public bool Number1Held;
        public bool Number2Held;
        public bool Number3Held;
        public bool Number4Held;
        public bool Number5Held;
        /// <summary>
        /// 动作（长按）
        /// </summary>
        public bool ActionHeld;
        /// <summary>
        /// 鼠标右键（长按）
        /// </summary>
        public bool LeftMouseHeld;

        /// <summary>
        /// 跳（按下）
        /// </summary>
        public bool JumpJustPressed;
        /// <summary>
        /// 闪避（按下）
        /// </summary>
        public bool DodgeJustPressed;
        /// <summary>
        /// 翻滚（按下）
        /// </summary>
        public bool RollJustPressed;
        /// <summary>
        /// 开火（按下）
        /// </summary>
        public bool FireJustPressed;

        /// <summary>
        /// 表情1（按下）
        /// </summary>
        public bool Expression1JustPressed;
        /// <summary>
        /// 表情2（按下）
        /// </summary>
        public bool Expression2JustPressed;
        /// <summary>
        /// 表情3（按下）
        /// </summary>
        public bool Expression3JustPressed;
        /// <summary>
        /// 表情4（按下）
        /// </summary>
        public bool Expression4JustPressed;

        public bool Number1JustPressed;
        public bool Number2JustPressed;
        public bool Number3JustPressed;
        public bool Number4JustPressed;
        public bool Number5JustPressed;

        /// <summary>
        /// 动作（按下）
        /// </summary>
        public bool ActionJustPressed;
        /// <summary>
        /// 鼠标右键（按下）
        /// </summary>
        public bool LeftMouseJustPressed;

    }

    /// <summary>
    /// 处理后的输入数据
    /// 逻辑真正的意愿快照
    /// </summary>
    public struct ProcessedInputData
    {
        /// <summary>
        /// 处理后的移动轴
        /// </summary>
        public Vector2 Move;
        /// <summary>
        /// 处理后的视角移动轴
        /// </summary>
        public Vector2 Look;
        
        /// <summary>
        /// 跳（长按）
        /// </summary>
        public bool JumpHeld;
        /// <summary>
        /// 闪避（长按）
        /// </summary>
        public bool DodgeHeld;
        /// <summary>
        /// 翻滚（长按）
        /// </summary>
        public bool RollHeld;
        /// <summary>
        /// 奔跑（长按）
        /// </summary>
        public bool SprintHeld;
        /// <summary>
        /// 走（长按）
        /// </summary>
        public bool WalkHeld;
        /// <summary>
        /// 瞄准（长按）
        /// </summary>
        public bool AimHeld;
        /// <summary>
        /// 交互（长按）
        /// </summary>
        public bool InteractHeld;
        /// <summary>
        /// 开火(长按)
        /// </summary>
        public bool FireHeld;
        
         /// <summary>
        /// 表情1（长按）
        /// </summary>
        public bool Expression1Held;
        /// <summary>
        /// 表情2（长按）
        /// </summary>
        public bool Expression2Held;
        /// <summary>
        /// 表情3（长按）
        /// </summary>
        public bool Expression3Held;
        /// <summary>
        /// 表情4（长按）
        /// </summary>
        public bool Expression4Held;

        public bool Number1Held;
        public bool Number2Held;
        public bool Number3Held;
        public bool Number4Held;
        public bool Number5Held;

        /// <summary>
        /// 动作（长按）
        /// </summary>
        public bool ActionHeld;
        /// <summary>
        /// 鼠标右键（长按）
        /// </summary>
        public bool LeftMouseHeld;

        //缓存计时器
        public float JumpBufferTimer;
        public float DodgeBufferTimer;
        public float RollBufferTimer;
        public float FireBufferTimer;
        public float Expression1BufferTimer;
        public float Expression2BufferTimer;
        public float Expression3BufferTimer;
        public float Expression4BufferTimer;
        public float Number1BufferTimer;
        public float Number2BufferTimer;
        public float Number3BufferTimer;
        public float Number4BufferTimer;
        public float Number5BufferTimer;
        public float ActionBufferTimer;
        public float LeftMouseBufferTimer;

        //判断输入缓冲是否有效
        //预输入
        public bool JumpPressed => JumpBufferTimer > 0f;
        public bool DodgePressed => DodgeBufferTimer > 0f;
        public bool RollPressed => RollBufferTimer > 0f;
        public bool FirePressed => FireBufferTimer > 0f;
        public bool Expression1Pressed => Expression1BufferTimer > 0f;
        public bool Expression2Pressed => Expression2BufferTimer > 0f;
        public bool Expression3Pressed => Expression3BufferTimer > 0f;
        public bool Expression4Pressed => Expression4BufferTimer > 0f;
        public bool Number1Pressed => Number1BufferTimer > 0f;
        public bool Number2Pressed => Number2BufferTimer > 0f;
        public bool Number3Pressed => Number3BufferTimer > 0f;
        public bool Number4Pressed => Number4BufferTimer > 0f;
        public bool Number5Pressed => Number5BufferTimer > 0f;
        public bool ActionPressed => ActionBufferTimer > 0f;
        public bool LeftMousePressed => LeftMouseBufferTimer > 0f;
    }

    /// <summary>
    /// 打包一帧的输入数据
    /// </summary>
    public struct FrameInputData
    {
        /// <summary>
        /// 帧索引
        /// </summary>
        public ulong FrameIndex;
        /// <summary>
        /// 原始输入
        /// </summary>
        public RawInputData Raw;
        /// <summary>
        /// 处理后的输入
        /// </summary>
        public ProcessedInputData Processed;
    }

    /// <summary>
    /// 全局输入状态数据
    /// 固定输入更新时序（记住上一帧，持有下一帧）
    /// 唯一输入入口
    /// </summary>
    public class InputData
    {
        /// <summary>
        /// 当前帧
        /// </summary>
        public FrameInputData currentFrameData;
        /// <summary>
        /// sh==上一帧
        /// </summary>
        public FrameInputData lastFrameData;
    }
}