using System;
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

    void Start()
    {
        m_interactableObject = GetComponentInChildren<VRTK_InteractableObject>();
        m_interactableObject.InteractableObjectGrabbed += TurnOn;
        m_interactableObject.InteractableObjectUngrabbed += TurnOff;

        m_renderers = Target.GetComponentsInChildren<Renderer>().ToList();
        m_renderers.ForEach(r =>
        {
            r.materials.ToList().ForEach(m =>
            {
                m.shader = EffectBlend;
                m.SetFloat("_EffectBlend", 0);
                m.SetFloat("_NumberSteps", EffectParameters.GetFloat("_NumberSteps"));
                m.SetFloat("_TotalDepth", EffectParameters.GetFloat("_TotalDepth"));
                m.SetFloat("_NoiseSize", EffectParameters.GetFloat("_NoiseSize"));
                m.SetFloat("_NoiseSpeed", EffectParameters.GetFloat("_NoiseSpeed"));
                m.SetFloat("_HueSize", EffectParameters.GetFloat("_HueSize"));
                m.SetFloat("_BaseHue", EffectParameters.GetFloat("_BaseHue"));
            });
        });
    }

    private void TurnOn(object sender, InteractableObjectEventArgs e)
    {
        StartCoroutine(FadeEffect(true));
        m_controllerReference = VRTK_ControllerReference.GetControllerReference(e.interactingObject);
    }

    private void TurnOff(object sender, InteractableObjectEventArgs e)
    {
        StartCoroutine(FadeEffect(false));
    }

    IEnumerator FadeEffect(bool _state)
    {
        m_active = _state;
        float start = Time.time;
        while (Time.time < start + BlendDuration && m_active == _state)
        {
            float amount = (Time.time - start) / BlendDuration;
            ApplyEffect((m_active ? amount : 1 - amount) * EffectMax);
            yield return 1;
        }
        if (m_active == _state)
            ApplyEffect(m_active ? 1 : 0 * EffectMax);
    }

    IEnumerator HapticEffect()
    {
        while (m_active)
        {
            VRTK_ControllerHaptics.TriggerHapticPulse(m_controllerReference, HapticClip);
            yield return 1;
        }
    }

    private void ApplyEffect(float _amount)
    {
        m_renderers.ForEach(r =>
        {
            r.materials.ToList().ForEach(m =>
            {
                m.SetFloat("_EffectBlend", _amount);
            });
        });
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            m_active = !m_active;
            StartCoroutine(FadeEffect(m_active));
        }
    }
}
