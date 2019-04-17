using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// EasterEgg State Transitions :
// 
//         +------> PrerequisitesNotMet
//         |
//    Init +------> PrerequisitesMet +-----> SettingUp +-----> SetUp +----------> Activating +---> Active
//                         ^                                    + ^                                  +
//                         |                                    | |                                  |
//                         |                                    | |                                  |
//                         +-------------+ TearingDown <--------+ +------------+ Deactivating <------+

public enum EasterEggState
{
    PrerequisitesNotMet,
    PrerequisitesMet,
    SetUp,
    SettingUp,
    Activating,
    Active,
    Deactivating,
    TearingDown
}

public abstract  class EasterEgg : MonoBehaviour
{
    protected GameObject Target { get; private set; }
    protected bool DebugLog = false;

    public EasterEggState State
    {
        get => m_state;
        private set
        {
            m_state = value;
            if (DebugLog) Debug.Log("<color=magenta>State = " + State + "</color>");
        }
    }
    private EasterEggState m_state;
    
    #region Setup

    protected abstract void GetPrerequisites(out List<Type> _targetPrerequisites, out List<Type> _childPrerequisites);

    private bool CheckPrerequisites()
    {
        GetPrerequisites(out var targetPrerequisites, out var childPrerequisites);
        State = targetPrerequisites.All(_p => Target.GetComponent(_p) != null) && 
                childPrerequisites.All(_p => Target.GetComponentInChildren(_p) != null)  
            ? EasterEggState.PrerequisitesMet
            : EasterEggState.PrerequisitesNotMet;
        
        if (State==EasterEggState.PrerequisitesNotMet)
            Debug.LogWarningFormat("<color=red>Easter egg {0}: prerequisites not met!</color>", name);
        return State == EasterEggState.PrerequisitesMet;
    }
    
    public void Setup(GameObject _target)
    {
        Target = _target;
        if (!CheckPrerequisites()) return;
        
        State = EasterEggState.SettingUp;
        DoSetup(SetupFinished);
    }
    
    protected abstract void DoSetup(Action _setupFinished);
    
    private void SetupFinished()
    {
        State = EasterEggState.SetUp;
    }

    #endregion

    #region Activate

    protected void CheckActivationTrigger(object _sender)
    {
        if (State != EasterEggState.SetUp) return;
        if (!DoCheckActivationTrigger(_sender)) return;
        
        Activate();
    }
    
    protected abstract bool DoCheckActivationTrigger(object _sender);
    
    public void Activate()
    {
        if (State != EasterEggState.SetUp) return;
        
        State = EasterEggState.Activating;
        DoActivate(ActivateFinished);
    }
    
    protected abstract void DoActivate(Action _activateFinished);
    
    private void ActivateFinished()
    {
        State = EasterEggState.Active;
    }

    #endregion

    #region Deactivate

    protected void CheckDeactivationTrigger(object _sender)
    {
        if (State != EasterEggState.Active) return;
        if (!DoCheckDeactivationTrigger(_sender)) return;
        
        Deactivate();
    }
    
    protected abstract bool DoCheckDeactivationTrigger(object _sender);

    public void Deactivate()
    {
        if (State != EasterEggState.Active) return;
        
        State = EasterEggState.Deactivating;
        DoDeactivate(DeactivateFinished);
    }
    
    protected abstract void DoDeactivate(Action _deactivateFinished);
    
    private void DeactivateFinished()
    {
        State = EasterEggState.SetUp;
    }

    #endregion

    #region Teardown

    public void Teardown(Action _finished)
    {
        State = EasterEggState.TearingDown;
        DoTeardown(() => TeardownFinished(_finished));
    }
    
    protected abstract void DoTeardown(Action _teardownFinished);
    
    private void TeardownFinished(Action _finished)
    {
        CheckPrerequisites();
        _finished?.Invoke();
    }

    #endregion
}