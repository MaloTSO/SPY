using UnityEngine.EventSystems;
using UnityEngine;
using FYFY;

public class TooltipContent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string text;
    private Tooltip tooltip = null;

    private bool isOver = false;

    private void Start()
    {
        GameObject tooltipGO = GameObject.Find("TooltipUI_Pointer");
        if (!tooltipGO)
        {
            GameObjectManager.unbind(gameObject);
            GameObject.Destroy(this);
        }
        else
            tooltip = tooltipGO.GetComponent<Tooltip>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        string formatedContent = text;

        // Remplacement pour #agentName, s'il est trouvé
        if (formatedContent.Contains("#agentName"))
        {
            formatedContent = formatedContent.Replace("#agentName", GetComponent<AgentEdit>().associatedScriptName);
        }

        // Remplacement pour #requiredEnergy, s'il est trouvé
        if (formatedContent.Contains("#requiredEnergy"))
        {
            EnergyDoorComponent doorComponent = GetComponent<EnergyDoorComponent>();
            formatedContent = formatedContent.Replace("#requiredEnergy", doorComponent.requiredEnergy.ToString());
        }

        // Remplacement pour #conditionOperator, s'il est trouvé
        if (formatedContent.Contains("#conditionOperator"))
        {
            EnergyDoorComponent doorComponent = GetComponent<EnergyDoorComponent>();
            formatedContent = formatedContent.Replace("#conditionOperator", doorComponent.conditionOperator);
        }

        // Affiche le tooltip avec le contenu formaté
        tooltip.ShowTooltip(formatedContent);
        isOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null)
        {
            tooltip.HideTooltip();
            isOver = false;
        }
    }

    public void OnDisable()
    {
        if (isOver)
        {
            tooltip.HideTooltip();
            isOver = false;
        }
    }
}
