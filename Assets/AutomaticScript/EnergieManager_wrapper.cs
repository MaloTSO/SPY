using UnityEngine;
using FYFY;

public class EnergieManager_wrapper : BaseWrapper
{
	public TMPro.TextMeshProUGUI energyText;
	private void Start()
	{
		this.hideFlags = HideFlags.NotEditable;
		MainLoop.initAppropriateSystemField (system, "energyText", energyText);
	}

}
