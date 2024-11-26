using FYFY;
using UnityEngine;

public class VariableSystem : FSystem
{
    private Family variableFamily  = FamilyManager.getFamily(new AllOfComponents(typeof(Variable)));

    public void ModifyVariable(GameObject target, string variableName, int amount)
    {
        foreach (GameObject go in variableFamily)
        {
            Variable variable = go.GetComponent<Variable>();
            if (variable.variableName == variableName)
            {
                variable.Increment(amount);
                Debug.Log($"{variableName} mise à jour : {variable.value}");
            }
        }
    }

    public int GetVariableValue(GameObject target, string variableName)
    {
        foreach (GameObject go in variableFamily)
        {
            Variable variable = go.GetComponent<Variable>();
            if (variable.variableName == variableName)
            {
                return variable.value;
            }
        }
        return 0;
    }
}
