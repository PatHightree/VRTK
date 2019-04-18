using UnityEngine;

public class Host : MonoBehaviour
{
    public GameObject Target;

    private GameObject m_easterEggGO;
    private EasterEgg m_easterEgg;
    
    void Start()
    {
        m_easterEggGO = Instantiate(Resources.Load("MagicAkron") as GameObject, Target.transform.position, Target.transform.rotation, Target.transform);
        if (m_easterEggGO == null) return;

        m_easterEgg = m_easterEggGO.GetComponent<EasterEgg>();
        m_easterEgg.Setup(Target);
//        m_easterEgg.Activate();
    }

    private void Update()
    {
        if (m_easterEgg == null) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (m_easterEgg.State == EasterEggState.Active)
                m_easterEgg.Deactivate();
            if (m_easterEgg.State == EasterEggState.SetUp)
                m_easterEgg.Activate();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            m_easterEgg.Teardown(() => Destroy(m_easterEggGO));
        }
    }
}
