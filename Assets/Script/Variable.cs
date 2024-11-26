using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Variable : MonoBehaviour
{
    public string variableName;
    public int value;

    public void Increment(int amount)
    {
        value += amount;
    }
    public void decrement(int amount)
    {
        value -= amount;
    }

    public void SetValue(int newValue)
    {
        value = newValue;
    }

}
