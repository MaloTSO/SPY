﻿using UnityEngine;
using FYFY;
using System.Collections.Generic;
using UnityEngine.UI;
using System.IO;
using TMPro;
using System.Xml;
using System.Collections;
using UnityEngine.Networking;
using System;
using UnityEngine.Events;
using System.Runtime.InteropServices;

/// <summary>
/// Manage main menu to launch a specific mission
/// </summary>
public class TitleScreenSystem : FSystem {
	private GameData gameData;
	public GameData prefabGameData;
	public GameObject mainCanvas;
	public GameObject campagneMenu;
	public GameObject compLevelButton;
	public GameObject listOfCampaigns;
	public GameObject listOfLevels;
	public GameObject loadingScenarioContent;
	public GameObject scenarioContent;
	public GameObject quitButton;
	public GameObject loadingScreen; 

	private GameObject selectedScenario;
	private Dictionary<string, List<DataLevel>> defaultCampaigns; // List of levels for each default campaign
	private UnityAction localCallback;

	private string loadLevelWithURL = "";
	private int webGL_levelLoaded = 0;
	private int webGL_levelToLoad = 0;
	private bool webGL_askToEnableSendSystem = false;

	[DllImport("__Internal")]
	private static extern void ShowHtmlButtons(); // call javascript

	[Serializable]
	public class WebGlDataLevels
    {
		public string scenarioName;
		public List<DataLevel> levels;
    }

	[Serializable]
	public class WebGlScenarioList
    {
		public List<WebGlDataLevels> scenarios;
    }


	// L'instance
	public static TitleScreenSystem instance;

	public TitleScreenSystem()
	{
		instance = this;
	}

	protected override void onStart()
	{
		if (!GameObject.Find("GameData"))
		{
			gameData = UnityEngine.Object.Instantiate(prefabGameData);
			gameData.name = "GameData";
			if (webGL_askToEnableSendSystem)
				gameData.disableSendStatement = false;
			GameObjectManager.dontDestroyOnLoadAndRebind(gameData.gameObject);
		}
		else
		{
			gameData = GameObject.Find("GameData").GetComponent<GameData>();
		}

		gameData.levels = new Dictionary<string, XmlNode>();
		gameData.scenario = new List<DataLevel>();

		defaultCampaigns = new Dictionary<string, List<DataLevel>>();
		selectedScenario = null;

		GameObjectManager.setGameObjectState(campagneMenu, false);

		MainLoop.instance.StartCoroutine(GetScenariosAndLevels());
	}

	private IEnumerator GetScenariosAndLevels()
	{
		// Enable Loading screen
		GameObjectManager.setGameObjectState(loadingScreen, true);
		if (Application.platform == RuntimePlatform.WebGLPlayer)
		{
			ShowHtmlButtons();
			GameObjectManager.setGameObjectState(quitButton, false);
			// Load scenario and levels from server
			GetScenarioWebRequest();
			GetLevelsWebRequest();
		}
		else
		{
			// Load scenario and levels from disk
			// explore streaming asstets path
			MainLoop.instance.StartCoroutine(exploreLevelsAndScenarios(Application.streamingAssetsPath));
			// explore persistent data path
			MainLoop.instance.StartCoroutine(exploreLevelsAndScenarios(Application.persistentDataPath));
		}

		yield return new WaitForSeconds(0.5f);

		// wait level loading
		MainLoop.instance.StartCoroutine(WaitLoadingScenariosAndLevels());
	}

	private IEnumerator WaitLoadingScenariosAndLevels()
    {
		while (webGL_levelLoaded < webGL_levelToLoad)
			yield return null;
		// Now, if require, we can load requested level by URL
		if (loadLevelWithURL != "")
			testLevelPath(loadLevelWithURL);
		// Disable Loading screen
		GameObjectManager.setGameObjectState(loadingScreen, false);
	}

	private IEnumerator GetScenarioWebRequest()
	{
		UnityWebRequest www = UnityWebRequest.Get(new Uri(Application.streamingAssetsPath + "/WebGlData/ScenarioList.json").AbsoluteUri);
		yield return www.SendWebRequest();

		if (www.result != UnityWebRequest.Result.Success)
		{
			localCallback = null;
			GameObjectManager.addComponent<MessageForUser>(MainLoop.instance.gameObject, new { message = "Erreur lors de l'accès au document " + Application.streamingAssetsPath + "/WebGlData/ScenarioList.json : " + www.error, OkButton = "", CancelButton = "OK", call = localCallback });
		}
		else
		{
			string scenarioJson = www.downloadHandler.text;

			WebGlScenarioList scenarioListRaw = JsonUtility.FromJson<WebGlScenarioList>(scenarioJson);
			// try to load all scenarios
			foreach (WebGlDataLevels scenarioRaw in scenarioListRaw.scenarios)
			{
				defaultCampaigns[scenarioRaw.scenarioName] = new List<DataLevel>();
				foreach (DataLevel levelPath in scenarioRaw.levels)
				{
					levelPath.src = new Uri(Application.streamingAssetsPath + "/" + levelPath.src).AbsoluteUri;
					defaultCampaigns[scenarioRaw.scenarioName].Add(levelPath);
				}
			}
		}
	}

	private IEnumerator GetLevelsWebRequest()
	{
		UnityWebRequest www = UnityWebRequest.Get(new Uri(Application.streamingAssetsPath + "/WebGlData/LevelsList.json").AbsoluteUri);
		yield return www.SendWebRequest();

		if (www.result != UnityWebRequest.Result.Success)
		{
			localCallback = null;
			GameObjectManager.addComponent<MessageForUser>(MainLoop.instance.gameObject, new { message = "Erreur lors de l'accès au document " + Application.streamingAssetsPath + "/WebGlData/LevelsList.json : " + www.error, OkButton = "", CancelButton = "OK", call = localCallback });
		}
		else
		{
			string levelsJson = www.downloadHandler.text;
			WebGlDataLevels levelsListRaw = JsonUtility.FromJson<WebGlDataLevels>(levelsJson);
			// try to load all levels
			foreach (DataLevel levelRaw in levelsListRaw.levels)
				MainLoop.instance.StartCoroutine(GetLevelOrScenario_WebRequest(new Uri(Application.streamingAssetsPath + "/" + levelRaw.src).AbsoluteUri));
			webGL_levelToLoad = levelsListRaw.levels.Count;
		}
	}

	private IEnumerator exploreLevelsAndScenarios(string path)
	{
		// try to load all child files
		string[] files = Directory.GetFiles(path);
		webGL_levelToLoad += files.Length;
		foreach (string fileName in files)
		{
			yield return null;
			MainLoop.instance.StartCoroutine(GetLevelOrScenario_WebRequest(fileName));
		}

		// explore subdirectories
		foreach (string directory in Directory.GetDirectories(path))
		{
			yield return null;
			MainLoop.instance.StartCoroutine(exploreLevelsAndScenarios(directory));
		}
	}

	private IEnumerator GetLevelOrScenario_WebRequest(string uri)
	{
		UnityWebRequest www = UnityWebRequest.Get(uri);
		yield return www.SendWebRequest();

		webGL_levelLoaded++;
		if (www.result != UnityWebRequest.Result.Success)
		{
			localCallback = null;
			GameObjectManager.addComponent<MessageForUser>(MainLoop.instance.gameObject, new { message = "Erreur lors de l'accès au document " + uri + " : " + www.error, OkButton = "", CancelButton = "OK", call = localCallback });
		}
		else
		{
			string xmlContent = www.downloadHandler.text;
			LoadLevelOrScenario(uri, xmlContent);
		}
	}

	private void LoadLevelOrScenario(string uri, string xmlContent)
    {
		XmlDocument doc = new XmlDocument();
		try
		{
			doc.LoadXml(xmlContent);
			EditingUtility.removeComments(doc);
			// a valid level must have only one tag "level" and no tag "scenario"
			if (doc.GetElementsByTagName("level").Count == 1 && doc.GetElementsByTagName("scenario").Count == 0)
				gameData.levels.Add(new Uri(uri).AbsoluteUri, doc.GetElementsByTagName("level")[0]);
			// a valid scenario must have only one tag "scenario"
			if (doc.GetElementsByTagName("scenario").Count == 1)
				updateScenarioContent(uri, doc);
		}
		catch { }
	}

	public void updateScenarioContent(string uri, XmlDocument doc)
	{
		List<DataLevel> levelList = new List<DataLevel>();
		foreach (XmlNode child in doc.GetElementsByTagName("scenario")[0])
			if (child.Name.Equals("level"))
			{
				DataLevel dl = new DataLevel();
				// get src
				dl.src = new Uri(Application.streamingAssetsPath + "/" + (child.Attributes.GetNamedItem("src").Value)).AbsoluteUri;

				// get name
				if (child.Attributes.GetNamedItem("name") != null)
					dl.name = child.Attributes.GetNamedItem("name").Value;
				else
					// if not def, use file name
					dl.name = Path.GetFileNameWithoutExtension(dl.src);

				levelList.Add(dl);
			}
		defaultCampaigns[Path.GetFileName(uri)] = levelList;
	}

	private class JavaScriptData
	{
		public string name;
		public string content;
	}

	// Fonction appelée depuis le javascript (voir Assets/WebGLTemplates/Custom/index.html) via le Wrapper du Système
	public void importScenario(string content)
	{
		JavaScriptData jsd = JsonUtility.FromJson<JavaScriptData>(content);
		try
		{
			LoadLevelOrScenario(jsd.name, jsd.content);
			localCallback = null;
			GameObjectManager.addComponent<MessageForUser>(MainLoop.instance.gameObject, new { message = "Scénario chargé", OkButton = "", CancelButton = "OK", call = localCallback });
		}
		catch (Exception e) {
			localCallback = null;
			GameObjectManager.addComponent<MessageForUser>(MainLoop.instance.gameObject, new { message = "Erreur lors du chargement du scénario : "+e.Message, OkButton = "", CancelButton = "OK", call = localCallback });
		}
	}

	// see Play Button in TitleScreen scene
	public void displayScenarioList()
	{
		// remove all old scenario
		foreach (Transform child in listOfCampaigns.transform)
		{
			GameObjectManager.unbind(child.gameObject);
			GameObject.Destroy(child.gameObject);
		}

		//create scenarios' button
		List<string> sortedScenarios = new List<string>();
		foreach (string key in defaultCampaigns.Keys)
			sortedScenarios.Add(key);
		sortedScenarios.Sort();
		foreach (string key in sortedScenarios)
		{
			GameObject directoryButton = GameObject.Instantiate<GameObject>(Resources.Load("Prefabs/Button") as GameObject, listOfCampaigns.transform);
			directoryButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = Path.GetFileNameWithoutExtension(key);
			GameObjectManager.bind(directoryButton);
			// add on click
			directoryButton.GetComponent<Button>().onClick.AddListener(delegate { showLevels(key); });
		}
	}

	private void showLevels(string campaignKey) {
		GameObjectManager.setGameObjectState(mainCanvas.transform.Find("SPYMenu").Find("MenuCampaigns").gameObject, false);
		GameObjectManager.setGameObjectState(mainCanvas.transform.Find("SPYMenu").Find("MenuLevels").gameObject, true);
		// delete all old level buttons
		foreach (Transform child in listOfLevels.transform)
        {
			GameObjectManager.unbind(child.gameObject);
			GameObject.Destroy(child.gameObject);
        }

		// create level buttons for this campaign
		for (int i = 0; i < defaultCampaigns[campaignKey].Count; i++)
		{
			DataLevel levelData = defaultCampaigns[campaignKey][i];
			GameObject button = GameObject.Instantiate<GameObject>(Resources.Load("Prefabs/LevelButton") as GameObject, listOfLevels.transform);
			button.transform.Find("Button").GetChild(0).GetComponent<TextMeshProUGUI>().text = levelData.name;
			button.transform.Find("Button").GetComponent<Button>().onClick.AddListener(delegate { launchLevel(campaignKey, levelData); });
			GameObjectManager.bind(button);
			//locked levels
			if (i <= PlayerPrefs.GetInt(campaignKey, 0)) //by default first level of directory is the only unlocked level of directory
				button.GetComponentInChildren<Button>().interactable = true;
			//unlocked levels
			else
				button.GetComponentInChildren<Button>().interactable = false;
			//scores
			int scoredStars = PlayerPrefs.GetInt(levelData + gameData.scoreKey, 0); //0 star by default
			Transform scoreCanvas = button.transform.Find("ScoreCanvas");
			for (int nbStar = 0; nbStar < 4; nbStar++)
			{
				if (nbStar == scoredStars)
					GameObjectManager.setGameObjectState(scoreCanvas.GetChild(nbStar).gameObject, true);
				else
					GameObjectManager.setGameObjectState(scoreCanvas.GetChild(nbStar).gameObject, false);
			}
		}
	}

	public void launchLevel(string campaignKey, DataLevel levelToLoad) {
		gameData.scenarioName = campaignKey;
		gameData.levelToLoad = levelToLoad;
		gameData.scenario = defaultCampaigns[campaignKey];
		GameObjectManager.loadScene("MainScene");
	}

	//Used on scenario editing window (see button ButtonTestLevel)
	public void testLevel(TMP_Text levelToLoad)
	{
		testLevelPath(levelToLoad.text);
	}
	private void testLevelPath(string levelToLoad)
	{
		gameData.scenarioName = "testLevel";
		gameData.scenario = new List<DataLevel>();
		DataLevel testLevel = new DataLevel();
		testLevel.src = new Uri(Application.streamingAssetsPath + "/" + levelToLoad).AbsoluteUri;
		testLevel.name = Path.GetFileNameWithoutExtension(testLevel.src);
		gameData.levelToLoad = testLevel;
		gameData.scenario.Add(testLevel);
		GameObjectManager.loadScene("MainScene");
	}

	// Fonction appelée depuis le javascript (voir Assets/WebGLTemplates/Custom/index.html) via le Wrapper du Système
	public void askToLoadLevel(string levelToLoad)
    {
		loadLevelWithURL = levelToLoad;
	}

	// Fonction appelée depuis le javascript (voir Assets/WebGLTemplates/Custom/index.html) via le Wrapper du Système
	public void enableSendStatement()
	{
		if (gameData == null)
			webGL_askToEnableSendSystem = true;
		else
			gameData.disableSendStatement = false;
	}

	// See Quitter button in editor
	public void quitGame(){
		Application.Quit();
	}

	// See ButtonLoadScenario
	public void displayLoadingPanel()
	{
		selectedScenario = null;
		GameObjectManager.setGameObjectState(mainCanvas.transform.Find("LoadingPanel").gameObject, true);
		// remove all old scenario
		foreach (Transform child in loadingScenarioContent.transform)
		{
			GameObjectManager.unbind(child.gameObject);
			GameObject.Destroy(child.gameObject);
		}

		//create level directory buttons
		foreach (string key in defaultCampaigns.Keys)
		{
			GameObject scenarioItem = GameObject.Instantiate<GameObject>(Resources.Load("Prefabs/ScenarioAvailable") as GameObject, loadingScenarioContent.transform);
			scenarioItem.GetComponent<TextMeshProUGUI>().text = key;
			GameObjectManager.bind(scenarioItem);
		}
	}

	public void onScenarioSelected(GameObject go)
    {
		selectedScenario = go;
    }

	// see LoadButton in LoadingPanel in TitleScreen scene
	public void loadScenario()
	{
		if (selectedScenario != null && defaultCampaigns.ContainsKey(selectedScenario.GetComponentInChildren<TMP_Text>().text))
		{
			//remove all old scenario
			foreach (Transform child in scenarioContent.transform)
            {
				GameObjectManager.unbind(child.gameObject);
				GameObject.Destroy(child.gameObject);
            }

			foreach (DataLevel levelPath in defaultCampaigns[selectedScenario.GetComponentInChildren<TMP_Text>().text])
			{
				GameObject newLevel = GameObject.Instantiate(Resources.Load("Prefabs/deletableElement") as GameObject, scenarioContent.transform);
				newLevel.GetComponentInChildren<TMP_Text>().text = levelPath.src.Replace(new Uri(Application.streamingAssetsPath + "/").AbsoluteUri, "");
				LayoutRebuilder.ForceRebuildLayoutImmediate(newLevel.transform as RectTransform);
				GameObjectManager.bind(newLevel);
			}
			LayoutRebuilder.ForceRebuildLayoutImmediate(scenarioContent.transform as RectTransform);
		}
    }
}