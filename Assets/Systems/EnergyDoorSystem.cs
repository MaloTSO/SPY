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

            // Vérifie si un opérateur est défini, sinon attribue "=" par défaut
            string conditionOperator = string.IsNullOrEmpty(doorComponent.conditionOperator) ? "=" : doorComponent.conditionOperator;

            // Vérifie la condition énergétique
            bool conditionMet = false;

            switch (conditionOperator)
            {
                case ">":
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
                case "=":
                    // Comparaison avec égalité si aucun autre opérateur n'est défini
                    conditionMet = gameData.totalEnergie == doorComponent.requiredEnergy;
                    break;
                default:
                    // Par sécurité, on utilise l'égalité comme fallback
                    Debug.LogWarning($"Unknown condition operator: {doorComponent.conditionOperator}. Using default '=' operator.");
                    conditionMet = gameData.totalEnergie == doorComponent.requiredEnergy;
                    break;
            }

            // Si la condition est remplie, ouvre la porte
            if (conditionMet && !doorComponent.isOpen)
            {
                doorComponent.isOpen = true;
                doorComponent.transform.parent.GetComponent<AudioSource>().Play();
                doorComponent.transform.parent.GetComponent<Animator>().SetTrigger("Open");
                doorComponent.transform.parent.GetComponent<Animator>().speed = gameData.gameSpeed_current * 3; // Vitesse de l'animation
            }
            if (!conditionMet && doorComponent.isOpen)
            {
                doorComponent.isOpen = false;
                doorComponent.transform.parent.GetComponent<AudioSource>().Play();
                doorComponent.transform.parent.GetComponent<Animator>().SetTrigger("Close");
                doorComponent.transform.parent.GetComponent<Animator>().speed = gameData.gameSpeed_current; // Vitesse de l'animation
            }
        }
    }

}
