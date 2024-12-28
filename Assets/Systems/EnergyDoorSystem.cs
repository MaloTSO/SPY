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

            // Vérifie la condition énergétique
            bool conditionMet = false;

            switch (doorComponent.conditionOperator)
            {
                case ">":
                    Debug.Log("ici >");
                    conditionMet = gameData.totalEnergie > doorComponent.requiredEnergy;
                    break;
                case "<":
                    conditionMet = gameData.totalEnergie < doorComponent.requiredEnergy;
                    break;
                case ">=":
                    conditionMet = gameData.totalEnergie >= doorComponent.requiredEnergy;
                    break;
                case "<=":
                    conditionMet = gameData.totalEnergie <= doorComponent.requiredEnergy;
                    break;
                default:
                    // Pas d'opérateur, compare avec égalité
                    conditionMet = gameData.totalEnergie == doorComponent.requiredEnergy;
                    break;
            }

            // Si la condition est remplie, ouvre la porte
            if (conditionMet)
            {
                doorComponent.transform.parent.GetComponent<Animator>().SetTrigger("Open");
                doorComponent.transform.parent.GetComponent<Animator>().speed = gameData.gameSpeed_current*3; // Vitesse de l'animation
            }
        }
    }
}
