using UnityEngine;

public class Host : MonoBehaviour
{
    public GameObject Target;

    private EasterEgg m_easterEgg;
    
    void Start()
    {
        GameObject egg = Instantiate(Resources.Load("MagicAkron") as GameObject, Target.transform.position, Target.transform.rotation, Target.transform);
        if (egg == null) return;

        m_easterEgg = egg.GetComponent<EasterEgg>();
        m_easterEgg.Setup(Target);
    }
}
