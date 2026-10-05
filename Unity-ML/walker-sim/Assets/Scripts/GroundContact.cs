using UnityEngine;

namespace WalkerSim
{
    /// <summary>
    /// 足先の接地状態を検知・管理するスクリプト。
    /// </summary>
    public class GroundContact : MonoBehaviour
    {
        [Header("Ground Detection")]
        [Tooltip("地面とみなすレイヤー")]
        [SerializeField] private LayerMask groundLayer;

        public bool IsGrounded { get; private set; }

        private int contactCount = 0;

        private void OnEnable()
        {
            ResetContact();
        }

        public void ResetContact()
        {
            contactCount = 0;
            IsGrounded = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsGround(collision.gameObject))
            {
                contactCount++;
                IsGrounded = true;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (IsGround(collision.gameObject))
            {
                IsGrounded = true;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (IsGround(collision.gameObject))
            {
                contactCount = Mathf.Max(0, contactCount - 1);
                if (contactCount == 0)
                {
                    IsGrounded = false;
                }
            }
        }

        private bool IsGround(GameObject obj)
        {
            if (groundLayer.value != 0)
            {
                return (groundLayer.value & (1 << obj.layer)) != 0;
            }

            int groundLayerIndex = LayerMask.NameToLayer("Ground");
            if (groundLayerIndex != -1 && obj.layer == groundLayerIndex)
            {
                return true;
            }

            string objName = obj.name.ToLower();
            return objName.Contains("ground") || objName.Contains("terrain") || objName.Contains("floor") || objName.Contains("block");
        }
    }
}
