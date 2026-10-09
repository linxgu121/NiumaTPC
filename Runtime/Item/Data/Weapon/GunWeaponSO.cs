
using UnityEngine;

namespace NiumaTPC.Item
{
    [CreateAssetMenu(fileName = "NewGunWeaponSO", menuName = "NiumaTPC/Items/Weapons/Gun")]
    public class GunWeaponSO : RangedWeaponSO
    {
        [Header("枪械武器专属动画参数")]
        [Tooltip("拿出动画允许退出时间")] 
        public float EquipEndTime = 0.5f;
        [Tooltip("开启IK的时间点（秒），相对于拿出动画开始")] 
        public float EnableIKTime = 0.4f;
        [Tooltip("关闭IK的时间点（秒），相对于收起动画开始")] 
        public float DisableIKTime = 0.4f;

        [Tooltip("射击时播放的音效 (会在射击位置播放)")]
        public AudioClip ShootSound;

        [Header("武器特效")]
        [Tooltip("首次确认射击时从对象池取得并挂到枪口，当前装备期间复用；留空不播放枪焰")]
        public GameObject MuzzleVFXPrefab;

        [Header("Recoil (后坐力)")]
        [Tooltip("后坐力的俯仰角度 (向上看的角度，单位：度)")]
        public float RecoilPitchAngle = 2f;

        [Tooltip("后坐力的偏航角度 (左右晃动，单位：度)")]
        public float RecoilYawAngle = 1f;

        [Header("Recoil Randomness (后坐力随机性)")]
        [Tooltip("俯仰随机范围（度）。实际俯仰 = RecoilPitchAngle + Random.Range(-RecoilPitchRandomRange, RecoilPitchRandomRange)")]
        public float RecoilPitchRandomRange = 0.5f;

        [Tooltip("偏航随机范围（度）。实际偏航 = RecoilYawAngle + Random.Range(-RecoilYawRandomRange, RecoilYawRandomRange)")]
        public float RecoilYawRandomRange = 0.5f;
    }
}
