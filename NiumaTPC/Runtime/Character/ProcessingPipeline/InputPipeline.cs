using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Input.Base;
using NiumaTPC.Character.RuntimeData;
using UnityEngine;

namespace NiumaTPC.Character.ProcessingPipeline
{
    /// <summary>
    /// 系统流转的第一道关卡 唯一的数据生产者
    /// 数据流向：从 IInputSource提取原始输入数据 - 栈上清洗 (防抖/缓存) - 压入堆内存黑板 (InputData)
    /// 拥有绝对的写入权限  对外部仅暴露只读引用 外部系统只能通过 Consume 接口进行受控的数据核销
    /// </summary>
    public class InputPipeline
    {
        // 输入源接口（现在使用具体基类以便读取序列化配置）
        private readonly InputSourceBase _inputSource;

        //  堆内存容器
        private InputData _inputData;

        // 栈上瞬时数据缓存
        private RawInputData _rawData;
        private Vector2 _bufferedMove;
        private float _lastNonZeroMoveTime;

        // 配置参数
        // WASD指令缓存时间
        private readonly float _inputFlickerBuffer;
        // 动作指令缓存时间  将原来单帧的硬件触发 拉长为 N 秒的合法“意愿期”
        private readonly float _actionBufferTime;

        // 全局物理帧计数器
        private ulong _frameIndex;

        private bool _isBlocked;

        /// <summary>
        /// 对外暴露的当前输入快照
        /// 其他系统只能读取此引用 绝对禁止直接修改内部字段
        /// </summary>
        public InputData Current => _inputData;

        /// <summary>
        /// 输入阻塞状态 供外部系统查询当前输入是否被管线阻塞
        /// </summary>
        public bool IsBlocked => _isBlocked;

        // 构造函数只接受 InputSourceBase 其他配置从 inputSource 注入；如果 inputSource 未配置则使用其字段的默认值
        public InputPipeline(InputSourceBase inputSource)
        {
            _inputSource = inputSource;
            _inputFlickerBuffer = _inputSource.InputFlickerBuffer;
            _actionBufferTime = _inputSource.ActionBufferTime;

            // 管线作为数据的绝对源头 自行分配容器 避免GC
            _inputData = new InputData();
            _inputData.currentFrameData = new FrameInputData { FrameIndex = 0 };
            _inputData.lastFrameData = new FrameInputData { FrameIndex = 0 };

            _rawData = default;
            _bufferedMove = Vector2.zero;
            _lastNonZeroMoveTime = Time.time;
            _frameIndex = 0;
            _isBlocked = false;
        }

        /// <summary>
        /// 输入阻塞接口 供外部系统调用以临时禁用玩家输入
        /// </summary>
        public void SetBlocked(bool blocked, bool clearBufferedInput = true)
        {
            if (_isBlocked == blocked)
                return;

            _isBlocked = blocked;

            if (blocked && clearBufferedInput)
                ClearBufferedInput();
        }

        /// <summary>
        /// 清空输入缓存 供外部系统调用以在特定事件（如场景切换、角色复活）后重置输入状态
        /// </summary>
        public void ClearBufferedInput()
        {
            _rawData = default;
            _bufferedMove = Vector2.zero;
            _lastNonZeroMoveTime = float.NegativeInfinity;

            var frame = new FrameInputData
            {
                FrameIndex = _frameIndex,
                Raw = default,
                Processed = default
            };

            _inputData.lastFrameData = frame;
            _inputData.currentFrameData = frame;
        }

        /// <summary>
        /// playercontroller Update 最优先调用的函数
        /// 负责历史快照更迭 并拉起一轮新的硬件数据采样
        /// </summary>
        public void Update()
        {
            // 推进历史帧
            _inputData.lastFrameData = _inputData.currentFrameData;

            if (_isBlocked || (_inputSource != null && _inputSource.IsBlocked))
            {
                ClearBufferedInput();
                _frameIndex++;
                return;
            }
            else
            {
                // 采样硬件真实状态
                _inputSource.FetchRawInput(ref _rawData);
            }

            // 后处理数据并压入内存
            ProcessRawInput();

            _frameIndex++;
        }

        /// <summary>
        /// 输入数据的后处理方法
        /// </summary>
        private void ProcessRawInput()
        {
            var currentFrame = new FrameInputData
            {
                FrameIndex = _frameIndex,
                Raw = _rawData,
                Processed = default
            };

            // 输入轴防抖处理
            if (_rawData.MoveAxis.sqrMagnitude > 0.01f)
            {
                _bufferedMove = _rawData.MoveAxis;
                _lastNonZeroMoveTime = Time.time;
                currentFrame.Processed.Move = _rawData.MoveAxis;
            }
            else if (Time.time - _lastNonZeroMoveTime < _inputFlickerBuffer)
            {
                // 处于防抖窗口内 使用缓存的最后一次有效值
                currentFrame.Processed.Move = _bufferedMove;
            }
            else
            {
                currentFrame.Processed.Move = Vector2.zero;
            }

            // 注：LookAxis属于Delta(增量)数据 绝对禁止在此处 SmoothDamp 直接原样透传给摄像机逻辑 不然视角会像弹簧一样回到原位
            currentFrame.Processed.Look = _rawData.LookAxis;

            // 将原始按钮位转换成现有处理层接口
            currentFrame.Processed.JumpHeld = _rawData.IsHeld(RawInputButtons.Jump);

            currentFrame.Processed.DodgeHeld = _rawData.IsHeld(RawInputButtons.Dodge);

            currentFrame.Processed.RollHeld = _rawData.IsHeld(RawInputButtons.Roll);

            currentFrame.Processed.SprintHeld = _rawData.IsHeld(RawInputButtons.Sprint);

            currentFrame.Processed.WalkHeld = _rawData.IsHeld(RawInputButtons.Walk);

            currentFrame.Processed.AimHeld = _rawData.IsHeld(RawInputButtons.Aim);

            currentFrame.Processed.InteractHeld = _rawData.IsHeld(RawInputButtons.Interact);

            bool fireHeld = _rawData.IsHeld(RawInputButtons.Fire);

            currentFrame.Processed.FireHeld = fireHeld;

            currentFrame.Processed.LeftMouseHeld = fireHeld;

            currentFrame.Processed.Expression1Held = _rawData.IsHeld(RawInputButtons.Expression1);

            currentFrame.Processed.Expression2Held = _rawData.IsHeld(RawInputButtons.Expression2);

            currentFrame.Processed.Expression3Held = _rawData.IsHeld(RawInputButtons.Expression3);

            currentFrame.Processed.Expression4Held = _rawData.IsHeld(RawInputButtons.Expression4);

            currentFrame.Processed.Number1Held = _rawData.IsHeld(RawInputButtons.Number1);

            currentFrame.Processed.Number2Held = _rawData.IsHeld(RawInputButtons.Number2);

            currentFrame.Processed.Number3Held = _rawData.IsHeld(RawInputButtons.Number3);

            currentFrame.Processed.Number4Held = _rawData.IsHeld(RawInputButtons.Number4);

            currentFrame.Processed.Number5Held = _rawData.IsHeld(RawInputButtons.Number5);

            currentFrame.Processed.ActionHeld = _rawData.IsHeld(RawInputButtons.Action);

            // 动作缓存池调度
            // 一旦硬件触发 JustPressed 给对应的Timer充能 随后随时间衰减。
            // 外部读取的 bool Pressed 是依赖此 Timer 的计算属性
            float dt = Time.deltaTime;
            var lastProc = _inputData.lastFrameData.Processed;

            float UpdateBuffer(float lastTimer, bool justPressed)
            {
                float newTimer = Mathf.Max(0f, lastTimer - dt);
                if (justPressed) newTimer = _actionBufferTime;
                return newTimer;
            }

            currentFrame.Processed.SlideBufferTimer = UpdateBuffer(lastProc.SlideBufferTimer, _rawData.WasPressed(RawInputButtons.Walk));

            currentFrame.Processed.JumpBufferTimer = UpdateBuffer(lastProc.JumpBufferTimer, _rawData.WasPressed(RawInputButtons.Jump));

            currentFrame.Processed.DodgeBufferTimer = UpdateBuffer(lastProc.DodgeBufferTimer, _rawData.WasPressed(RawInputButtons.Dodge));

            currentFrame.Processed.RollBufferTimer = UpdateBuffer(lastProc.RollBufferTimer, _rawData.WasPressed(RawInputButtons.Roll));

            bool firePressed = _rawData.WasPressed(RawInputButtons.Fire);

            currentFrame.Processed.FireBufferTimer = UpdateBuffer(lastProc.FireBufferTimer, firePressed);

            currentFrame.Processed.LeftMouseBufferTimer = currentFrame.Processed.FireBufferTimer;

            currentFrame.Processed.Expression1BufferTimer = UpdateBuffer(lastProc.Expression1BufferTimer, _rawData.WasPressed(RawInputButtons.Expression1));

            currentFrame.Processed.Expression2BufferTimer = UpdateBuffer(lastProc.Expression2BufferTimer, _rawData.WasPressed(RawInputButtons.Expression2));

            currentFrame.Processed.Expression3BufferTimer = UpdateBuffer(lastProc.Expression3BufferTimer, _rawData.WasPressed(RawInputButtons.Expression3));

            currentFrame.Processed.Expression4BufferTimer = UpdateBuffer(lastProc.Expression4BufferTimer, _rawData.WasPressed(RawInputButtons.Expression4));

            currentFrame.Processed.Number1BufferTimer = UpdateBuffer(lastProc.Number1BufferTimer, _rawData.WasPressed(RawInputButtons.Number1));

            currentFrame.Processed.Number2BufferTimer = UpdateBuffer(lastProc.Number2BufferTimer, _rawData.WasPressed(RawInputButtons.Number2));

            currentFrame.Processed.Number3BufferTimer = UpdateBuffer(lastProc.Number3BufferTimer, _rawData.WasPressed(RawInputButtons.Number3));

            currentFrame.Processed.Number4BufferTimer = UpdateBuffer(lastProc.Number4BufferTimer, _rawData.WasPressed(RawInputButtons.Number4));

            currentFrame.Processed.Number5BufferTimer = UpdateBuffer(lastProc.Number5BufferTimer, _rawData.WasPressed(RawInputButtons.Number5));

            currentFrame.Processed.ActionBufferTimer = UpdateBuffer(lastProc.ActionBufferTimer, _rawData.WasPressed(RawInputButtons.Action));

            // 将局部计算完毕的纯净数据 一次性写回堆内存 供全局读取
            _inputData.currentFrameData = currentFrame;
        }

        // 消费仲裁接口 
        // IntentProcessor (意图判定) 或 State在动作确立时调用
        // 调用后 Timer 瞬间归零 配合 实现同帧内核销
        public void ConsumeSlidePressed() { var frame = _inputData.currentFrameData; frame.Processed.SlideBufferTimer = 0f; _inputData.currentFrameData = frame; }
        public void ConsumeJumpPressed() { var f = _inputData.currentFrameData; f.Processed.JumpBufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeDodgePressed() { var f = _inputData.currentFrameData; f.Processed.DodgeBufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeRollPressed() { var f = _inputData.currentFrameData; f.Processed.RollBufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression1Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression1BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression2Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression2BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression3Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression3BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression4Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression4BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber1Pressed() { var f = _inputData.currentFrameData; f.Processed.Number1BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber2Pressed() { var f = _inputData.currentFrameData; f.Processed.Number2BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber3Pressed() { var f = _inputData.currentFrameData; f.Processed.Number3BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber4Pressed() { var f = _inputData.currentFrameData; f.Processed.Number4BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber5Pressed() { var f = _inputData.currentFrameData; f.Processed.Number5BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeActionPressed() { var f = _inputData.currentFrameData; f.Processed.ActionBufferTimer = 0f; _inputData.currentFrameData = f; }
        
        /// <summary>
        /// 消费攻击按下缓存并同步清理旧鼠标字段
        /// </summary>
        public void ConsumeFirePressed() { var frame = _inputData.currentFrameData; frame.Processed.FireBufferTimer = 0f; frame.Processed.LeftMouseBufferTimer = 0f; _inputData.currentFrameData = frame; }

        /// <summary>
        /// 兼容仍在使用旧鼠标命名的调用方
        /// </summary>
        public void ConsumeLeftMousePressed() { ConsumeFirePressed(); }
    }
}
