using UnityEngine;

namespace Shuimian.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Shuimian/独立船体与可替换外观")]
    public sealed class ShuimianShipModel : MonoBehaviour
    {
        [Tooltip("浮力、尾迹、地形阻挡共用固定船体，不再按外观重新计算。")]
        public bool fixedHull = true;
        [Tooltip("相对 Ship 根节点的控制船体。更换外观时保持不变。")]
        public Bounds hullBounds = new Bounds(Vector3.zero, new Vector3(4, 3, 14));
        public Transform visualRoot;
        [Tooltip("在下方点击替换外观；外观应当只包含渲染/动画，不应包含控制船只的脚本或动态刚体。")]
        public GameObject visualPrefab;
        void OnDrawGizmosSelected()
        {
            if (!fixedHull) return;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(hullBounds.center, hullBounds.size);
        }
    }
}
