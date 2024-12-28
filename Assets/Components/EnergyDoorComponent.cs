using UnityEngine;

public class EnergyDoorComponent : MonoBehaviour
{
    public int requiredEnergy; // Seuil d'énergie requis pour ouvrir
    public string conditionOperator; // Opérateur de comparaison
    public bool isOpen = false; // Indique si la porte est ouverte
}