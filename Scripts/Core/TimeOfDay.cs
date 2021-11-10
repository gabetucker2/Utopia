using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeOfDay : MonoBehaviour
{
    [Header("ADJUST")]
    public Prime prime;
    private MapGenerator mapGenerator;
    public Camera primeCamera;
    public GameObject thisTimeText;
    [Range(0, 24)] public int sceneStartTime;
    [Range(0.03125f, 10f)] public float secondsPerMinute;
    [Tooltip("This must be more than UITransitionSeconds divided by two")]
    [Range(0.25f, 5f)] public float lightingTransitionSeconds, timeTextDisplaySeconds;
    public List<TimeLightingSetting> timeLightingSettings;

    [Header("DISPLAY")]
    public float sceneCurrentTime;
    public string prevTime, currentTime, nextTime;

    private TextMeshProUGUI thisTimeTextText;
    private RectTransform thisTimeTextRT;
    private bool isJustStarting = true;
    private float thisTimeTextStartY, thisTimeTextEndY;

    private void Start()
    {
        mapGenerator = prime.mapGenerator;

        thisTimeTextStartY = thisTimeText.GetComponent<RectTransform>().rect.height;
        thisTimeTextEndY = 0f;

        sceneCurrentTime = sceneStartTime;
        thisTimeTextText = thisTimeText.GetComponent<TextMeshProUGUI>();
        thisTimeTextRT = thisTimeText.GetComponent<RectTransform>();
        thisTimeTextRT.anchoredPosition = new Vector2(thisTimeTextRT.anchoredPosition.x, thisTimeTextStartY);
    }

    private void SetAllLighting(TimeLightingSetting thisSetting)
    {
        primeCamera.backgroundColor = thisSetting.skyColor;
        RenderSettings.ambientSkyColor = thisSetting.ambientSkyColor;
        RenderSettings.ambientEquatorColor = thisSetting.ambientEquatorColor;
        RenderSettings.ambientGroundColor = thisSetting.ambientGroundColor;
        RenderSettings.fogColor = thisSetting.fogColor;
        RenderSettings.fogStartDistance = thisSetting.fogStart * mapGenerator.hexSize;
        RenderSettings.fogEndDistance = thisSetting.fogEnd * mapGenerator.hexSize;
    }

    IEnumerator Transition(TimeLightingSetting currentLightingSetting, TimeLightingSetting nextLightingSetting)
    {
        thisTimeTextText.text = nextLightingSetting.settingName;

        IEnumerator TransitionUI(bool goDown)
        {
            float localStartY, localGoalY;//just define so it doesn't vary based on current position

            if (goDown) { localStartY = thisTimeTextStartY; localGoalY = thisTimeTextEndY; } else { localStartY = thisTimeTextEndY; localGoalY = thisTimeTextStartY; }

            //LOOP CLOSE
            for (float t = Time.deltaTime; t < prime.UITransitionSeconds / 2; t += Time.deltaTime)
            {
                float thisLerp = t * (1 / (prime.UITransitionSeconds / 2));
                thisTimeTextRT.anchoredPosition = Vector2.Lerp(new Vector2(0f, localStartY), new Vector2(0f, localGoalY), thisLerp);

                yield return new WaitForSeconds(Time.deltaTime);
            }

            //SOLIDIFY POSITION
            thisTimeTextRT.anchoredPosition = new Vector2(0f, localGoalY);
        }

        IEnumerator TransitionLighting()
        {
            //LOOP CLOSE
            for (float t = Time.deltaTime; t < lightingTransitionSeconds; t += Time.deltaTime)
            {
                float thisLerp = t * (1 / lightingTransitionSeconds);
                primeCamera.backgroundColor = Color.Lerp(currentLightingSetting.skyColor, nextLightingSetting.skyColor, thisLerp);
                RenderSettings.ambientSkyColor = Color.Lerp(currentLightingSetting.ambientSkyColor, nextLightingSetting.ambientSkyColor, thisLerp);
                RenderSettings.ambientEquatorColor = Color.Lerp(currentLightingSetting.ambientEquatorColor, nextLightingSetting.ambientEquatorColor, thisLerp);
                RenderSettings.ambientGroundColor = Color.Lerp(currentLightingSetting.ambientGroundColor, nextLightingSetting.ambientGroundColor, thisLerp);
                RenderSettings.fogColor = Color.Lerp(currentLightingSetting.fogColor, nextLightingSetting.fogColor, thisLerp);
                RenderSettings.fogDensity = Mathf.Lerp(currentLightingSetting.fogStart, nextLightingSetting.fogStart, thisLerp);
                RenderSettings.fogDensity = Mathf.Lerp(currentLightingSetting.fogEnd, nextLightingSetting.fogEnd, thisLerp);

                yield return new WaitForSeconds(Time.deltaTime);
            }

            //SOLIDIFY POSITION
            SetAllLighting(nextLightingSetting);
        }

        //UI IN
        StartCoroutine(TransitionUI(true));

        //FADE
        StartCoroutine(TransitionLighting());


        yield return new WaitForSeconds((prime.UITransitionSeconds / 2) + timeTextDisplaySeconds);

        //UI OUT
        StartCoroutine(TransitionUI(false));
    }

    private void Update()
    {
        //SET SCENECURRENTTIME
        sceneCurrentTime += (Time.deltaTime * (1f / (secondsPerMinute / 60f)) / 60f / 24f);
        if (sceneCurrentTime >= 24f) { sceneCurrentTime -= 24f; }

        //SET CURRENT AND NEXT LIGHTING SETTINGS
        TimeLightingSetting prevLightingSetting = timeLightingSettings[0];
        TimeLightingSetting currentLightingSetting = timeLightingSettings[0];
        TimeLightingSetting nextLightingSetting = timeLightingSettings[0];
        for (int s = 0; s < timeLightingSettings.Count; s++)
        {
            TimeLightingSetting thisSetting = timeLightingSettings[s];
            TimeLightingSetting prevSetting, nextSetting;

            if (s + 1 > timeLightingSettings.Count - 1) { prevSetting = timeLightingSettings[s - 1]; nextSetting = timeLightingSettings[0]; }
            else if (s - 1 < 0) { prevSetting = timeLightingSettings[timeLightingSettings.Count - 1]; nextSetting = timeLightingSettings[s + 1]; }
            else { prevSetting = timeLightingSettings[s - 1]; nextSetting = timeLightingSettings[s + 1]; }

            if ((thisSetting.startTime <= sceneCurrentTime && nextSetting.startTime > sceneCurrentTime && thisSetting.startTime < nextSetting.startTime)//in the middle zone
                || (thisSetting.startTime <= sceneCurrentTime && nextSetting.startTime < sceneCurrentTime && thisSetting.startTime > nextSetting.startTime)//in the end overlapping
                || (thisSetting.startTime > sceneCurrentTime && nextSetting.startTime > sceneCurrentTime && thisSetting.startTime > nextSetting.startTime))
            {//in the beginning overlapping
                prevLightingSetting = prevSetting;
                currentLightingSetting = thisSetting;
                nextLightingSetting = nextSetting;
            }
        }

        if (isJustStarting)
        {
            isJustStarting = false;

            SetAllLighting(currentLightingSetting);
        }
        else if (currentTime != currentLightingSetting.settingName)//if it's updating time settings
        {
            StartCoroutine(Transition(prevLightingSetting, currentLightingSetting));
        }

        prevTime = prevLightingSetting.settingName;
        currentTime = currentLightingSetting.settingName;
        nextTime = nextLightingSetting.settingName;
    }
}
