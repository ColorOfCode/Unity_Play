using System.Collections.Generic;
using UnityEngine;

namespace SF_Studio.Shared.Modules.ProjectileSystem.Scripts {
    /// <summary>
    /// Default Projectile System that moves the projectile forward until it hits some obstacle.
    /// Spawns launch flashes and hit effects if assigned
    /// When the optional vfxLayer is injected, sets the hit effect to the given layer in order to avoid clipping
    /// * This happens only if hit position visible from player camera to avoid rendering the hit when its behind some walls
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DefaultProjectile : MonoBehaviour {
        public float maxSpeed = 15;
        public AnimationCurve speedOverTime = AnimationCurve.Constant(0, 0, 1);
        public float animationDuration = 2;

        public GameObject hitVFX;
        public GameObject flashVFX;
        public List<GameObject> trails;
        [Min(1)]
        public float maxDestroyTimeAfterHit = 2f;

        private Rigidbody _rb;
        private float _spawnTime;

        private LayerMask _vfxLayer;
        private bool _useVfxLayer;

        private Collider _collider;

        /// <summary>
        /// Configures a custom layer for VFX effects and enables VFX layer usage.
        /// Stores the layer mask and retrieves the sphere collider component for later visibility checks.
        /// </summary>
        public void SetVfxLayer(LayerMask vfxLayer) {
            _vfxLayer = vfxLayer;
            _useVfxLayer = true;
            _collider = GetComponent<Collider>();
        }

        /// <summary>
        /// Initializes the projectile on spawn.
        /// Retrieves the rigidbody component, records the spawn time, and launches the initial flash VFX.
        /// </summary>
        private void Start() {
            _rb = GetComponent<Rigidbody>();
            _spawnTime = Time.time;
            var vfx = LaunchEffect(flashVFX, transform.position, Quaternion.identity);
            if (vfx) {
                vfx.transform.forward = gameObject.transform.forward;
            }
        }

        /// <summary>
        /// Updates the projectile's velocity each physics frame based on the speed curve.
        /// Calculates elapsed time, evaluates the animation curve, and applies velocity in the forward direction.
        /// </summary>
        private void FixedUpdate() {
            var elapsed = Time.time - _spawnTime;
            var time = Mathf.Clamp01(elapsed / animationDuration);
            var curveValue = speedOverTime.Evaluate(time);
            var currentSpeed = curveValue * maxSpeed;
            _rb.velocity = transform.forward * currentSpeed;
        }

        /// <summary>
        /// Instantiates a visual effect prefab at the specified position and rotation.
        /// Optionally applies a custom layer if setCustomLayer is true and VFX layer is configured.
        /// Automatically destroys the effect after the particle system duration or maxDestroyTimeAfterHit.
        /// </summary>
        private GameObject LaunchEffect(GameObject prefab, Vector3 position, Quaternion rotation, bool setCustomLayer = false) {
            if (prefab == null) {
                return null;
            }

            var vfx = Instantiate(prefab, position, rotation);
            if (setCustomLayer) {
                SetLayerRecursively(vfx, GetLayerFromMask(_vfxLayer));
            }

            var ps = vfx.GetComponentInChildren<ParticleSystem>();
            var waitUntilDestroy = ps ? ps.main.duration : maxDestroyTimeAfterHit;
            Destroy(vfx, waitUntilDestroy);
            return vfx;
        }

        /// <summary>
        /// Handles collision events when the projectile hits something.
        /// Spawns hit VFX at the contact point with a small normal offset, checks visibility to determine layer usage,
        /// detaches and stops all trail effects, then destroys the projectile game object.
        /// </summary>
        private void OnCollisionEnter(Collision collision) {
            var contact = collision.contacts[0];
            // small offset along normal to avoid too high clipping of hit effect
            var spawnPos = contact.point + contact.normal * 0.15f;

            var isVisible = IsPointVisibleFromCamera(
                spawnPos
            );

            // only trigger vfx layer change if useVfxLayer is true
            LaunchEffect(hitVFX, spawnPos, Quaternion.FromToRotation(Vector3.up, contact.normal), _useVfxLayer && isVisible);
            if (trails.Count > 0) {
                foreach (var trail in trails) {
                    trail.transform.parent = null;
                    var ps = trail.GetComponent<ParticleSystem>();
                    if (ps != null) {
                        ps.Stop();
                    }

                    Destroy(trail, maxDestroyTimeAfterHit);
                }
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Recursively sets the layer for a game object and all of its children in the hierarchy.
        /// </summary>
        private static void SetLayerRecursively(GameObject obj, int layer) {
            obj.layer = layer;
            foreach (Transform child in obj.transform) {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Converts a LayerMask to a single layer index.
        /// Uses logarithm base 2 to extract the layer number from the mask's bit value.
        /// </summary>
        private int GetLayerFromMask(LayerMask mask) {
            return (int)Mathf.Log(mask.value, 2);
        }

        /// <summary>
        /// Determines if a point in world space is visible from the main camera and belongs to this projectile.
        /// Performs a raycast from the camera to the point and checks if it hits this projectile's collider.
        /// Returns false if no main camera exists, the raycast misses, or hits a different object.
        /// </summary>
        private bool IsPointVisibleFromCamera(Vector3 point) {
            var cam = Camera.main;
            if (cam == null) {
                return false;
            }

            var dir = point - cam.transform.position;
            var distance = dir.magnitude;
            dir.Normalize();

            if (!Physics.Raycast(
                    cam.transform.position,
                    dir,
                    out var hit,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore
                )) {
                return false;
            }

            return hit.collider == _collider;
        }
    }
}