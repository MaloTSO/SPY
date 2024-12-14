using UnityEngine;
using FYFY;

public class EnergyDoorSystem : FSystem
{
    private Family f_energyDoors = FamilyManager.getFamily(new AllOfComponents(typeof(EnergyDoorComponent), typeof(Animator)));
    private GameData gameData;

    protected override void onStart()
    {
        // Récupération de GameData
        GameObject go = GameObject.Find("GameData");
        if (go != null)
            gameData = go.GetComponent<GameData>();
    }

    protected override void onProcess(int familiesUpdateCount)
    {
        // Parcourt toutes les portes énergétiques dans la famille
        foreach (GameObject door in f_energyDoors)
        {
            EnergyDoorComponent doorComponent = door.GetComponent<EnergyDoorComponent>();

            // Vérifie si la porte doit s'ouvrir
            if (!doorComponent.isOpen && gameData.totalEnergie >= doorComponent.requiredEnergy)
            {
                OpenDoor(door);
                doorComponent.isOpen = true; // Marquer comme ouvert
            }
        }
    }

    private void OpenDoor(GameObject door)
    {
        Animator animator = door.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Open"); // Déclenche l'animation d'ouverture
        }
        Debug.Log("La porte s'est ouverte !");
    }
}
