using System.Collections;
using Spine.Unity;
using UnityEngine;

// Plays AimNPC's one-shot reaction when AimDot5 starts its activation animation.
public class AimNPCAnimation : MonoBehaviour
{
    private const string IdleAnimation = "Panda_Big_Ain1";
    private const string ReactionAnimation = "Panda_Big_Ain2";

    [SerializeField] private GameObject reactionEffectPrefab;
    private SkeletonAnimation skeletonAnimation;

    private void Awake()
    {
        skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        SpecialAimDot5.ActivationStarted += PlayReaction;
        PlayIdle();
    }

    private void OnDisable()
    {
        SpecialAimDot5.ActivationStarted -= PlayReaction;
    }

    private void PlayIdle()
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        if (skeletonAnimation == null) return;

        skeletonAnimation.Initialize(false);
        if (skeletonAnimation.AnimationState != null)
            skeletonAnimation.AnimationState.SetAnimation(0, IdleAnimation, true);
    }

    private void PlayReaction()
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        if (skeletonAnimation == null) return;

        skeletonAnimation.Initialize(false);
        if (skeletonAnimation.AnimationState == null) return;

        skeletonAnimation.AnimationState.SetAnimation(0, ReactionAnimation, false);
        PlayReactionEffect();
        skeletonAnimation.AnimationState.AddAnimation(0, IdleAnimation, true, 0f);
    }

    private void PlayReactionEffect()
    {
        if (reactionEffectPrefab == null) return;

        // Keep the prefab's local offset and rotation relative to AimNPC.
        GameObject effect = Instantiate(reactionEffectPrefab, transform, false);
        StartCoroutine(DestroyWhenParticlesFinish(effect));
    }

    private IEnumerator DestroyWhenParticlesFinish(GameObject effect)
    {
        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        yield return null;
        while (effect != null)
        {
            bool alive = false;
            foreach (ParticleSystem particle in particles)
            {
                if (particle != null && particle.IsAlive(true))
                {
                    alive = true;
                    break;
                }
            }

            if (!alive) break;
            yield return null;
        }

        if (effect != null) Destroy(effect);
    }
}
