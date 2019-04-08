using System.Collections;
using UnityEngine;
using VRTK;

public class HapticEffectTweaking : MonoBehaviour
{
    [Range(0, 1)] public float Fade = 1;
    [Range(0, 1)] public float OscilateMinStrength = 0.07f;
    [Range(0, 1)] public float OscilateMaxStrength = 0.25f;
    [Range(0, 1)] public float OscilatePeriod = 0.5f;
    [Range(0, 1)] public float OscilateStrengthOut;
    
    [Range(0, 1)] public float UpdateInterval = 0.1f;
    [Range(0.00001f, 0.1f)] public float PulseInterval = 0.01f;
    
    private void Start()
    {
        StartCoroutine(DoHaptics());
    }

    private IEnumerator DoHaptics()
    {
        while (true)
        {
            float strength = Mathf.Sin(Time.time / OscilatePeriod / 2 * Mathf.PI);
            strength = Fade * Mathf.Lerp(OscilateMinStrength, OscilateMaxStrength, strength / 2.0f + 0.5f);
            OscilateStrengthOut = strength;
            VRTK_ControllerHaptics.TriggerHapticPulse(VRTK_DeviceFinder.GetControllerReferenceRightHand(), strength, UpdateInterval, PulseInterval);
            yield return new WaitForSeconds(UpdateInterval);
        }
    }
}
