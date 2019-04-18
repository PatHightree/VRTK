using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using VRTK;
using VRTK.GrabAttachMechanics;
using VRTK.SecondaryControllerGrabActions;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

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
    public AudioClip Rush;

    private ParticleSystem m_originalParticleSystem;
    private List<Renderer> m_renderers;
    private Dictionary<Material, Shader> m_prevShaders;
    private List<Component> m_components;
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
    private float m_rushVolume;
    private float m_rushVolumeSpeed;
    private static readonly int EffectBlend = Shader.PropertyToID("_EffectBlend");
    private static readonly int Emission = Shader.PropertyToID("_Emission");
    private static readonly int NumberSteps = Shader.PropertyToID("_NumberSteps");
    private static readonly int TotalDepth = Shader.PropertyToID("_TotalDepth");
    private static readonly int NoiseSize = Shader.PropertyToID("_NoiseSize");
    private static readonly int NoiseSpeed = Shader.PropertyToID("_NoiseSpeed");
    private static readonly int HueSize = Shader.PropertyToID("_HueSize");
    private static readonly int BaseHue = Shader.PropertyToID("_BaseHue");

    #region Overrides

    protected override void GetPrerequisites(out List<Type> _targetPrerequisites, out List<Type> _childPrerequisites)
    {
        _targetPrerequisites = new List<Type>();
        _childPrerequisites = new List<Type> { typeof(ParticleSystem) };
    }

    protected override void DoSetup(Action _setupFinished)
    {
        // Switch off original particle system
        m_originalParticleSystem = Target.GetComponentInChildren<ParticleSystem>();
        m_originalParticleSystem.gameObject.SetActive(false);

        // Prepare egg components
        m_confetti = gameObject.GetComponentInChildren<ParticleSystem>();
        m_humSource = gameObject.AddComponent<AudioSource>();
        m_humSource.clip = Hum;
        m_humSource.volume = 0;
        m_humSource.loop = true;
        m_humSource.spatialBlend = 1;
        m_humSource.Play();
        m_rushSource = gameObject.AddComponent<AudioSource>();
        m_rushSource.clip = Rush;
        m_rushSource.volume = m_rushVolume = 0;
        m_rushSource.loop = true;
        m_rushSource.spatialBlend = 1;
        m_rushSource.Play();

        // Prepare components in Target
        m_prevShaders = new Dictionary<Material, Shader>();
        m_renderers = Target.GetComponentsInChildren<Renderer>().ToList();
        m_renderers.ForEach(_r =>
        {
            _r.materials.ToList().ForEach(_m =>
            {
                // Don't change the confetti particles' material
                if (_r.gameObject == m_confetti.gameObject) return;
                // Store original shaders before changing them
                if (!m_prevShaders.ContainsKey(_m))
                    m_prevShaders.Add(_m, _m.shader);
                // Change shaders to effect shader and set their params
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

        // Install VRTK callbacks
        m_interactableObject = Target.GetComponent<VRTK_InteractableObject>();
        m_interactableObject.InteractableObjectGrabbed += Grabbed;
        m_interactableObject.InteractableObjectUngrabbed += Ungrabbed;
        VRTK_SDKManager.instance.LoadedSetupChanged += (_sender, _args) =>
        {
            if (_sender.loadedSetup == null || !_sender.loadedSetup.isValid) return;
            GameObject controllerRight = VRTK_DeviceFinder.GetControllerRightHand(true);
            m_controllerEvents = VRTK_DeviceFinder.GetScriptAliasController(controllerRight).GetComponent<VRTK_ControllerEvents>();
            if (VRTK_DeviceFinder.GetHeadsetType() == SDK_BaseHeadset.HeadsetType.HTCVive)
            {
                // For Vive Wands, clicking works better
                m_controllerEvents.TriggerClicked += TriggerClicked;
                m_controllerEvents.TriggerUnclicked += TriggerUnclicked;
            }
            else
            {
                // For Oculus Touch, pressing passed 50% works better
                m_controllerEvents.TriggerPressed += TriggerPressed;
                m_controllerEvents.TriggerReleased += TriggerReleased;
            }
            m_controllerEvents.TriggerAxisChanged += TriggerAxisChanged; 
        };
        
        _setupFinished?.Invoke();
    }

    protected override bool DoCheckActivationTrigger(object _sender)
    {
        if (!(_sender is VRTK_ControllerReference controllerReference)) return false;
        if (!m_interactableObject.IsGrabbed()) return false;
        
        // To (de)activate, the controller must be held upside down
        Vector3 modelUp = 
            VRTK_DeviceFinder.GetHeadsetType() == SDK_BaseHeadset.HeadsetType.HTCVive 
            ? controllerReference.actual.transform.up
            : controllerReference.actual.transform.TransformPoint(new Vector3(0, 1, 1));
        Debug.DrawLine(controllerReference.actual.transform.position, modelUp, Color.green);
        if (Vector3.Angle(modelUp, Vector3.up) < UpsideDownThreshold)
        {
            if (DebugLog) Debug.LogFormat("<color=red>Angle {0}</color>", Vector3.Angle(modelUp, Vector3.up));
            return false;
        }
        
        // To (de)activate click 3 times in 1.5 seconds
        if (Time.time < m_lastClick + ClickMaxDuration)
        {
            m_clickCount++;
            if (DebugLog) Debug.LogFormat("<color=green>Clicked within time limit : {0}</color>", m_clickCount);
            if (m_clickCount == ClickThreshold - 1)
            {
                if (DebugLog) Debug.LogFormat("<color=green>Clicked the threshold # : Activating</color>");
                
                return true;
            }
        }
        else
        {
            if (DebugLog) Debug.LogFormat("<color=red>Clicked too late {0} > {1} + {2}</color>", Time.time, m_lastClick, ClickMaxDuration);
            m_clickCount = 0;
        }
        
        m_lastClick = Time.time;
        return false;
    }

    protected override bool DoCheckDeactivationTrigger(object _sender)
    {
        return DoCheckActivationTrigger(_sender);
    }

    protected override void DoActivate(Action _activateFinished)
    {
        StartCoroutine(FadeIn(_activateFinished));
    }

    protected override void DoDeactivate(Action _deactivateFinished)
    {
        StartCoroutine(FadeOut(_deactivateFinished));
    }

    protected override void DoTeardown(Action _teardownFinished)
    {
        // Re-enable original particle system
        m_originalParticleSystem.gameObject.SetActive(true);
        
        // Remove added sound sources
        Destroy(m_humSource);
        Destroy(m_rushSource);
        
        // Detach VRTK callbacks 
        m_interactableObject.InteractableObjectGrabbed -= Grabbed;
        m_interactableObject.InteractableObjectUngrabbed -= Ungrabbed;
        if (VRTK_DeviceFinder.GetHeadsetType() == SDK_BaseHeadset.HeadsetType.HTCVive)
        {
            m_controllerEvents.TriggerClicked -= TriggerClicked;
            m_controllerEvents.TriggerUnclicked -= TriggerUnclicked;
        }
        else
        {
            m_controllerEvents.TriggerPressed -= TriggerPressed;
            m_controllerEvents.TriggerReleased -= TriggerReleased;
        }
        m_controllerEvents.TriggerAxisChanged -= TriggerAxisChanged; 

        // Restore original shaders
        foreach (var prevShader in m_prevShaders)
            prevShader.Key.shader = prevShader.Value;

        _teardownFinished?.Invoke();
    }

    #endregion

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

    private void TriggerClicked(object _sender, ControllerInteractionEventArgs _e)
    {
        TriggerPressed(_sender, _e);
    }

    private void TriggerUnclicked(object _sender, ControllerInteractionEventArgs _e)
    {
        TriggerReleased(_sender, _e);
    }

    #endregion

    #region Shader Effect

    private IEnumerator FadeIn(Action _activateFinished)
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
        _activateFinished?.Invoke();
    }

    private IEnumerator FadeOut(Action _deactivateFinished)
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
        _deactivateFinished?.Invoke();
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
            ApplyShaderEffect(Mathf.Lerp(0.25f, 1, effectStrength) * FadeAmount);
            
            // Audio
            m_humSource.volume = Mathf.Lerp(HumMinVolume, HumMaxVolume, effectStrength) * FadeAmount;
            
            const float triggerTolerance = 0.1f;
            m_rushVolume = Mathf.SmoothDamp(m_rushVolume, m_triggerPressure, ref m_rushVolumeSpeed, 0.35f); // Substract triggerTolerance because trigger is not exactly 0
            m_rushSource.volume = (Mathf.Max(m_triggerPressure, m_rushVolume) - triggerTolerance) * FadeAmount; // Max lets the starting of sound be immediate and stopping be delayed
            
            // Confetti speed
            ParticleSystem.MainModule main = m_confetti.main;
            main.startSpeedMultiplier = m_triggerPressure * 3;

            yield return new WaitForSeconds(UpdateInterval + PulseInterval);
        }
    }

    #endregion
}
