using UnityEngine;
using FYFY;
using System.Collections.Generic;
using System.Xml;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections;
using UnityEngine.Networking;
using System.Runtime.InteropServices;
using System;

/// <summary>
/// Read XML file and load level
/// Need to be binded before UISystem
/// </summary>
public class LevelGenerator : FSystem {

	public static LevelGenerator instance;

	// Famille contenant les agents editables
	private Family f_level = FamilyManager.getFamily(new AnyOfComponents(typeof(Position), typeof(CurrentAction)));
	private Family f_drone = FamilyManager.getFamily(new AllOfComponents(typeof(ScriptRef)), new AnyOfTags("Drone")); // On r�cup�re les agents pouvant �tre �dit�s
	private Family f_draggableElement = FamilyManager.getFamily(new AnyOfComponents(typeof(ElementToDrag)));

	public GameObject LevelGO;
	private List<List<int>> map;
	private GameData gameData;
	private int nbAgentCreate = 0; // Nombre d'agents cr��s
	private int nbDroneCreate = 0; // Nombre de drones cr��s
	private HashSet<string> scriptNameUsed = new HashSet<string>();
	private GameObject lastAgentCreated = null;

	public GameObject editableCanvas;// Le container qui contient les Viewport/script containers
	public GameObject scriptContainer;
	public GameObject library; // Le viewport qui contient la librairie
	public TMP_Text levelName;
	public GameObject canvas;
	public GameObject buttonExecute;

	[DllImport("__Internal")]
	private static extern void HideHtmlButtons(); // call javascript

	public LevelGenerator()
	{
		instance = this;
	}

	protected override void onStart()
	{
		GameObject gameDataGO = GameObject.Find("GameData");
		if (gameDataGO == null)
			GameObjectManager.loadScene("TitleScreen");
		else
		{
			gameData = gameDataGO.GetComponent<GameData>();
			if (gameData.levels.ContainsKey(gameData.levelToLoad.src))
				XmlToLevel(gameData.levels[gameData.levelToLoad.src].OwnerDocument);
			else
				GameObjectManager.addComponent<NewEnd>(MainLoop.instance.gameObject, new { endType = NewEnd.Error });
			levelName.text = gameData.levelToLoad.name;
			if (Application.platform == RuntimePlatform.WebGLPlayer)
				HideHtmlButtons();
            GameObjectManager.addComponent<ActionPerformedForLRS>(LevelGO, new
			{
				verb = "launched",
				objectType = "level",
				activityExtensions = new Dictionary<string, string>() {
					{ "value", gameData.levelToLoad.src.Replace(new Uri(Application.streamingAssetsPath + "/").AbsoluteUri, "") }
				}
			});
		}
	}

	// Read xml document and create all game objects
	public void XmlToLevel(XmlDocument doc)
	{
		gameData.totalActionBlocUsed = 0;
		gameData.totalStep = 0;
		gameData.totalExecute = 0;
		gameData.totalCoin = 0;
		gameData.levelToLoadScore = null;
		// check if dialogs are defined in the scenario
		bool dialogsOverrided = true;
		if (gameData.levelToLoad.overridedDialogs == null)
		{
			dialogsOverrided = false;
			gameData.levelToLoad.overridedDialogs = new List<Dialog>();
		}
		gameData.actionBlockLimit = new Dictionary<string, int>();
		map = new List<List<int>>();

		// remove comments
		EditingUtility.removeComments(doc);

		XmlNode root = doc.ChildNodes[1];

		// check if dragdropDisabled node exists and set gamedata accordingly
		gameData.dragDropEnabled = doc.GetElementsByTagName("dragdropDisabled").Count == 0;

		foreach (XmlNode child in root.ChildNodes)
		{
			switch (child.Name)
			{
				case "map":
					readXMLMap(child);
					break;
				case "dialogs":
					if (!dialogsOverrided)
						EditingUtility.readXMLDialogs(child, gameData.levelToLoad.overridedDialogs);
					break;
				case "executionLimit":
					int amount = int.Parse(child.Attributes.GetNamedItem("amount").Value);
					if (amount > 0)
					{
						GameObject amountGO = buttonExecute.transform.GetChild(1).gameObject;
						GameObjectManager.setGameObjectState(amountGO, true);
						amountGO.GetComponentInChildren<TMP_Text>(true).text = "" + amount;
					}
					break;
				case "fog":
					// fog has to be enabled after agents
					MainLoop.instance.StartCoroutine(delayEnableFog());
					break;
				case "blockLimits":
					readXMLLimits(child);
					break;
				case "coin":
					createCoin(int.Parse(child.Attributes.GetNamedItem("posX").Value), int.Parse(child.Attributes.GetNamedItem("posY").Value));
					break;
				case "console":
					readXMLConsole(child);
					break;
				case "door":
					createDoor(int.Parse(child.Attributes.GetNamedItem("posX").Value), int.Parse(child.Attributes.GetNamedItem("posY").Value),
					(Direction.Dir)int.Parse(child.Attributes.GetNamedItem("direction").Value), int.Parse(child.Attributes.GetNamedItem("slotId").Value));
					break;
				case "player":
				case "enemy":
					string nameAgentByUser = "";
					XmlNode agentName = child.Attributes.GetNamedItem("associatedScriptName");
					if (agentName != null && agentName.Value != "")
						nameAgentByUser = agentName.Value;
					GameObject agent = createEntity(nameAgentByUser, int.Parse(child.Attributes.GetNamedItem("posX").Value), int.Parse(child.Attributes.GetNamedItem("posY").Value),
					(Direction.Dir)int.Parse(child.Attributes.GetNamedItem("direction").Value), child.Name);
					if (child.Name == "enemy")
					{
						agent.GetComponent<DetectRange>().range = int.Parse(child.Attributes.GetNamedItem("range").Value);
						agent.GetComponent<DetectRange>().selfRange = bool.Parse(child.Attributes.GetNamedItem("selfRange").Value);
						agent.GetComponent<DetectRange>().type = (DetectRange.Type)int.Parse(child.Attributes.GetNamedItem("typeRange").Value);
					}
					else
						lastAgentCreated = agent;
					break;
				case "decoration":
					createDecoration(child.Attributes.GetNamedItem("name").Value, int.Parse(child.Attributes.GetNamedItem("posX").Value), int.Parse(child.Attributes.GetNamedItem("posY").Value), (Direction.Dir)int.Parse(child.Attributes.GetNamedItem("direction").Value));
					break;
				case "script":
					UIRootContainer.EditMode editModeByUser = UIRootContainer.EditMode.Locked;
					XmlNode editMode = child.Attributes.GetNamedItem("editMode");
					int tmpValue;
					if (editMode != null && int.TryParse(editMode.Value, out tmpValue))
						editModeByUser = (UIRootContainer.EditMode)tmpValue;
					UIRootContainer.SolutionType typeByUser = UIRootContainer.SolutionType.Undefined;
					XmlNode typeNode = child.Attributes.GetNamedItem("type");
					if (typeNode != null && int.TryParse(typeNode.Value, out tmpValue))
						typeByUser = (UIRootContainer.SolutionType)tmpValue;
					// Script has to be created after agents
					MainLoop.instance.StartCoroutine(delayReadXMLScript(child, child.Attributes.GetNamedItem("name").Value, editModeByUser, typeByUser));
					break;
				case "score":
					gameData.levelToLoadScore = new int[2];
					gameData.levelToLoadScore[0] = int.Parse(child.Attributes.GetNamedItem("threeStars").Value);
					gameData.levelToLoadScore[1] = int.Parse(child.Attributes.GetNamedItem("twoStars").Value);
					break;
			}
		}
		eraseMap();
		generateMap(doc.GetElementsByTagName("hideExits").Count > 0);
		MainLoop.instance.StartCoroutine(delayGameLoaded());
	}

	IEnumerator delayGameLoaded()
	{
		yield return null;
		yield return null;
		GameObjectManager.addComponent<GameLoaded>(MainLoop.instance.gameObject);
	}

	IEnumerator delayEnableFog()
	{
		yield return null;
		if (lastAgentCreated != null)
		{
			Transform fog = lastAgentCreated.transform.Find("Fog");
			if (fog != null)
				GameObjectManager.setGameObjectState(fog.gameObject, true);
		}
	}

	IEnumerator delayReadXMLScript(XmlNode scriptNode, string name, UIRootContainer.EditMode editMode, UIRootContainer.SolutionType type)
    {
		yield return null; // wait agent was created
		readXMLScript(scriptNode, name, editMode, type);
	}

	// read the map and create wall, ground, spawn and exit
	private void generateMap(bool hideExits){
		for (int y = 0; y< map.Count; y++){
			for(int x = 0; x < map[y].Count; x++){
				switch (map[y][x]){
					case -1: // void
						createWall(x, y, false);
						break;
					case 0: // Path
						createCell(x,y);
						break;
					case 1: // Wall
						createCell(x,y);
						createWall(x,y, true);
						break;
					case 2: // Spawn
						createCell(x,y);
						createSpawnExit(x,y,true);
						break;
					case 3: // Exit
						createCell(x,y);
						createSpawnExit(x,y,false, hideExits);
						break;
				}
			}
		}
	}

	// Cr�er une entit� agent ou robot et y associer un panel container
	private GameObject createEntity(string nameAgent, int gridX, int gridY, Direction.Dir direction, string type){
		GameObject entity = null;
		switch(type){
			case "player": // Robot
				entity = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/Robot Kyle") as GameObject, LevelGO.transform.position + new Vector3(gridY*3,1.5f,gridX*3), Quaternion.Euler(0,0,0), LevelGO.transform);
				break;
			case "enemy": // Enemy
				entity = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/Drone") as GameObject, LevelGO.transform.position + new Vector3(gridY*3,3.8f,gridX*3), Quaternion.Euler(0,0,0), LevelGO.transform);
				break;
		}

		// Charger l'agent aux bonnes coordon�es dans la bonne direction
		entity.GetComponent<Position>().x = gridX;
		entity.GetComponent<Position>().y = gridY;
		entity.GetComponent<Position>().targetX = -1;
		entity.GetComponent<Position>().targetY = -1;
		entity.GetComponent<Direction>().direction = direction;
		
		//add new container to entity
		ScriptRef scriptref = entity.GetComponent<ScriptRef>();
		GameObject executablePanel = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/ExecutablePanel") as GameObject, scriptContainer.gameObject.transform, false);
		// Associer � l'agent l'UI container
		scriptref.executablePanel = executablePanel;
		// Associer � l'agent le script container
		scriptref.executableScript = executablePanel.transform.Find("Scroll View").Find("Viewport").Find("ScriptContainer").gameObject;
		// Association de l'agent au script de gestion des fonctions
		executablePanel.GetComponentInChildren<LinkedWith>(true).target = entity;

		// On va charger l'image et le nom de l'agent selon l'agent (robot, ennemi etc...)
		if (type == "player")
		{
			nbAgentCreate++;
			// On nomme l'agent
			AgentEdit agentEdit = entity.GetComponent<AgentEdit>();
			if (nameAgent != "")
				agentEdit.associatedScriptName = nameAgent;
			else
				agentEdit.associatedScriptName = "Agent" + nbAgentCreate;

			// Chargement de l'ic�ne de l'agent sur la localisation
			executablePanel.transform.Find("Header").Find("locateButton").GetComponentInChildren<Image>().sprite = Resources.Load("UI Images/robotIcon", typeof(Sprite)) as Sprite;
			// Affichage du nom de l'agent
			executablePanel.transform.Find("Header").Find("agentName").GetComponent<TMP_InputField>().text = entity.GetComponent<AgentEdit>().associatedScriptName;
		}
		else if (type == "enemy")
		{
			nbDroneCreate++;
			// Chargement de l'ic�ne de l'agent sur la localisation
			executablePanel.transform.Find("Header").Find("locateButton").GetComponentInChildren<Image>().sprite = Resources.Load("UI Images/droneIcon", typeof(Sprite)) as Sprite;
			// Affichage du nom de l'agent
			if(nameAgent != "")
				executablePanel.transform.Find("Header").Find("agentName").GetComponent<TMP_InputField>().text = nameAgent;
            else
				executablePanel.transform.Find("Header").Find("agentName").GetComponent<TMP_InputField>().text = "Drone "+nbDroneCreate;
		}

		AgentColor ac = MainLoop.instance.GetComponent<AgentColor>();
		scriptref.executablePanel.transform.Find("Scroll View").GetComponent<Image>().color = (type == "player" ? ac.playerBackground : ac.droneBackground);

		executablePanel.SetActive(false);
		GameObjectManager.bind(executablePanel);
		GameObjectManager.bind(entity);
		return entity;
	}

	private void createDoor(int gridX, int gridY, Direction.Dir orientation, int slotID){
		GameObject door = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/Door") as GameObject, LevelGO.transform.position + new Vector3(gridY*3,3,gridX*3), Quaternion.Euler(0,0,0), LevelGO.transform);

		door.GetComponentInChildren<ActivationSlot>().slotID = slotID;
		door.GetComponentInChildren<Position>().x = gridX;
		door.GetComponentInChildren<Position>().y = gridY;
		door.GetComponentInChildren<Direction>().direction = orientation;
		GameObjectManager.bind(door);
	}

	private void createDecoration(string name, int gridX, int gridY, Direction.Dir orientation)
	{
		GameObject decoration = GameObject.Instantiate<GameObject>(Resources.Load("Prefabs/"+name) as GameObject, LevelGO.transform.position + new Vector3(gridY * 3, 3, gridX * 3), Quaternion.Euler(0, 0, 0), LevelGO.transform);

		decoration.GetComponent<Position>().x = gridX;
		decoration.GetComponent<Position>().y = gridY;
		decoration.GetComponent<Direction>().direction = orientation;
		GameObjectManager.bind(decoration);
	}

	private void createConsole(int state, int gridX, int gridY, List<int> slotIDs, Direction.Dir orientation)
	{
		GameObject activable = GameObject.Instantiate<GameObject>(Resources.Load("Prefabs/ActivableConsole") as GameObject, LevelGO.transform.position + new Vector3(gridY * 3, 3, gridX * 3), Quaternion.Euler(0, 0, 0), LevelGO.transform);

		activable.GetComponent<Activable>().slotID = slotIDs;
		DoorPath path = activable.GetComponentInChildren<DoorPath>();
		if (slotIDs.Count > 0)
			path.slotId = slotIDs[0];
		else
			path.slotId = -1;
		activable.GetComponent<Position>().x = gridX;
		activable.GetComponent<Position>().y = gridY;
		activable.GetComponent<Direction>().direction = orientation;
		if (state == 1)
			activable.AddComponent<TurnedOn>();
		GameObjectManager.bind(activable);
	}

	private void createSpawnExit(int gridX, int gridY, bool type, bool hideExit = false){
		GameObject spawnExit;
		if(type)
			spawnExit = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/TeleporterSpawn") as GameObject, LevelGO.transform.position + new Vector3(gridY*3,1.5f,gridX*3), Quaternion.Euler(-90,0,0), LevelGO.transform);
		else
			spawnExit = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/TeleporterExit") as GameObject, LevelGO.transform.position + new Vector3(gridY*3,1.5f,gridX*3), Quaternion.Euler(-90,0,0), LevelGO.transform);

		if (hideExit)
        {
			Component.Destroy(spawnExit.GetComponent<Renderer>());
			Component.Destroy(spawnExit.GetComponent<ParticleSystem>());
        }
			
		spawnExit.GetComponent<Position>().x = gridX;
		spawnExit.GetComponent<Position>().y = gridY;
		GameObjectManager.bind(spawnExit);
	}

	private void createCoin(int gridX, int gridY){
		GameObject coin = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/Coin") as GameObject, LevelGO.transform.position + new Vector3(gridY*3,3,gridX*3), Quaternion.Euler(90,0,0), LevelGO.transform);
		coin.GetComponent<Position>().x = gridX;
		coin.GetComponent<Position>().y = gridY;
		GameObjectManager.bind(coin);
	}

	private void createCell(int gridX, int gridY){
		GameObject cell = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/Cell") as GameObject, LevelGO.transform.position + new Vector3(gridY*3,0,gridX*3), Quaternion.Euler(0,0,0), LevelGO.transform);
		GameObjectManager.bind(cell);
	}

	private void createWall(int gridX, int gridY, bool visible = true){
		GameObject wall = GameObject.Instantiate<GameObject>(Resources.Load ("Prefabs/Wall") as GameObject, LevelGO.transform.position + new Vector3(gridY*3,3,gridX*3), Quaternion.Euler(0,0,0), LevelGO.transform);
		wall.GetComponent<Position>().x = gridX;
		wall.GetComponent<Position>().y = gridY;
		if (!visible)
			wall.GetComponent<Renderer>().enabled = false;
		GameObjectManager.bind(wall);
	}

	private void eraseMap(){
		foreach( GameObject go in f_level){
			GameObjectManager.unbind(go.gameObject);
			GameObject.Destroy(go.gameObject);
		}
	}

	// Load the data of the map from XML
	private void readXMLMap(XmlNode mapNode){
		foreach(XmlNode lineNode in mapNode.ChildNodes){
			List<int> line = new List<int>();
			foreach(XmlNode cellNode in lineNode.ChildNodes){
				line.Add(int.Parse(cellNode.Attributes.GetNamedItem("value").Value));
			}
			map.Add(line);
		}
	}

	private void readXMLLimits(XmlNode limitsNode){
		string actionName = null;
		foreach (XmlNode limitNode in limitsNode.ChildNodes)
		{
			actionName = limitNode.Attributes.GetNamedItem("blockType").Value;
			// check if a GameObject exists with the same name
			if (getLibraryItemByName(actionName) && !gameData.actionBlockLimit.ContainsKey(actionName)){
				gameData.actionBlockLimit[actionName] = int.Parse(limitNode.Attributes.GetNamedItem("limit").Value);
			}
		}
	}

	private void readXMLConsole(XmlNode activableNode){
		List<int> slotsID = new List<int>();

		foreach(XmlNode child in activableNode.ChildNodes){
			slotsID.Add(int.Parse(child.Attributes.GetNamedItem("slotId").Value));
		}

		createConsole(int.Parse(activableNode.Attributes.GetNamedItem("state").Value), int.Parse(activableNode.Attributes.GetNamedItem("posX").Value), int.Parse(activableNode.Attributes.GetNamedItem("posY").Value),
		 slotsID, (Direction.Dir)int.Parse(activableNode.Attributes.GetNamedItem("direction").Value));
	}

	// Lit le XML d'un script est g�n�re les game objects des instructions
	private void readXMLScript(XmlNode scriptNode, string name, UIRootContainer.EditMode editMode, UIRootContainer.SolutionType type)
	{
		if(scriptNode != null){

			// Look for another script with the same name. If one already exists, we don't create one more.
			if (!scriptNameUsed.Contains(name))
            {
				// Rechercher un drone associ� � ce script
				bool droneFound = false;
				foreach (GameObject drone in f_drone)
				{
					ScriptRef scriptRef = drone.GetComponent<ScriptRef>();
					if (scriptRef.executablePanel.transform.Find("Header").Find("agentName").GetComponent<TMP_InputField>().text == name)
					{
						List<GameObject> script = new List<GameObject>();
						foreach (XmlNode actionNode in scriptNode.ChildNodes)
							script.Add(readXMLInstruction(actionNode));
						GameObject tmpContainer = GameObject.Instantiate(scriptRef.executableScript);
						foreach (GameObject go in script)
							go.transform.SetParent(tmpContainer.transform, false); //add actions to container
						EditingUtility.fillExecutablePanel(tmpContainer, scriptRef.executableScript, drone.tag);
						// bind all child
						foreach (Transform child in scriptRef.executableScript.transform)
							GameObjectManager.bind(child.gameObject);
						// On fait apparaitre le panneau du robot
						scriptRef.executablePanel.transform.Find("Header").Find("Toggle").GetComponent<Toggle>().isOn = true;
						GameObjectManager.setGameObjectState(scriptRef.executablePanel, true);
						GameObject.Destroy(tmpContainer);
						droneFound = true;
					}
				}
				if (!droneFound)
				{
					List<GameObject> script = new List<GameObject>();
					foreach (XmlNode actionNode in scriptNode.ChildNodes)
						script.Add(readXMLInstruction(actionNode));
					GameObjectManager.addComponent<AddSpecificContainer>(MainLoop.instance.gameObject, new { title = name, editState = editMode, typeState = type, script = script });
				}
			}
			else
            {
				Debug.LogWarning("Script \"" + name + "\" not created because another one already exists. Only one script with the same name is possible.");
            }		
		}
	}

	private GameObject getLibraryItemByName(string itemName)
    {
		foreach (GameObject item in f_draggableElement)
			if (item.name == itemName)
				return item;
		return null;
	}

	// Transforme le noeud d'action XML en gameObject
	private GameObject readXMLInstruction(XmlNode actionNode){
		GameObject obj = null;
		Transform conditionContainer = null;
		Transform firstContainerBloc = null;
		Transform secondContainerBloc = null;
        switch (actionNode.Name)
        {
			case "if":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName("IfThen"), canvas);

				conditionContainer = obj.transform.Find("ConditionContainer");
				firstContainerBloc = obj.transform.Find("Container");

				// On ajoute les �l�ments enfants dans les bons containers
				foreach (XmlNode containerNode in actionNode.ChildNodes)
				{
					// Ajout des conditions
					if (containerNode.Name == "condition")
					{
						if (containerNode.HasChildNodes)
						{
							// The first child of the conditional container of a If action contains the ReplacementSlot
							GameObject emptyZone = conditionContainer.GetChild(0).gameObject;
							// Parse xml condition
							GameObject child = readXMLCondition(containerNode.FirstChild);
							// Add child to empty zone
							EditingUtility.addItemOnDropArea(child, emptyZone);
						}
					}
					else if (containerNode.Name == "container")
					{
						if (containerNode.HasChildNodes)
							processXMLInstruction(firstContainerBloc, containerNode);
					}
				}
				break;

			case "ifElse":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName("IfElse"), canvas);
				conditionContainer = obj.transform.Find("ConditionContainer");
				firstContainerBloc = obj.transform.Find("Container");
				secondContainerBloc = obj.transform.Find("ElseContainer");

				// On ajoute les �l�ments enfants dans les bons containers
				foreach (XmlNode containerNode in actionNode.ChildNodes)
				{
					// Ajout des conditions
					if (containerNode.Name == "condition")
					{
						if (containerNode.HasChildNodes)
						{
							// The first child of the conditional container of a IfElse action contains the ReplacementSlot
							GameObject emptyZone = conditionContainer.GetChild(0).gameObject;
							// Parse xml condition
							GameObject child = readXMLCondition(containerNode.FirstChild);
							// Add child to empty zone
							EditingUtility.addItemOnDropArea(child, emptyZone);
						}
					}
					else if (containerNode.Name == "thenContainer")
					{
						if (containerNode.HasChildNodes)
							processXMLInstruction(firstContainerBloc, containerNode);
					}
					else if (containerNode.Name == "elseContainer")
					{
						if (containerNode.HasChildNodes)
							processXMLInstruction(secondContainerBloc, containerNode);
					}
				}
				break;

			case "for":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName("ForLoop"), canvas);
				firstContainerBloc = obj.transform.Find("Container");
				BaseElement action = obj.GetComponent<ForControl>();

				((ForControl)action).nbFor = int.Parse(actionNode.Attributes.GetNamedItem("nbFor").Value);
				obj.transform.GetComponentInChildren<TMP_InputField>().text = ((ForControl)action).nbFor.ToString();

				if (actionNode.HasChildNodes)
					processXMLInstruction(firstContainerBloc, actionNode);
				break;

			case "while":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName("While"), canvas);
				firstContainerBloc = obj.transform.Find("Container");
				conditionContainer = obj.transform.Find("ConditionContainer");

				// On ajoute les �l�ments enfants dans les bons containers
				foreach (XmlNode containerNode in actionNode.ChildNodes)
				{
					// Ajout des conditions
					if (containerNode.Name == "condition")
					{
						if (containerNode.HasChildNodes)
						{
							// The first child of the conditional container of a While action contains the ReplacementSlot
							GameObject emptyZone = conditionContainer.GetChild(0).gameObject;
							// Parse xml condition
							GameObject child = readXMLCondition(containerNode.FirstChild);
							// Add child to empty zone
							EditingUtility.addItemOnDropArea(child, emptyZone);
						}
					}
					else if (containerNode.Name == "container")
					{
						if (containerNode.HasChildNodes)
							processXMLInstruction(firstContainerBloc, containerNode);
					}
				}
				break;

			case "forever":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName("Forever"), canvas);
				firstContainerBloc = obj.transform.Find("Container");

				if (actionNode.HasChildNodes)
					processXMLInstruction(firstContainerBloc, actionNode);
				break;
			case "action":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName(actionNode.Attributes.GetNamedItem("type").Value), canvas);
				break;
        }

		if (!gameData.dragDropEnabled)
		{
			Color disabledColor = MainLoop.instance.GetComponent<AgentColor>().droneAction;
			obj.GetComponent<Image>().color = disabledColor;
			if (obj.GetComponent<ControlElement>())
				foreach (Transform child in obj.gameObject.transform)
				{
					Image childImg = child.GetComponent<Image>();
					if (child.name != "3DEffect"&& childImg != null)
						childImg.color = disabledColor;
				}
		}

		return obj;
	}

	private void processXMLInstruction(Transform gameContainer, XmlNode xmlContainer)
	{
		// The first child of a control container is an emptySolt
		GameObject emptySlot = gameContainer.GetChild(0).gameObject;
		foreach (XmlNode eleNode in xmlContainer.ChildNodes)
			EditingUtility.addItemOnDropArea(readXMLInstruction(eleNode), emptySlot);
	}

	// Transforme le noeud d'action XML en gameObject �l�ment/op�rator
	private GameObject readXMLCondition(XmlNode conditionNode) {
		GameObject obj = null;
		ReplacementSlot[] slots = null;
		switch (conditionNode.Name)
        {
			case "and":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName("AndOperator"), canvas);
				slots = obj.GetComponentsInChildren<ReplacementSlot>(true);
				if (conditionNode.HasChildNodes)
				{
					GameObject emptyZone = null;
					foreach (XmlNode andNode in conditionNode.ChildNodes)
					{
						if (andNode.Name == "conditionLeft")
							// The Left slot is the second ReplacementSlot (first is the And operator)
							emptyZone = slots[1].gameObject;
						if (andNode.Name == "conditionRight")
							// The Right slot is the third ReplacementSlot
							emptyZone = slots[2].gameObject;
						if (emptyZone != null && andNode.HasChildNodes)
						{
							// Parse xml condition
							GameObject child = readXMLCondition(andNode.FirstChild);
							// Add child to empty zone
							EditingUtility.addItemOnDropArea(child, emptyZone);
						}
						emptyZone = null;
					}
				}
				break;

			case "or":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName("OrOperator"), canvas);
				slots = obj.GetComponentsInChildren<ReplacementSlot>(true);
				if (conditionNode.HasChildNodes)
				{
					GameObject emptyZone = null;
					foreach (XmlNode orNode in conditionNode.ChildNodes)
					{
						if (orNode.Name == "conditionLeft")
							// The Left slot is the second ReplacementSlot (first is the And operator)
							emptyZone = slots[1].gameObject;
						if (orNode.Name == "conditionRight")
							// The Right slot is the third ReplacementSlot
							emptyZone = slots[2].gameObject;
						if (emptyZone != null && orNode.HasChildNodes)
						{
							// Parse xml condition
							GameObject child = readXMLCondition(orNode.FirstChild);
							// Add child to empty zone
							EditingUtility.addItemOnDropArea(child, emptyZone);
						}
						emptyZone = null;
					}
				}
				break;

			case "not":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName("NotOperator"), canvas);
				if (conditionNode.HasChildNodes)
				{
					GameObject emptyZone = obj.transform.Find("Container").GetChild(1).gameObject;
					GameObject child = readXMLCondition(conditionNode.FirstChild);
					// Add child to empty zone
					EditingUtility.addItemOnDropArea(child, emptyZone);
				}
				break;
			case "captor":
				obj = EditingUtility.createEditableBlockFromLibrary(getLibraryItemByName(conditionNode.Attributes.GetNamedItem("type").Value), canvas);
				break;
		}

		if (!gameData.dragDropEnabled)
		{
			Color disabledColor = MainLoop.instance.GetComponent<AgentColor>().droneAction;
			obj.GetComponent<Image>().color = disabledColor;
			if (obj.GetComponent<BaseOperator>())
				foreach (Transform child in obj.gameObject.transform)
				{
					Image childImg = child.GetComponent<Image>();
					if (child.name != "3DEffect" && childImg != null)
						childImg.color = disabledColor;
				}
		}

			return obj;
	}
}
