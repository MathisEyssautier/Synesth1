using FMOD.Studio;
using FMODUnity;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

public class SubtitleManager : MonoBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference voiceEventRef;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private GameObject subtitlePanel;

    private EventInstance voiceInstance;

    // Marqueurs dont le nom EST le texte à afficher.
    // "sub_end" est le seul cas spécial : il efface le texte.
    private const string END_MARKER = "sub_end";

    void Start()
    {
        subtitlePanel.SetActive(false);

        voiceInstance = RuntimeManager.CreateInstance(voiceEventRef);
        voiceInstance.setCallback(OnFMODCallback, EVENT_CALLBACK_TYPE.TIMELINE_MARKER);
        RuntimeManager.AttachInstanceToGameObject(voiceInstance, gameObject);
        StartCoroutine(StartVoiceDelayed(5f));
    }

    private IEnumerator StartVoiceDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        voiceInstance.start();
    }

    [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    private FMOD.RESULT OnFMODCallback(
        EVENT_CALLBACK_TYPE type,
        System.IntPtr instancePtr,
        System.IntPtr paramPtr)
    {
        if (type == EVENT_CALLBACK_TYPE.TIMELINE_MARKER)
        {
            var props = (TIMELINE_MARKER_PROPERTIES)
                Marshal.PtrToStructure(paramPtr, typeof(TIMELINE_MARKER_PROPERTIES));

            string markerName = props.name;

            UnityMainThreadDispatcher.Instance().Enqueue(() => ShowSubtitle(markerName));
        }
        return FMOD.RESULT.OK;
    }

    private void ShowSubtitle(string markerName)
    {
        if (markerName == END_MARKER)
        {
            subtitlePanel.SetActive(false);
            subtitleText.text = "";
        }
        else
        {
            subtitleText.text = markerName;
            subtitlePanel.SetActive(true);
        }
    }

    void OnDestroy()
    {
        voiceInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        voiceInstance.release();
    }
}