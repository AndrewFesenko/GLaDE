using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace GLaDE.UI{
	public class UIMenuManager : MonoBehaviour {
		public enum Theme { 
			custom1, 
			custom2, 
			custom3 
		};
		
		[Header("THEME SETTINGS")]
		public Theme theme;
		int themeIndex;
		public ThemedUIData themeController;

        public List<GameObject> screens = new List<GameObject>();
        [HideInInspector]
		public GameObject MainMenuRoot;

		[Header("WELCOME SCREEN")]
		[Tooltip("If this is true, at the start of the scene, the welcome screen will load before the main menu, prompting users")]
		public bool useWelcomeScreen = true;
		[HideInInspector] public GameObject splashScreen;
		float splashScreenTimer = 1;
		bool closingSplashScreen = false;

		[Header("SETTINGS SCREEN")]
		[Tooltip("The dropdown menu containing all the resolutions that your game can adapt to")]
		public TMP_Dropdown ResolutionDropDown;
		private Resolution[] resolutions;
		[Tooltip("The text object in the Settings Panel displaying the current quality setting enabled")]
		private string[] qualityNames;
		[Tooltip("The volume slider UI element in the Settings Screen")]
		public Slider audioSlider;
		public Button[] qualityButtons;

		[Header("LOADING SCREEN")]
		[Tooltip("If this is true, the loaded scene won't load until receiving user input")]
		public bool waitForInput = true;
		public TMP_Text loadPromptText;
		[Tooltip("The loading bar Slider UI element in the Loading Screen")]
		public Slider loadingBar;
		public KeyCode userPromptKey;

		void Update(){
			if(closingSplashScreen){
				if(splashScreenTimer > 0){
					splashScreenTimer -= Time.deltaTime;
				}else{
					screens[0].SetActive(true);
					closingSplashScreen = false;
					splashScreenTimer = 1;
				}
			} 
		}

        void Awake()
        {
            screens.Clear();

            MainMenuRoot = transform.Find("MainMenu").gameObject;
            for (int i = 0; i < MainMenuRoot.transform.childCount; i++)
            {
                Transform child = MainMenuRoot.transform.GetChild(i);

                if (child.gameObject.GetComponent<CanvasGroup>())
                {
                    screens.Add(child.gameObject);
                }
            }
        }

        void Start(){
			// Splash screen or main menu?
			if(!splashScreen && useWelcomeScreen) splashScreen = transform.Find("SplashScreen").gameObject;

			ClosePanels();
            if (!useWelcomeScreen)
            {
                if (splashScreen) splashScreen.SetActive(false);

                if (screens.Count > 0)
                {
                    screens[0].SetActive(true);
                }
            }
            else
            {
				splashScreen.SetActive(true);
				gameObject.SetActive(true);
            }

			// Initialize Settings
			qualityNames = QualitySettings.names; // Get quality settings names
			resolutions = Screen.resolutions; // Get screens possible resolutions

			// Set Drop Down resolution options according to possible screen resolutions of your monitor
			if(ResolutionDropDown){
				for (int i = 0; i < resolutions.Length; i++){
					ResolutionDropDown.options.Add(new TMP_Dropdown.OptionData(ResToString(resolutions[i])));
					ResolutionDropDown.value = i;
					ResolutionDropDown.onValueChanged.AddListener(delegate
					{
						Screen.SetResolution(resolutions
						[ResolutionDropDown.value].width, resolutions[ResolutionDropDown.value].height, true);
					});

				}
			}

			// Check if first time so the volume can be set to MAX
			if (PlayerPrefs.GetInt("firsttime") == 0){
				// it's the player's first time. Set to false now...
				PlayerPrefs.SetInt("firsttime", 1);
				PlayerPrefs.SetFloat("volume", 1);
				PlayerPrefs.SetInt("quality", 0); // set qualitys ettings to low by default
				Screen.fullScreenMode = FullScreenMode.Windowed;
			}
			Screen.fullScreenMode = FullScreenMode.Windowed; // set to windows by default for now

			// Check volume that was saved from last play
			if(audioSlider) audioSlider.value = PlayerPrefs.GetFloat("volume");
			CheckSettings();

			SetThemeColors();
		}

		void SetThemeColors(){
			if (theme == Theme.custom1){
				themeController.currentColor = themeController.custom1.graphic1;
				themeController.textColor = themeController.custom1.text1;
				themeIndex = 0;
			}else if (theme == Theme.custom2){
				themeController.currentColor = themeController.custom2.graphic2;
				themeController.textColor = themeController.custom2.text2;
				themeIndex = 1;
			}else if (theme == Theme.custom3){
				themeController.currentColor = themeController.custom3.graphic3;
				themeController.textColor = themeController.custom3.text3;
				themeIndex = 2;
			}
		}

		// Make sure all the settings panel text are displaying current quality settings properly and updating UI
		public void CheckSettings(){
			int temp = PlayerPrefs.GetInt("quality");
			if(qualityButtons.Length > 0){
				ResetQualityButtonStates();
				if (temp == 0){
					QualityChange(0);
				}else if (temp == 1){
					QualityChange(1);
				}else if (temp == 2){
					QualityChange(2);
				}else if (temp == 3){
					QualityChange(3);
				}
			}
		}

		public void CloseSplashScreen(){
			splashScreen.GetComponent<Animator>().SetBool("Done", true);
			closingSplashScreen = true;
		}

		public void ClosePanels(){
			for (int i = 0; i < screens.Count; i++){
				screens[i].SetActive(false);
			}
		}

		// Converts the resolution into a string form that is then used in the dropdown list as the options
		string ResToString(Resolution res){
			return res.width + " x " + res.height;
		}

		// Whenever a value on the audio slider in the settings panel is changed, this 
		// function is called and updates the overall game volume
		public void AudioSlider(){
			AudioListener.volume = audioSlider.value;
			PlayerPrefs.SetFloat("volume", audioSlider.value);
		}

		// When accepting the QUIT question, the application will close, or if in editor - stop playing
		public void Quit(){
			#if UNITY_EDITOR
				UnityEditor.EditorApplication.isPlaying = false;
			#else
				Application.Quit();
			#endif
		}

		public void QuitToHome(){
			SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
		}

		// Changes the current quality settings by taking the number passed in from the UI 
		// element and matching it to the index of the Quality Settings
		public void QualityChange(int x){
			if(qualityButtons.Length != 0) ResetQualityButtonStates();
			if (x == 0){
				QualitySettings.SetQualityLevel(x, true);
				qualityButtons[0].interactable = false;
				PlayerPrefs.SetInt("quality", 0);
			}
			else if (x == 1){
				QualitySettings.SetQualityLevel(x, true);
				qualityButtons[1].interactable = false;
				PlayerPrefs.SetInt("quality", 1);
			}
			else if (x == 2){
				QualitySettings.SetQualityLevel(x, true);
				qualityButtons[2].interactable = false;
				PlayerPrefs.SetInt("quality", 2);
			}
			if (x == 3){
				QualitySettings.SetQualityLevel(x, true);
				qualityButtons[3].interactable = false;
				PlayerPrefs.SetInt("quality", 3);
			}
		}

		private void ResetQualityButtonStates(){
            for (int i = 0; i < qualityButtons.Length; i++){
				qualityButtons[i].interactable = true;
            }
        }

		public void LoadScene(string sceneName){
			if (sceneName != ""){
				StartCoroutine(LoadAsynchronously(sceneName));
			}
		}

		// Load Bar synching animation
		IEnumerator LoadAsynchronously(string sceneName){ // scene name is just the name of the current scene being loaded
			AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
			operation.allowSceneActivation = false;

			while (!operation.isDone){
				float progress = Mathf.Clamp01(operation.progress / .95f);
				loadingBar.value = progress;

				if (operation.progress >= 0.9f && waitForInput){
					loadPromptText.text = "Press " + userPromptKey.ToString().ToUpper() + " to continue";
					loadingBar.value = 1;

					if (Input.GetKeyDown(userPromptKey)){
						operation.allowSceneActivation = true;
					}
                }else if(operation.progress >= 0.9f && !waitForInput){
					operation.allowSceneActivation = true;
				}

				yield return null;
			}
		}
	}
}