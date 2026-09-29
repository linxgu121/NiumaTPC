using System;
using NiumaTPC.Character;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NiumaTPC.Role
{
    /// <summary>
    /// 创建和清理单机TPC角色
    /// </summary>
    [DisallowMultipleComponent]
    public class OfflineCharacterSpawner : MonoBehaviour
    {
        public NiumaCharacterController CurrentPlayer { get; private set; }

        //未激活的创建容器，确保可以在角色 Awake 前准备属性
        private GameObject _inactiveRoot;

        //标记是否生成
        private bool _isSpawning;

        /// <summary>
        /// 生成玩家角色实例
        /// </summary>
        public bool TrySpawn(
            CharacterDefinitionSO definition,
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            out string error)
        {
            if (!Application.isPlaying)
            {
                error = "角色生成器只能在运行模式下使用。";
                return false;
            }

            if (!isActiveAndEnabled)
            {
                error = "组件没有启用";
                return false;
            }

            if (_isSpawning)
            {
                error = "正在生成角色，请勿重复调用。";
                return false;
            }

            if (CurrentPlayer != null)
            {
                error = "已有存在玩家实例,请先清理当前角色";
                return false;
            }

            if (prefab == null)
            {
                error = "角色预制体为空";
                return false;
            }

            if (prefab.GetComponent<NiumaCharacterController>() == null)
            {
                error = "预制体根节点缺少 NiumaCharacterController";
                return false;
            }

            if (!CharacterBaseStatsFactory.TryCreate(definition, out var baseStats, out var statsErr))
            {
                error = statsErr;
                return false;
            }

            GameObject instance = null;
            bool completed = false;
            _isSpawning = true;

            try
            {
                EnsureInactiveRoot();

                //TODO：后期更换对象池，从对象池中取出

                // 在未激活容器下创建，保证此时还没有执行 Awake。
                instance = Instantiate(prefab, _inactiveRoot.transform, false);

                //脱离创建容器前关闭实例自身，防止脱离时自动激活
                instance.SetActive(false);

                var player = instance.GetComponent<NiumaCharacterController>();

                if (!player.TryPrepareBaseStats(baseStats, out error))
                {
                    return false;
                }

                // 实例仍未激活，可以安全设置出生位置。
                instance.transform.SetParent(null, true);
                instance.transform.SetPositionAndRotation(position, rotation);

                // 先登记所有权，激活期间若入口关闭，Clear 也能找到实例。
                CurrentPlayer = player;
                instance.SetActive(true);

                // 生命周期回调可能关闭生成器或清理刚生成的角色。
                if (!isActiveAndEnabled ||
                    instance == null ||
                    player == null ||
                    !instance.activeInHierarchy ||
                    CurrentPlayer != player)
                {
                    error = "角色激活过程中被关闭或清理，生成未完成。";
                    return false;
                }

                // 只能检查最基本的初始化结果，
                // 不代表后续 Start、移动和动画已经全部验证通过。
                if (player.RuntimeData == null)
                {
                    error = "角色激活后未创建 RuntimeData，请检查 TPC 初始化日志。";
                    return false;
                }

                completed = true;
                error = string.Empty;
                return true;

            }
            catch (Exception ex)
            {
                error = $"角色生成异常：{ex.Message}";
                Debug.LogException(ex, this);
                return false;
            }
            finally
            {
                if (!completed)
                {
                    // 失败的实例不对外保留。
                    if (CurrentPlayer != null &&
                        CurrentPlayer.gameObject == instance)
                    {
                        CurrentPlayer = null;
                    }

                    ReleaseInstance(instance);

                    GameObject root = _inactiveRoot;
                    _inactiveRoot = null;
                    ReleaseInstance(root);
                }

                _isSpawning = false;
            }

        }

        /// <summary>
        /// 仅清理本生成器创建的角色和临时容器
        /// </summary>
        public void Clear()
        {
            NiumaCharacterController player = CurrentPlayer;
            GameObject root = _inactiveRoot;

            // 先清空记录，避免关闭对象时的回调重复处理同一实例。
            CurrentPlayer = null;
            _inactiveRoot = null;

            if (player != null)
            {
                ReleaseInstance(player.gameObject);
            }

            ReleaseInstance(root);
        }

        /// <summary>
        /// 确保存在一个全局隐藏父物体
        /// </summary>
        private void EnsureInactiveRoot()
        {
            if (_inactiveRoot != null)
            {
                return;
            }

            _inactiveRoot = new GameObject("OfflineCharacter_InactiveRoot");
            _inactiveRoot.SetActive(false);

            // 不挂到可能带有缩放的节点下，保持默认位置、旋转和缩放。
            // 放入生成器所在场景，避免多场景加载时落入其他活动场景。
            SceneManager.MoveGameObjectToScene(_inactiveRoot, gameObject.scene);
        }

        /// <summary>
        /// 释放角色实例
        /// </summary>
        private void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            // Destroy 延迟到帧末执行，先关闭以立即停止输入和更新。
            instance.SetActive(false);
            Destroy(instance);
        }

        private void OnDisable()
        {
            Clear();
        }
    }
}
