using UnityEngine;

namespace WalkerSim
{
    /// <summary>
    /// 胴体が地面に接触（転倒）したことを検知するスクリプト。
    /// </summary>
    public class BodyContact : MonoBehaviour
    {
        [Header("Ground Detection")]
        [Tooltip("地面とみなすレイヤー")]
        [SerializeField] private LayerMask groundLayer;

        public bool HasTouchedGround { get; private set; }

        public void ResetContact()
        {
            HasTouchedGround = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsGround(collision.gameObject))
            {
                HasTouchedGround = true;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (IsGround(collision.gameObject))
            {
                HasTouchedGround = true;
            }
        }

        private bool IsGround(GameObject obj)
        {
            if (groundLayer.value != 0)
            {
                return (groundLayer.value & (1 << obj.layer)) != 0;
            }

            int defaultGroundLayer = LayerMask.NameToLayer("Ground");
            if (defaultGroundLayer != -1 && obj.layer == defaultGroundLayer)
            {
                return true;
            }

            return obj.name.StartsWith("TerrainBlock");
        }
    }
}
