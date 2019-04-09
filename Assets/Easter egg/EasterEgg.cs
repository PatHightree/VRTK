using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRTK;

public class EasterEgg : MonoBehaviour
{
    public Transform Target;
    public float FadeDuration = 2;
    [Header("Visuals")] 
    [Range(0, 1)]public float EffectIntensity;
    public Shader EffectShader;
    public Material EffectParameters;

    [Header("Haptics")]
    [Range(0, 1)] public float HapticsIntensity = 1;
    [Range(0, 1)] public float OscillationResult;
    [Range(0, 1)] public float OscillateMinStrength = 0.07f;
    [Range(0, 1)] public float OscillateMaxStrength = 0.25f;
    [Range(0, 1)] public float OscillatePeriod = 0.5f;
    [Range(0, 1)] public float UpdateInterval = 0.05f;
    [Range(0.00001f, 0.1f)] public float PulseInterval = 0.01f;

    [Header("Audio")] 
    public float AudioMin = 0.05f;
    public float AudioMax = 0.15f;
    
    private bool m_active;
    private List<Renderer> m_renderers;
    private VRTK_InteractableObject m_interactableObject;
    private VRTK_ControllerReference m_controllerReference;
    private VRTK_ControllerEvents m_controllerEvents;
    private float m_lastClick;
    private int m_clickCount;
    private const int ClickThreshold = 3;
    private const float ClickMaxDuration = 0.5f;
    private const float UpsideDownThreshold = 90;
    private Coroutine m_effectRoutine;
    private AudioSource m_buzzSource;
    private static readonly int Blend = Shader.PropertyToID("_EffectBlend");
    private static readonly int NumberSteps = Shader.PropertyToID("_NumberSteps");
    private static readonly int TotalDepth = Shader.PropertyToID("_TotalDepth");
    private static readonly int NoiseSize = Shader.PropertyToID("_NoiseSize");
    private static readonly int NoiseSpeed = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int HueSize = Shader.PropertyToID("_HueSize");
    private static readonly int BaseHue = Shader.PropertyToID("_BaseHue");

    #region Unity Events
    
    private void Start()
    {
        m_interactableObject = GetComponentInChildren<VRTK_InteractableObject>();
        m_interactableObject.InteractableObjectGrabbed += Grabbed;
        m_interactableObject.InteractableObjectUngrabbed += Ungrabbed;
        
        m_renderers = Target.GetComponentsInChildren<Renderer>().ToList();
        m_renderers.ForEach(_r =>
        {
            _r.materials.ToList().ForEach(_m =>
            {
                _m.shader = EffectShader;
                _m.SetFloat(Blend, 0);
                _m.SetFloat(NumberSteps, EffectParameters.GetFloat(NumberSteps));
                _m.SetFloat(TotalDepth, EffectParameters.GetFloat(TotalDepth));
                _m.SetFloat(NoiseSize, EffectParameters.GetFloat(NoiseSize));
                _m.SetFloat(NoiseSpeed, EffectParameters.GetFloat(NoiseSpeed));
                _m.SetFloat(HueSize, EffectParameters.GetFloat(HueSize));
                _m.SetFloat(BaseHue, EffectParameters.GetFloat(BaseHue));
            });
        });

        m_buzzSource = GetComponent<AudioSource>();
        m_buzzSource.volume = 0;

        VRTK_SDKManager.instance.LoadedSetupChanged += (_sender, _args) =>
        {
            if (_sender.loadedSetup == null || !_sender.loadedSetup.isValid) return;
            GameObject controllerRight = VRTK_DeviceFinder.GetControllerRightHand(true);
            m_controllerReference = VRTK_ControllerReference.GetControllerReference(controllerRight);
            m_controllerEvents = VRTK_DeviceFinder.GetScriptAliasController(controllerRight).GetComponent<VRTK_ControllerEvents>();
            m_controllerEvents.TriggerPressed += TriggerPressed;
        };
    }

    #endregion

    #region Event Handling
    
    private void Grabbed(object _sender, InteractableObjectEventArgs _e)
    {
        // If player re-grabs while effect is active, start haptics immediately
        if (m_active);
            HapticsIntensity = 1;
    }

    private void Ungrabbed(object _sender, InteractableObjectEventArgs _e)
    {
        // If player lets go of the controller while effect is active, cancel haptics immediately
        if (!m_active)
            HapticsIntensity = 0;
    }

    private void TriggerPressed(object _sender, ControllerInteractionEventArgs _e)
    {
        if (!m_interactableObject.IsGrabbed()) return;
        
        // To activate, the controller must be held upside down (no angle restriction for deactivating)
        if (Vector3.Angle(_e.controllerReference.actual.transform.up, Vector3.up) < UpsideDownThreshold && !m_active)
        {
            Debug.LogFormat("<color=red>Angle {0}</color>", Vector3.Angle(_e.controllerReference.actual.transform.up, Vector3.up));
            return;
        }
        
        // To activate click 3 times in 1.5 seconds
        if (Time.time < m_lastClick + ClickMaxDuration)
        {
            m_clickCount++;
            Debug.LogFormat("<color=green>Clicked within time limit : {0}</color>", m_clickCount);
            if (m_clickCount == ClickThreshold - 1)
            {
                m_active = !m_active;
                Debug.LogFormat("<color=green>Clicked the threshold # active={0}</color>", m_active);
                m_controllerReference = _e.controllerReference;
                StartCoroutine(Fade());
                m_effectRoutine = StartCoroutine(Effect());
            }
        }
        else
        {
            Debug.LogFormat("<color=red>Clicked too late {0} > {1} + {2}</color>", Time.time, m_lastClick, ClickMaxDuration);
            m_clickCount = 0;
        }
        m_lastClick = Time.time;
    }

    #endregion

    #region Shader Effect

    private IEnumerator Fade()
    {
        bool startState = m_active;
        float start = Time.time;
        while (Time.time < start + FadeDuration && m_active == startState)
        {
            EffectIntensity = (Time.time - start) / FadeDuration;
            EffectIntensity = (m_active ? EffectIntensity : 1 - EffectIntensity);
            yield return 1;
        }

        if (m_active == startState)
        {
            EffectIntensity = m_active ? 1 : 0;
            if (!m_active)
                StopCoroutine(m_effectRoutine);
        }        
    }

    private void ApplyShaderEffect(float _amount)
    {
        m_renderers.ForEach(_r =>
        {
            _r.materials.ToList().ForEach(_m =>
            {
                _m.SetFloat(Blend, _amount);
            });
        });
    }

    #endregion

    #region Haptic Effect

    private IEnumerator Effect()
    {
        while (true)
        {
            // Oscillation
            float oscillation = Mathf.Sin(Time.time / OscillatePeriod / 2 * Mathf.PI) / 2.0f + 0.5f;
            OscillationResult = Mathf.Lerp(OscillateMinStrength, OscillateMaxStrength, oscillation);
            
            // Haptics
            HapticsIntensity = EffectIntensity * OscillationResult;
            if (m_interactableObject.IsGrabbed())
                VRTK_ControllerHaptics.TriggerHapticPulse(VRTK_DeviceFinder.GetControllerReferenceRightHand(), HapticsIntensity, UpdateInterval, PulseInterval);
            else
                VRTK_ControllerHaptics.CancelHapticPulse(VRTK_DeviceFinder.GetControllerReferenceRightHand());
                
            // Visuals
            ApplyShaderEffect(Mathf.Lerp(0.5f, 1, oscillation) * EffectIntensity);
            
            // Audio
            m_buzzSource.volume = Mathf.Lerp(AudioMin, AudioMax, oscillation) * EffectIntensity;
            
            yield return new WaitForSeconds(UpdateInterval + PulseInterval);
        }
    }

    #endregion
}
