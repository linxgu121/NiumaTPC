using System.Collections;
using System.Collections.Generic;
using NiumaTPC.Character.Input.Base;
using NiumaTPC.Character.RuntimeData;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NiumaTPC.Character.Input.Player
{
    public class PlayerInputSource : InputSourceBase
    {
        #region 设置参数
        [Header("视角设置")]
        [Tooltip("鼠标灵敏度")]
        public float mouseSensitivity = 1f;
        [Tooltip("鼠标X轴反转")]
        public bool invertMouseX = false;
        [Tooltip("鼠标Y轴反转")]
        public bool invertMouseY = false;

        #endregion

        #region 输入动作
        [Header("输入动作引用")]
        public InputActionReference moveAction;
        public InputActionReference lookAction;
        public InputActionReference jumpAction;
        public InputActionReference sprintAction;
        public InputActionReference walkAction;
        public InputActionReference aimAction;
        public InputActionReference interactAction;
        public InputActionReference dodgeAction;
        public InputActionReference rollAction;
        public InputActionReference actionAction;
        public InputActionReference LeftMouseAction;
        public InputActionReference number1Action;
        public InputActionReference number2Action;
        public InputActionReference number3Action;
        public InputActionReference number4Action;
        public InputActionReference number5Action;
        [Header("表情输入")]
        public InputActionReference expression1Action;
        public InputActionReference expression2Action;
        public InputActionReference expression3Action;
        public InputActionReference expression4Action;

        #endregion

        private void OnEnable() => ToggleActions(true);
        private void OnDisable() => ToggleActions(false);

        public override void FetchRawInput(ref RawInputData rawData)
        {
            rawData.MoveAxis = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

            Vector2 rawLook = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;

            rawLook.x *= mouseSensitivity * (invertMouseX ? -1f : 1f);

            rawLook.y *= mouseSensitivity * (invertMouseY ? -1f : 1f);

            rawData.LookAxis = rawLook;

            /*
             * RawInputData 会在多帧之间复用
             * 每次采样前必须清空按钮位，避免释放按键后遗留旧状态
             */
            rawData.HeldButtons = RawInputButtons.None;

            rawData.PressedButtons = RawInputButtons.None;

            SampleButton(ref rawData,RawInputButtons.Jump,jumpAction);

            SampleButton(ref rawData,RawInputButtons.Dodge,dodgeAction);

            SampleButton(ref rawData,RawInputButtons.Roll,rollAction);

            SampleButton(ref rawData,RawInputButtons.Sprint,sprintAction);

            SampleButton(ref rawData,RawInputButtons.Walk,walkAction);

            SampleButton(ref rawData,RawInputButtons.Aim,aimAction);

            SampleButton(ref rawData,RawInputButtons.Interact,interactAction);

            /*
             * 输入动作引用暂时保留原序列化名称
             * 角色逻辑统一把它解释为 Fire
             */
            SampleButton(ref rawData,RawInputButtons.Fire,LeftMouseAction);

            SampleButton(ref rawData,RawInputButtons.Expression1,expression1Action);

            SampleButton(ref rawData,RawInputButtons.Expression2,expression2Action);

            SampleButton(ref rawData,RawInputButtons.Expression3,expression3Action);

            SampleButton(ref rawData,RawInputButtons.Expression4,expression4Action);

            SampleButton(ref rawData,RawInputButtons.Number1,number1Action);

            SampleButton(ref rawData,RawInputButtons.Number2,number2Action);

            SampleButton(ref rawData,RawInputButtons.Number3,number3Action);

            SampleButton(ref rawData,RawInputButtons.Number4,number4Action);

            SampleButton(ref rawData,RawInputButtons.Number5,number5Action);

            SampleButton(ref rawData,RawInputButtons.Action,actionAction);
        }

        /// <summary>
        /// 从一个 Input Action 采样持续状态与当前帧按下边沿
        /// </summary>
        private static void SampleButton(
            ref RawInputData rawData,
            RawInputButtons button,
            InputActionReference actionReference)
        {
            InputAction action =
                actionReference != null
                    ? actionReference.action
                    : null;

            if (action == null)
            {
                return;
            }

            if (action.IsPressed())
            {
                rawData.HeldButtons |= button;
            }

            if (action.WasPressedThisFrame())
            {
                rawData.PressedButtons |= button;
            }
        }

        /// <summary>
        /// 统一开关输入动作
        /// </summary>
        private void ToggleActions(bool enable)
        {
            InputActionReference[] all = {
                moveAction, lookAction, jumpAction, sprintAction, walkAction,
                aimAction, interactAction, dodgeAction, rollAction, actionAction, LeftMouseAction,
                number1Action, number2Action, number3Action, number4Action, number5Action,
                expression1Action, expression2Action, expression3Action, expression4Action
            };
            //遍历所有动作并处理
            foreach (var ar in all)
            {
                //没赋值跳过
                if (ar == null) continue;
                //为true则监听
                if (enable) ar.action.Enable();
                //为false禁用监听
                else ar.action.Disable();
            }
        }
    }
}
