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
    public EasterEggState State { get; private set; } = EasterEggState.PrerequisitesNotMet;

    protected Transform Target { get; private set; }
    
    private List<Type> m_targetPrerequisites;
    private List<Type> m_childPrerequisites;

    protected void Create(Transform _target, List<Type> _targetPrerequisites, List<Type> _childPrerequisites)
    {
        Target = _target;
        m_targetPrerequisites = _targetPrerequisites;
        m_childPrerequisites = _childPrerequisites;
        
        CheckPrerequisites();
    }

    public bool CheckPrerequisites()
    {
        State = m_targetPrerequisites.All(_p => Target.GetComponent(_p) != null) && 
                m_childPrerequisites.All(_p => Target.GetComponentInChildren(_p) != null)  
            ? EasterEggState.PrerequisitesMet
            : EasterEggState.PrerequisitesNotMet;
        return State == EasterEggState.PrerequisitesMet;
    }
    
    public void Setup()
    {
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
        State = EasterEggState.PrerequisitesMet;
    }
    protected abstract void DoTeardown();
}