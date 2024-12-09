using UnityEngine;
using FYFY;
using System.Collections;
using FYFY_plugins.TriggerManager;
using TMPro;

/// <summary>
/// Manage collision between player agents and Coins
/// </summary>
public class EnergieManager : FSystem {
    private Family f_robotcollision = FamilyManager.getFamily(new AllOfComponents(typeof(Triggered3D)), new AnyOfTags("Player"));

	private Family f_playingMode = FamilyManager.getFamily(new AllOfComponents(typeof(PlayMode)));
	private Family f_editingMode = FamilyManager.getFamily(new AllOfComponents(typeof(EditMode)));

	private GameData gameData;
    private bool activeEnergie;

	public TextMeshProUGUI energyText;

	protected override void onStart()
    {
		activeEnergie = false;
		GameObject go = GameObject.Find("GameData");
		if (go != null)
			gameData = go.GetComponent<GameData>();
		f_robotcollision.addEntryCallback(onNewCollision);

		f_playingMode.addEntryCallback(delegate { activeEnergie = true; });
		f_editingMode.addEntryCallback(delegate { activeEnergie = false; });

		if (energyText != null){
			energyText.text = "Energy: " + gameData.totalEnergie.ToString();
		}

	}

	private void onNewCollision(GameObject robot){
		if(activeEnergie){
			Triggered3D trigger = robot.GetComponent<Triggered3D>();
			foreach(GameObject target in trigger.Targets){
				//Check if the player collide with a 
                if(target.CompareTag("Energie")){
                    gameData.totalEnergie++;
					Debug.Log(gameData.totalEnergie);
					if(energyText != null){
						energyText.text = "Energy: " + gameData.totalEnergie.ToString();
					}
                    // target.GetComponent<AudioSource>().Play();
					target.GetComponent<Collider>().enabled = false;
                    MainLoop.instance.StartCoroutine(energieDestroy(target));					
				}
			}			
		}
    }

	private IEnumerator energieDestroy(GameObject go){
		go.GetComponent<ParticleSystem>().Play();
		go.GetComponent<Renderer>().enabled = false;
		yield return new WaitForSeconds(1f); // let time for animation
		GameObjectManager.setGameObjectState(go, false); // then disabling GameObject
	}
}