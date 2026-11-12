using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
//设置循环Timeline段落

/// <summary>
/// 时间线动画
/// </summary>
public class TimelineAnimation : MonoBehaviour
{
    public List<AnimClip> animClips = new List<AnimClip>();
    public string defaultAnim;
    private PlayableDirector mPlayableDirector;

    private AnimClip mCurAnimClip;
    void Awake()
    {
        mPlayableDirector = GetComponent<PlayableDirector>();
    }

    private void Start()
    {
        Play(defaultAnim);
    }

    void Update()
    {
        if (mCurAnimClip != null) 
        {
            //
            if (mPlayableDirector.time >= mCurAnimClip.interval.y) 
            {
                //循环
                if (mCurAnimClip.loop)
                {
                    mPlayableDirector.time = mCurAnimClip.interval.x;
                }
                else //播放完毕 
                {
                    if (mCurAnimClip.joinName != "")
                    {
                        Play(mCurAnimClip.joinName);
                    }
                    else
                    {
                        Play(defaultAnim);
                    }
                }
            }
        }
    }

    public void Play(string name) 
    {
        mCurAnimClip = GetAnimClipByName(name);

        if (mCurAnimClip != null)
        {
            mPlayableDirector.time = mCurAnimClip.interval.x;
            mPlayableDirector.Play();
        }
        else 
        {
            Debug.LogError("找不到对应动画:"+ name);
        }
    }


    private AnimClip GetAnimClipByName(string name)
    {
        for (int i = 0; i < animClips.Count; i++)
        {
            if (animClips[i].name == name)
                return animClips[i];
        }
        return null;
    }

    [System.Serializable]
    public class AnimClip
    {
        public string name;
        public Vector2 interval;
        public bool loop;
        public string joinName = "";
    }
}
