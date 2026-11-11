using UnityEngine;

namespace ObscuredByClouds.Gameplay
{
    /// <summary>One character field shared by every compatible plant material.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ObscuredByClouds/Terrain Grass Interaction")]
    public sealed class TerrainGrassInteraction : MonoBehaviour
    {
        [Header("Interactor")]
        public Transform character;

        [Header("Bending and recovery")]
        [Min(0.1f)] public float radius = 1.25f;
        [Range(0f, 80f)] public float bendAngle = 58f;
        [Min(0.05f)] public float recoveryTime = 0.7f;
        [Min(0.1f)] public float verticalTolerance = 2f;
        [Min(0.02f)] public float trailInterval = 0.09f;

        const int TrailCapacity = 8;
        readonly Vector4[] points = new Vector4[TrailCapacity + 1];
        readonly Vector4[] directions = new Vector4[TrailCapacity + 1];
        readonly Vector3[] trailPositions = new Vector3[TrailCapacity];
        readonly Vector2[] trailDirections = new Vector2[TrailCapacity];
        readonly float[] trailTimes = new float[TrailCapacity];
        Vector3 previousPosition;
        Vector2 lastDirection = Vector2.up;
        int trailHead, trailCount;
        float lastTrailTime;
        bool ready;
        static readonly int CountId = Shader.PropertyToID("_OBCGrassPointCount");
        static readonly int PointsId = Shader.PropertyToID("_OBCGrassPoints");
        static readonly int DirectionsId = Shader.PropertyToID("_OBCGrassDirections");
        static readonly int BendId = Shader.PropertyToID("_OBCGrassBend");

        void OnEnable()
        {
            if (Application.isPlaying) InitializeInteraction();
        }

        internal void InitializeInteraction()
        {
            if (!character)
            {
                Debug.LogError("TerrainGrassInteraction: assign the character transform.", this);
                enabled = false;
                return;
            }
            previousPosition = character.position;
            lastDirection = Vector2.up;
            trailHead = trailCount = 0;
            lastTrailTime = Time.time;
            ready = true;
            Publish();
        }

        void LateUpdate()
        {
            if (!ready) return;
            if (!character) { Shader.SetGlobalInt(CountId, 0); return; }
            Vector3 movement = character.position - previousPosition;
            Vector2 horizontal = new Vector2(movement.x, movement.z);
            if (horizontal.sqrMagnitude > radius * radius * 16f)
            {
                trailCount = trailHead = 0;
                lastTrailTime = Time.time;
            }
            else if (horizontal.sqrMagnitude > 0.000001f)
            {
                lastDirection = horizontal.normalized;
                if (Time.time - lastTrailTime >= trailInterval)
                {
                    trailPositions[trailHead] = previousPosition;
                    trailDirections[trailHead] = lastDirection;
                    trailTimes[trailHead] = Time.time;
                    trailHead = (trailHead + 1) % TrailCapacity;
                    trailCount = Mathf.Min(trailCount + 1, TrailCapacity);
                    lastTrailTime = Time.time;
                }
            }
            previousPosition = character.position;
            Publish();
        }

        void Publish()
        {
            int count = 0;
            if (character.gameObject.activeInHierarchy)
            {
                points[count] = new Vector4(character.position.x, character.position.y, character.position.z, Mathf.Max(0.1f, radius));
                directions[count++] = new Vector4(lastDirection.x, lastDirection.y, 1f, 0);
            }
            for (int i = 0; i < trailCount; i++)
            {
                float age = (Time.time - trailTimes[i]) / Mathf.Max(0.05f, recoveryTime);
                if (age >= 1f) continue;
                float fade = 1f - Mathf.SmoothStep(0f, 1f, age);
                Vector3 p = trailPositions[i];
                points[count] = new Vector4(p.x, p.y, p.z, Mathf.Max(0.1f, radius));
                Vector2 d = trailDirections[i];
                directions[count++] = new Vector4(d.x, d.y, fade, 0);
            }
            Shader.SetGlobalVectorArray(PointsId, points);
            Shader.SetGlobalVectorArray(DirectionsId, directions);
            Shader.SetGlobalVector(BendId, new Vector4(bendAngle * Mathf.Deg2Rad, Mathf.Max(0.1f, verticalTolerance), 0, 0));
            Shader.SetGlobalInt(CountId, count);
        }

        void OnDisable()
        {
            ready = false;
            trailHead = trailCount = 0;
            Shader.SetGlobalInt(CountId, 0);
        }

        void OnDrawGizmosSelected()
        {
            if (!character) return;
            Gizmos.color = new Color(0.25f, 0.95f, 0.4f, 0.7f);
            Gizmos.DrawWireSphere(character.position, radius);
        }
    }
}
