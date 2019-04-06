using System.Collections.Generic;
using UnityEngine;
using VRTK;

public class EasterEggTouchpadCircleTrigger : MonoBehaviour
{
    public List<GameObject> Targets;
    public Material Effect;

    private List<VRTK_ControllerEvents> m_controllerEvents;
    private Dictionary<Renderer, Material> m_PrevMaterials;
    private bool m_TouchpadPressed;
    private bool m_ValidMovement;
    private float m_PrevTouchpadAngle;
    private float m_StartTouchpadAngle;

    void Start()
    {
        m_PrevMaterials = new Dictionary<Renderer, Material>();
        m_controllerEvents = new List<VRTK_ControllerEvents>();
        GameObject test = VRTK_SDK_Bridge.GetControllerLeftHand(true);
        m_controllerEvents.Add(VRTK_SDK_Bridge.GetControllerLeftHand(true)?.GetComponentInChildren<VRTK_ControllerEvents>());
        m_controllerEvents.Add(VRTK_SDK_Bridge.GetControllerRightHand(true)?.GetComponentInChildren<VRTK_ControllerEvents>());

        m_controllerEvents?.ForEach(ce => ce.TouchpadPressed += OnTouchpadPressed);
        m_controllerEvents?.ForEach(ce => ce.TouchpadReleased += OnTouchpadReleased);
        m_controllerEvents?.ForEach(ce => ce.TouchpadAxisChanged += OnTouchpadAxisChanged);
    }

    private void OnTouchpadPressed(object sender, ControllerInteractionEventArgs e)
    {
        m_TouchpadPressed = true;
        m_PrevTouchpadAngle = e.touchpadAngle;
        m_StartTouchpadAngle = e.touchpadAngle;
    }

    private void OnTouchpadReleased(object sender, ControllerInteractionEventArgs e)
    {
        m_TouchpadPressed = false;
    }

    private void OnTouchpadAxisChanged(object sender, ControllerInteractionEventArgs e)
    {
        m_ValidMovement = e.touchpadAngle > m_PrevTouchpadAngle;
        if (m_TouchpadPressed && m_ValidMovement)
            Debug.Log(e.touchpadAngle);
    }

    void Update()
    {
        //if (m_TouchpadPressed)
        //{
        //    m_controllerEvents[0].
        //}
    }
}
