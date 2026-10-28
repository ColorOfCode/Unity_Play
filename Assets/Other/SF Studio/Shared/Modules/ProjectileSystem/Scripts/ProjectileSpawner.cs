using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SF_Studio.Shared.Modules.ProjectileSystem.Scripts {
    /// <summary>
    /// Simple projectile spawning system that enables you to spawn a projectile from a given list within the target direction.
    /// Additional features:
    /// * Enables switching between the projectiles (customizable keys for next or previous projectile)
    /// * Optionally rotates a given gun to face the target
    /// * Optionally sets a vfx layer to the overlay mask and injects it to the projectile
    /// * Supports mouse position targeting mode for aiming projectiles at cursor location
    /// </summary>
    public class ProjectileSpawner : MonoBehaviour {
        public Transform firePoint;
        public Transform defaultTarget;
        public float fireRatePerSecond = 2f;

        [Header("Targeting Mode")]
        [Tooltip("If enabled, projectiles will aim at mouse position instead of default target")]
        public bool useMouseTargeting = false;
        
        [Tooltip("Layers that can be targeted by mouse raycast")]
        public LayerMask mouseTargetLayers = -1;

        [Header("Effect Control")]
        public TextMeshProUGUI effectName;

        public KeyCode nextKey = KeyCode.Alpha2;
        public KeyCode previousKey = KeyCode.Alpha1;

        [Header("Optional gun object")]
        public GameObject gun;

        [Header("Optional VFX Overlay Camera Settings")]
        [Tooltip(
            "Enables overlay rendering of the VFX layer. Note: VFX will then be always rendered. this means you see them even if they are behind a wall")]
        public bool enableVfxLayerOverlay;

        [Tooltip("The layer used for VFX rendering")]
        public LayerMask vfxLayer;

        [Tooltip("The overlay camera that renders the VFX layer")]
        public Camera overlayCam;

        public List<GameObject> projectiles = new();

        private float _timeToFire;
        private int _index;
        private int _maxIndex;
        private GameObject _currentVariant;

        private void Start() {
            if (gun) {
                FaceTarget(gun);
            }

            _maxIndex = projectiles.Count - 1;
            _currentVariant = projectiles[_index];
            if (effectName != null) {
                effectName.text = _currentVariant.name;
            }

            SetupOverlayCameraCullingMask();
        }

        private void Update() {
            if (Input.GetKeyDown(nextKey)) {
                ChangeVariant(true);
            }

            if (Input.GetKeyDown(previousKey)) {
                ChangeVariant(false);
            }

            if (fireRatePerSecond <= 0f) {
                Debug.LogError("FireRate per second needs to be greater than 0!");
            }

            // Update gun rotation to follow mouse or target
            if (gun) {
                if (useMouseTargeting) {
                    FaceMousePosition(gun);
                } else {
                    FaceTarget(gun);
                }
            }

            if (!Input.GetMouseButton(0) || !(Time.time >= _timeToFire)) {
                return;
            }

            _timeToFire = Time.time + (1f / fireRatePerSecond);
            SpawnProjectile();
        }

        /// <summary>
        /// Spawns the projectile and sets the proper projectile direction.
        /// If mouse targeting is enabled, orients projectile toward mouse position.
        /// If enabled, injects the vfxLayer to the defaultProjectile, so that the hit effect can be set to another layer (fixing clipping issues)
        /// </summary>
        private void SpawnProjectile() {
            var projectile = Instantiate(_currentVariant, firePoint.transform.position, Quaternion.identity);
            
            if (useMouseTargeting) {
                FaceMousePosition(projectile);
            } else {
                FaceTarget(projectile);
            }

            if (!enableVfxLayerOverlay) {
                return;
            }

            var defaultProjectile = projectile.GetComponent<DefaultProjectile>();
            defaultProjectile?.SetVfxLayer(vfxLayer);
        }

        /// <summary>
        /// Rotates the given game object to face the mouse position in world space.
        /// Performs a raycast from the camera through the mouse position to find the target point.
        /// </summary>
        private void FaceMousePosition(GameObject obj) {
            var cam = Camera.main;
            if (cam == null) {
                Debug.LogWarning("No main camera found for mouse targeting!");
                return;
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            Vector3 targetPoint;

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, mouseTargetLayers, QueryTriggerInteraction.Ignore)) {
                targetPoint = hit.point;
            } else {
                // Fallback: use a point far along the ray
                targetPoint = ray.origin + ray.direction * 100f;
            }

            var direction = targetPoint - obj.transform.position;
            if (direction.sqrMagnitude < 0.001f) {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction);
            obj.transform.rotation = targetRotation;
        }

        /// <summary>
        /// Rotates the projectile to face the default target
        /// </summary>
        private void FaceTarget(GameObject projectile) {
            if (defaultTarget == null) {
                return;
            }

            var direction = defaultTarget.position - projectile.transform.position;
            if (direction.sqrMagnitude < 0.001f) {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction);
            projectile.transform.rotation = targetRotation;
        }

        private void ChangeVariant(bool up) {
            _currentVariant = projectiles[GetUpdatedIndex(up)];
            if (effectName != null) {
                effectName.text = _currentVariant.name;
            }
        }

        private int GetUpdatedIndex(bool up) {
            if (up) {
                _index++;
                if (_index > _maxIndex) {
                    _index = 0;
                }
            } else {
                _index--;
                if (_index < 0) {
                    _index = _maxIndex;
                }
            }

            return _index;
        }

        private void SetupOverlayCameraCullingMask() {
            if (overlayCam == null) {
                Debug.LogWarning("Overlay camera is not assigned!");
                return;
            }

            overlayCam.cullingMask = vfxLayer.value;
            if (Camera.main != null) {
                Camera.main.cullingMask = ~vfxLayer.value;
            }
        }
    }
}