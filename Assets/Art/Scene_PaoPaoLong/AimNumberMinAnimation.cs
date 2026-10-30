using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Plays the number bounce once for each special item that reaches its target.
public class AimNumberMinAnimation : MonoBehaviour
{
    public static event System.Action AutoClearRequested;
    public static event System.Action AutoClearCompleted;
    private const string StateName = "Ani_Node_Number_Min";

    [SerializeField] private Animator numberAnimator;
    [SerializeField] private Text numberText;

    private int currentNumber = 3;
    private int pendingArrivals;
    private Coroutine playRoutine;
    private float clipLength = 1.35f;
    private float playbackSpeed = 1f;
    private string textPrefix = string.Empty;
    private string textSuffix = string.Empty;

    private void Awake()
    {
        if (numberAnimator == null) numberAnimator = GetComponent<Animator>();
        if (numberText == null) numberText = GetComponent<Text>();

        if (numberAnimator != null)
        {
            playbackSpeed = Mathf.Max(0.01f, numberAnimator.speed);
            if (numberAnimator.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = numberAnimator.runtimeAnimatorController.animationClips;
                if (clips.Length > 0) clipLength = Mathf.Max(0.01f, clips[0].length);
            }
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (numberText != null)
        {
            string originalText = numberText.text;
            string digits = originalText.Trim();
            if (int.TryParse(digits, out int displayedNumber))
            {
                currentNumber = displayedNumber;
                int start = originalText.IndexOf(digits, System.StringComparison.Ordinal);
                textPrefix = originalText.Substring(0, start);
                textSuffix = originalText.Substring(start + digits.Length);
            }
        }

        HoldFirstFrame();
        SpecialAimDot5.ReachedDestination += OnSpecialReachedDestination;
        AutoClearCompleted += ResetForNextBoard;
    }

    private void OnDisable()
    {
        SpecialAimDot5.ReachedDestination -= OnSpecialReachedDestination;
        AutoClearCompleted -= ResetForNextBoard;
        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = null;
        pendingArrivals = 0;
    }

    private void OnSpecialReachedDestination()
    {
        if (numberAnimator == null || numberText == null) return;
        pendingArrivals++;
        if (playRoutine == null) playRoutine = StartCoroutine(PlayArrivals());
    }

    private IEnumerator PlayArrivals()
    {
        while (pendingArrivals > 0)
        {
            pendingArrivals--;
            numberAnimator.speed = playbackSpeed;
            numberAnimator.Play(StateName, 0, 0f);
            numberAnimator.Update(0f);
            int previousNumber = currentNumber;
            currentNumber = Mathf.Min(5, currentNumber + 1);
            // Keep the original whitespace: this UI Text uses trailing newlines for placement.
            numberText.text = textPrefix + currentNumber + textSuffix;
            if (previousNumber < 5 && currentNumber == 5)
                AutoClearRequested?.Invoke();

            float elapsed = 0f;
            float duration = clipLength / playbackSpeed;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            HoldFirstFrame();
        }

        playRoutine = null;
    }

    private void ResetForNextBoard()
    {
        pendingArrivals = 0;
        if (playRoutine != null) StopCoroutine(playRoutine);
        playRoutine = null;
        currentNumber = 3;
        if (numberText != null) numberText.text = textPrefix + currentNumber + textSuffix;
        HoldFirstFrame();
    }

    public static void NotifyAutoClearCompleted() => AutoClearCompleted?.Invoke();

    private void HoldFirstFrame()
    {
        if (numberAnimator == null) return;
        numberAnimator.speed = 0f;
        numberAnimator.Play(StateName, 0, 0f);
        numberAnimator.Update(0f);
    }
}
