using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRTK;

public class EasterEgg : MonoBehaviour
{
    public Transform Target;
    public Shader EffectBlend;
    public Material EffectParameters;
    public float EffectMax = 1;
    public float BlendDuration = 2;
    public AudioClip HapticClip;

    private bool m_active;
    private List<Renderer> m_renderers;
    private VRTK_InteractableObject m_interactableObject;
    private VRTK_ControllerReference m_controllerReference;
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
        m_interactableObject.InteractableObjectGrabbed += TurnOn;
        m_interactableObject.InteractableObjectUngrabbed += TurnOff;

        m_controllerReference = VRTK_ControllerReference.GetControllerReference(VRTK_DeviceFinder.GetControllerRightHand(true));
        
        m_renderers = Target.GetComponentsInChildren<Renderer>().ToList();
        m_renderers.ForEach(_r =>
        {
            _r.materials.ToList().ForEach(_m =>
            {
                _m.shader = EffectBlend;
                _m.SetFloat(Blend, 0);
                _m.SetFloat(NumberSteps, EffectParameters.GetFloat(NumberSteps));
                _m.SetFloat(TotalDepth, EffectParameters.GetFloat(TotalDepth));
                _m.SetFloat(NoiseSize, EffectParameters.GetFloat(NoiseSize));
                _m.SetFloat(NoiseSpeed, EffectParameters.GetFloat(NoiseSpeed));
                _m.SetFloat(HueSize, EffectParameters.GetFloat(HueSize));
                _m.SetFloat(BaseHue, EffectParameters.GetFloat(BaseHue));
            });
        });
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            m_active = !m_active;
            StartCoroutine(FadeEffect());
            
            if (m_active)
                StartCoroutine(LoopHapticEffect());
            else
                CancelHapticEffect();
        }
    }

    #endregion

    #region Event Handling
    
    private void TurnOn(object _sender, InteractableObjectEventArgs _e)
    {
        m_active = true;
        m_controllerReference = VRTK_ControllerReference.GetControllerReference(_e.interactingObject);
        StartCoroutine(FadeEffect());
        StartCoroutine(LoopHapticEffect());
    }

    private void TurnOff(object _sender, InteractableObjectEventArgs _e)
    {
        m_active = false;
        StartCoroutine(FadeEffect());
        CancelHapticEffect();
    }

    #endregion

    #region Shader Effect

    private IEnumerator FadeEffect()
    {
        bool startState = m_active;
        float start = Time.time;
        while (Time.time < start + BlendDuration && m_active == startState)
        {
            float amount = (Time.time - start) / BlendDuration;
            ApplyEffect((m_active ? amount : 1 - amount) * EffectMax);
            yield return 1;
        }
        if (m_active == startState)
            ApplyEffect(m_active ? 1 : 0 * EffectMax);
    }

    private void ApplyEffect(float _amount)
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

    private IEnumerator LoopHapticEffect()
    {
        while (m_active)
        {
            VRTK_ControllerHaptics.TriggerHapticPulse(m_controllerReference, HapticClip);
            yield return new WaitForSeconds(HapticClip.length);
        }
    }

    private void CancelHapticEffect()
    {
        VRTK_ControllerHaptics.CancelHapticPulse(m_controllerReference);
    }
    

    #endregion
}
