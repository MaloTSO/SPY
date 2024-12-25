using UnityEngine;
using FYFY;
using System.Collections;

public class EnergyDoorSystem : FSystem
{
    private Family f_energyDoors = FamilyManager.getFamily(new AllOfComponents(typeof(EnergyDoorComponent), typeof(Position)), new AnyOfTags("DoorEnergie"));
    private Family quads = FamilyManager.getFamily(new AllOfComponents(typeof(Collider)));
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
            if (gameData.totalEnergie >= doorComponent.requiredEnergy)
            {
                foreach (GameObject quad in quads)
                {
                    if (quad.CompareTag("Quad")) // Filtrer par tag si nécessaire
                    {
                        Collider collider = quad.GetComponent<Collider>();
                        if (collider != null)
                        {
                            collider.enabled = false; // Désactiver uniquement le collider
                        }
                    }
                }
                doorComponent.transform.GetComponent<Animator>().SetTrigger("Open");
                doorComponent.transform.GetComponent<Animator>().speed = gameData.gameSpeed_current; // Vitesse de l'animation
            }
        }
    }
}
