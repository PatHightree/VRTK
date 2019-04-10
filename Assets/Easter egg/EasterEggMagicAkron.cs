using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRTK;

public class EasterEggMagicAkron : EasterEgg
{
    public float FadeDuration = 2;
    [Header("Visuals")] 
    [Range(0, 1)]public float FadeAmount;
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
    public float HumMinVolume = 0.05f;
    public float HumMaxVolume = 0.15f;
    public AudioClip Hum;
    public float RushMinVolume = 0.05f;
    public float RushMaxVolume = 0.15f;
    public AudioClip Rush;
    
    private List<Renderer> m_renderers;
    private VRTK_InteractableObject m_interactableObject;
    private VRTK_ControllerEvents m_controllerEvents;
    private float m_lastClick;
    private int m_clickCount;
    private const int ClickThreshold = 3;
    private const float ClickMaxDuration = 0.5f;
    private const float UpsideDownThreshold = 90;
    private Coroutine m_effectRoutine;
    private AudioSource m_humSource;
    private AudioSource m_rushSource;
    private ParticleSystem m_confetti;
    private float m_triggerPressure;
    private static readonly int EffectBlend = Shader.PropertyToID("_EffectBlend");
    private static readonly int Emission = Shader.PropertyToID("_Emission");
    private static readonly int NumberSteps = Shader.PropertyToID("_NumberSteps");
    private static readonly int TotalDepth = Shader.PropertyToID("_TotalDepth");
    private static readonly int NoiseSize = Shader.PropertyToID("_NoiseSize");
    private static readonly int NoiseSpeed = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int HueSize = Shader.PropertyToID("_HueSize");
    private static readonly int BaseHue = Shader.PropertyToID("_BaseHue");

    
    private void Awake()
    {
        Create(transform, 
            new List<Type> {typeof(VRTK_InteractableObject), typeof(AudioSource)}, 
            new List<Type> { typeof(ParticleSystem) });
    }

    private void Start()
    {
        Setup();
    }
    
    protected override void DoSetup()
    {
        m_interactableObject = Target.GetComponentInChildren<VRTK_InteractableObject>();
        m_interactableObject.InteractableObjectGrabbed += Grabbed;
        m_interactableObject.InteractableObjectUngrabbed += Ungrabbed;
        
        m_confetti = Target.GetComponentInChildren<ParticleSystem>();

        m_renderers = Target.GetComponentsInChildren<Renderer>().ToList();
        m_renderers.ForEach(_r =>
        {
            if (_r.gameObject == m_confetti.gameObject) return;
            
            _r.materials.ToList().ForEach(_m =>
            {
                _m.shader = EffectShader;
                _m.SetFloat(EffectBlend, 0);
                _m.SetFloat(NumberSteps, EffectParameters.GetFloat(NumberSteps));
                _m.SetFloat(TotalDepth, EffectParameters.GetFloat(TotalDepth));
                _m.SetFloat(NoiseSize, EffectParameters.GetFloat(NoiseSize));
                _m.SetFloat(NoiseSpeed, EffectParameters.GetFloat(NoiseSpeed));
                _m.SetFloat(HueSize, EffectParameters.GetFloat(HueSize));
                _m.SetFloat(BaseHue, EffectParameters.GetFloat(BaseHue));
            });
        });

        m_humSource = Target.gameObject.AddComponent<AudioSource>();
        m_humSource.clip = Hum;
        m_humSource.volume = 0;
        m_humSource.loop = true;
        m_humSource.spatialBlend = 1;
        m_humSource.Play();
        m_rushSource = Target.gameObject.AddComponent<AudioSource>();
        m_rushSource.clip = Rush;
        m_rushSource.volume = 0;
        m_rushSource.loop = true;
        m_rushSource.spatialBlend = 1;
        m_rushSource.Play();

        VRTK_SDKManager.instance.LoadedSetupChanged += (_sender, _args) =>
        {
            if (_sender.loadedSetup == null || !_sender.loadedSetup.isValid) return;
            GameObject controllerRight = VRTK_DeviceFinder.GetControllerRightHand(true);
            m_controllerEvents = VRTK_DeviceFinder.GetScriptAliasController(controllerRight).GetComponent<VRTK_ControllerEvents>();
            m_controllerEvents.TriggerPressed += TriggerPressed;
            m_controllerEvents.TriggerReleased += TriggerReleased;
            m_controllerEvents.TriggerAxisChanged += TriggerAxisChanged; 
        };
    }

    protected override bool DoCheckActivationTrigger(object _sender)
    {
        if (!(_sender is VRTK_ControllerReference controllerReference)) return false;
        if (!m_interactableObject.IsGrabbed()) return false;
        
        // To (de)activate, the controller must be held upside down
        if (Vector3.Angle(controllerReference.actual.transform.up, Vector3.up) < UpsideDownThreshold)
        {
            Debug.LogFormat("<color=red>Angle {0}</color>", Vector3.Angle(controllerReference.actual.transform.up, Vector3.up));
            return false;
        }
        
        // To (de)activate click 3 times in 1.5 seconds
        if (Time.time < m_lastClick + ClickMaxDuration)
        {
            m_clickCount++;
            Debug.LogFormat("<color=green>Clicked within time limit : {0}</color>", m_clickCount);
            if (m_clickCount == ClickThreshold - 1)
            {
                Debug.LogFormat("<color=green>Clicked the threshold # : Activating</color>");
                
                return true;
            }
        }
        else
        {
            Debug.LogFormat("<color=red>Clicked too late {0} > {1} + {2}</color>", Time.time, m_lastClick, ClickMaxDuration);
            m_clickCount = 0;
        }
        
        m_lastClick = Time.time;
        return false;
    }

    protected override bool DoCheckDeactivationTrigger(object _sender)
    {
        return DoCheckActivationTrigger(_sender);
    }

    protected override void DoActivate()
    {
        StartCoroutine(FadeIn());
    }

    protected override void DoDeactivate()
    {
        StartCoroutine(FadeOut());
    }

    protected override void DoTeardown()
    {
        throw new System.NotImplementedException();
    }

    #region Event Handling
    
    private void Grabbed(object _sender, InteractableObjectEventArgs _e)
    {
        // If player re-grabs while effect is active, start haptics immediately
        if (State == EasterEggState.Active)
            HapticsIntensity = 1;
    }

    private void Ungrabbed(object _sender, InteractableObjectEventArgs _e)
    {
        // If player lets go of the controller while effect is active, cancel haptics immediately
        if (State == EasterEggState.Active)
            HapticsIntensity = 0;
    }

    private void TriggerPressed(object _sender, ControllerInteractionEventArgs _e)
    {
        CheckActivationTrigger(_e.controllerReference);
        CheckDeactivationTrigger(_e.controllerReference);
        
        if (State == EasterEggState.Active)
            m_confetti.Play();
    }

    private void TriggerAxisChanged(object _sender, ControllerInteractionEventArgs _e)
    {
        m_triggerPressure = _e.buttonPressure;
    }

    private void TriggerReleased(object _sender, ControllerInteractionEventArgs _e)
    {
        m_confetti.Stop();
    }

    #endregion

    #region Shader Effect

    private IEnumerator FadeIn()
    {
        // Start effect coroutine
        FadeAmount = 0;
        m_effectRoutine = StartCoroutine(Effect());
        
        // Animate FadeAmount
        float start = Time.time;
        while (Time.time < start + FadeDuration)
        {
            FadeAmount = (Time.time - start) / FadeDuration;
            yield return 1;
        }
        FadeAmount = 1;
        
        // Signal that activation is complete
        DoActivateFinished();
    }

    private IEnumerator FadeOut()
    {
        // Animate FadeAmount
        float start = Time.time;
        while (Time.time < start + FadeDuration)
        {
            FadeAmount = 1 - ((Time.time - start) / FadeDuration);
            yield return 1;
        }
        FadeAmount = 0;
        yield return 1;
        
        // Stop effect coroutine
        StopCoroutine(m_effectRoutine);
        
        // Signal that deactivation is complete
        DoDeactivateFinished();
    }

    private void ApplyShaderEffect(float _amount)
    {
        m_renderers.ForEach(_r =>
        {
            _r.materials.ToList().ForEach(_m =>
            {
                _m.SetFloat(EffectBlend, _amount);
                _m.SetFloat(Emission, m_triggerPressure * 0.5f);
            });
        });
    }

    #endregion

    private IEnumerator Effect()
    {
        while (true)
        {
            // Oscillation
            float oscillation = Mathf.Sin(Time.time / OscillatePeriod / 2 * Mathf.PI) / 2.0f + 0.5f;
            OscillationResult = Mathf.Lerp(OscillateMinStrength, OscillateMaxStrength, oscillation);
            float effectStrength = Mathf.Max(oscillation, m_triggerPressure); 
            
            // Haptics
            HapticsIntensity = FadeAmount * effectStrength;
            if (m_interactableObject.IsGrabbed())
                VRTK_ControllerHaptics.TriggerHapticPulse(VRTK_DeviceFinder.GetControllerReferenceRightHand(), HapticsIntensity, UpdateInterval, PulseInterval);
            else
                VRTK_ControllerHaptics.CancelHapticPulse(VRTK_DeviceFinder.GetControllerReferenceRightHand());
                
            // Visuals
            ApplyShaderEffect(Mathf.Lerp(0.5f, 1, effectStrength) * FadeAmount);
            
            // Audio
            m_humSource.volume = Mathf.Lerp(HumMinVolume, HumMaxVolume, effectStrength) * FadeAmount;

//            float audioMaxBoost = Mathf.Lerp(HumMaxVolume, 2, m_triggerPressure);
            m_rushSource.volume = Mathf.Lerp(RushMinVolume, RushMaxVolume, m_triggerPressure) * FadeAmount;

            // Confetti speed
            ParticleSystem.MainModule main = m_confetti.main;
            main.startSpeedMultiplier = m_triggerPressure * 3;

            yield return new WaitForSeconds(UpdateInterval + PulseInterval);
        }
    }

}
