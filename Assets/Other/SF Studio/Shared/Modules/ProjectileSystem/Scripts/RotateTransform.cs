using UnityEngine;

namespace SF_Studio.Shared.Modules.ProjectileSystem.Scripts {
    public class RotateTransform : MonoBehaviour {
        public Vector3 rotateSpeed = Vector3.zero;

        private void Update() {
            transform.Rotate(rotateSpeed * Time.deltaTime);
        }
    }
}