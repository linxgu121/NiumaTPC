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
            //轴向输入
            rawData.MoveAxis = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            Vector2 rawLook = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;
            rawLook.x *= mouseSensitivity * (invertMouseX ? -1f : 1f);
            rawLook.y *= mouseSensitivity * (invertMouseY ? -1f : 1f);
            rawData.LookAxis = rawLook;

            //长按状态采样
            rawData.JumpHeld = jumpAction != null && jumpAction.action.IsPressed();
            rawData.DodgeHeld = dodgeAction != null && dodgeAction.action.WasPressedThisFrame();
            rawData.RollHeld = rollAction != null && rollAction.action.WasPressedThisFrame();
            rawData.SprintHeld = sprintAction != null && sprintAction.action.IsPressed();
            rawData.WalkHeld = walkAction != null && walkAction.action.IsPressed();
            rawData.AimHeld = aimAction != null && aimAction.action.IsPressed();
            rawData.Expression1Held = expression1Action != null && expression1Action.action.IsPressed();
            rawData.Expression2Held = expression2Action != null && expression2Action.action.IsPressed();
            rawData.Expression3Held = expression3Action != null && expression3Action.action.IsPressed();
            rawData.Expression4Held = expression4Action != null && expression4Action.action.IsPressed();
            rawData.Number1Held = number1Action != null && number1Action.action.IsPressed();
            rawData.Number2Held = number2Action != null && number2Action.action.IsPressed();
            rawData.Number3Held = number3Action != null && number3Action.action.IsPressed();
            rawData.Number4Held = number4Action != null && number4Action.action.IsPressed();
            rawData.Number5Held = number5Action != null && number5Action.action.IsPressed();
            rawData.ActionHeld = actionAction != null && actionAction.action.IsPressed();
            rawData.LeftMouseHeld = LeftMouseAction != null && LeftMouseAction.action.IsPressed();
            rawData.FireHeld = rawData.LeftMouseHeld;

            //瞬时短按数据
            rawData.JumpJustPressed = jumpAction != null && jumpAction.action.WasPressedThisFrame();
            rawData.DodgeJustPressed = dodgeAction != null && dodgeAction.action.WasPressedThisFrame();
            rawData.RollJustPressed = rollAction != null && rollAction.action.WasPressedThisFrame();
            rawData.LeftMouseJustPressed = LeftMouseAction != null && LeftMouseAction.action.WasPressedThisFrame();
            rawData.FireJustPressed = rawData.LeftMouseJustPressed; 
            rawData.Expression1JustPressed = expression1Action != null && expression1Action.action.WasPressedThisFrame();
            rawData.Expression2JustPressed = expression2Action != null && expression2Action.action.WasPressedThisFrame();
            rawData.Expression3JustPressed = expression3Action != null && expression3Action.action.WasPressedThisFrame();
            rawData.Expression4JustPressed = expression4Action != null && expression4Action.action.WasPressedThisFrame();
            rawData.Number1JustPressed = number1Action != null && number1Action.action.WasPressedThisFrame();
            rawData.Number2JustPressed = number2Action != null && number2Action.action.WasPressedThisFrame();
            rawData.Number3JustPressed = number3Action != null && number3Action.action.WasPressedThisFrame();
            rawData.Number4JustPressed = number4Action != null && number4Action.action.WasPressedThisFrame();
            rawData.Number5JustPressed = number5Action != null && number5Action.action.WasPressedThisFrame();
            rawData.ActionJustPressed = actionAction != null && actionAction.action.WasPressedThisFrame(); 

        }

        /// <summary>
        /// 统一开关输入动作
        /// </summary>
        private void ToggleActions(bool enable)
        {
            InputActionReference[] all = {
                moveAction, lookAction, jumpAction, sprintAction, walkAction,
                aimAction, dodgeAction, rollAction, actionAction, LeftMouseAction,
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
