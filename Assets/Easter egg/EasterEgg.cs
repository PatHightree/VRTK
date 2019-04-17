using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    protected bool DebugLog = false;
    public EasterEggState State { get; private set; } = EasterEggState.PrerequisitesNotMet;

    protected GameObject Target { get; private set; }
    

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
        CheckPrerequisites();
        if (State != EasterEggState.PrerequisitesMet) return;
        
        State = EasterEggState.SettingUp;
        DoSetup();
        State = EasterEggState.SetUp;
    }
    protected abstract void DoSetup();

    protected void CheckActivationTrigger(object _sender)
    {
        if (State != EasterEggState.SetUp) return;
        if (!DoCheckActivationTrigger(_sender)) return;
        
        Activate();
    }
    protected abstract bool DoCheckActivationTrigger(object _sender);
    
    protected void CheckDeactivationTrigger(object _sender)
    {
        if (State != EasterEggState.Active) return;
        if (!DoCheckDeactivationTrigger(_sender)) return;
        
        Deactivate();
    }
    protected abstract bool DoCheckDeactivationTrigger(object _sender);
    
    public void Activate()
    {
        if (State != EasterEggState.SetUp) return;
        
        State = EasterEggState.Activating;
        DoActivate();
    }
    protected abstract void DoActivate();
    protected void DoActivateFinished()
    {
        State = EasterEggState.Active;
    }

    public void Deactivate()
    {
        if (State != EasterEggState.Active) return;
        
        State = EasterEggState.Deactivating;
        DoDeactivate();
    }
    protected abstract void DoDeactivate();
    protected void DoDeactivateFinished()
    {
        State = EasterEggState.SetUp;
    }

    public void Teardown()
    {
        if (State != EasterEggState.SetUp) return;

        State = EasterEggState.TearingDown;
        DoTeardown();
        CheckPrerequisites();
    }
    protected abstract void DoTeardown();
}