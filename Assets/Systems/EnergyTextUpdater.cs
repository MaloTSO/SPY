using UnityEngine;
using FYFY;
using FYFY_plugins.PointerManager;
using UnityEngine.UI;
using TMPro;

public class EnergieTextUpdater : FSystem
{
    // Cette famille contient toutes les entités qui ont le composant EnergyTextComponent
    private Family f_energyTextFamily = FamilyManager.getFamily(new AllOfComponents(typeof(EnergyTextComponent)));

    private GameData gameData;  // Référence à GameData pour obtenir la valeur de l'énergie

    protected override void onStart()
    {
        // Trouve l'objet contenant GameData dans la scène
        GameObject go = GameObject.Find("GameData");
        if (go != null)
            gameData = go.GetComponent<GameData>();  // Accède aux données de l'énergie globale
    }

    protected void onProcess()
    {
        // Parcourt toutes les entités qui ont le composant EnergyTextComponent
        foreach (GameObject go in f_energyTextFamily)
        {
            // Récupère le composant EnergyTextComponent de chaque entité
            EnergyTextComponent energyTextComponent = go.GetComponent<EnergyTextComponent>();

            if (energyTextComponent != null && energyTextComponent.energyText != null)
            {
                // Met à jour le texte avec la valeur actuelle de l'énergie dans GameData
                energyTextComponent.energyText.text = "Quantité d'energie : " + gameData.totalEnergie.ToString();
            }
        }
    }
}
