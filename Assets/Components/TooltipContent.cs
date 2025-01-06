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
            AgentEdit agentEdit = GetComponent<AgentEdit>();
            if (agentEdit != null)
            {
                formatedContent = formatedContent.Replace("#agentName", agentEdit.associatedScriptName);
            }
            else
            {
                Debug.LogWarning("AgentEdit component is missing on this GameObject!");
            }
        }

        // Remplacement pour #requiredEnergy, s'il est trouvé
        if (formatedContent.Contains("#requiredEnergy"))
        {
            Transform doorTransform = transform.parent.Find("Door");
            if (doorTransform != null)
            {
                EnergyDoorComponent doorComponent = doorTransform.GetComponentInChildren<EnergyDoorComponent>();
                if (doorComponent != null)
                {
                    formatedContent = formatedContent.Replace("#requiredEnergy", doorComponent.requiredEnergy.ToString());
                }
                else
                {
                    Debug.LogWarning("EnergyDoorComponent is missing on this GameObject!");
                }
            }
            else
            {
                Debug.LogWarning("Door GameObject is missing on this GameObject!");
            }
        }

        // Remplacement pour #conditionOperator, s'il est trouvé
        if (formatedContent.Contains("#conditionOperator"))
        {
            Transform doorTransform = transform.parent.Find("Door");
            if (doorTransform != null)
            {
                EnergyDoorComponent doorComponent = doorTransform.GetComponentInChildren<EnergyDoorComponent>();
                if (doorComponent != null)
                {
                    formatedContent = formatedContent.Replace("#conditionOperator", doorComponent.conditionOperator);
                }
                else
                {
                    Debug.LogWarning("EnergyDoorComponent is missing on this GameObject!");
                }
            }else
            {
                Debug.LogWarning("Door GameObject is missing on this GameObject!");
            }
        }

        if (formatedContent.Contains("#energie"))
        {
            EnergyComponent energyComponent = GetComponent<EnergyComponent>();
            if (energyComponent != null)
            {
                formatedContent = formatedContent.Replace("#energie", energyComponent.energie.ToString());
            }
            else
            {
                Debug.LogWarning("EnergyComponent is missing on this GameObject!");
            }
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
