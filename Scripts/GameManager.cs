using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

//Script for controlling levels, scores, etc.
//Has the UI as a child object

public class GameManager : MonoBehaviour
{

    //An instance of the game manager that can be invoked. Should only be one instance at a time
    #region Singleton
    public static GameManager instance;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //Calls a utility method that selects the level for a given playthrough. Store these in an array of scenes.
            spawnedScenes = LevelSpawner.SpawnScenes();
            sceneIterator = 0;
            SceneManager.LoadScene(spawnedScenes[sceneIterator]);
        }
        //If instance already exists and it's not this:
        else if (instance != this)
        {
            //Then destroy this. This enforces our singleton pattern, meaning there can only ever be one instance of a GameManager.
            Destroy(gameObject);
        }

        //Sets this to not be destroyed when reloading scene
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    //Used for tracking what scenes are loaded
    //spawnedScenes are the scene's pathnames
    private string[] spawnedScenes;
    private int sceneIterator;

    //Action declarations for callbacks
    private UnityAction submitAction;
    private UnityAction playAgainAction;
    private UnityAction returnAction;
    public UnityAction<GameObject> bugClicked;
    public UnityAction returnZoom;
    public UnityAction<string> bugIdentified;

    Button submitButton;
    Button playAgainButton;
    Button returnButton;

    private int playerScore;
    private int totalScore;
    private string selectedBug;


    //Private vars for the zooming effect once a bug has been clicked
    private float defaultFOV;
    private Vector3 defaultCameraPosition;
    private float zoomedFOV;
    private bool zoomingIn;
    private bool zoomingOut;
    private float zoomInSpeed = 3f; //5f
    private float zoomOutSpeed = 5f;
    private bool bugHasBeenCategorized = false;
    private bool measurementGiven = false;

    GameObject gameOver;
    GameObject returnObject;
    GameObject bugSelectionUI;
    GameObject bugButtons;
    Bug currentBugScript;


    // Start is called before the first frame update
    void Start()
    {
        selectedBug = null;

        returnObject = GameObject.Find("Return");
        returnObject.SetActive(false);

        defaultFOV = Camera.main.orthographicSize;
        defaultCameraPosition = Camera.main.transform.position;
        zoomedFOV = defaultFOV/4.0f;

        //Finds the submit button from the scene and adds an event listener
        submitButton = GameObject.Find("Submit").GetComponent<Button>();
        submitAction += Submit;
        submitButton.onClick.AddListener(submitAction);

        //Callback function for when a bug has been clicked by the user
        bugClicked += BugClicked;

       //Find the gameover UI
       gameOver = GameObject.Find("GameOver");
       //Make the gameover screen invisible
       gameOver.SetActive(false);

      bugButtons = GameObject.Find("BugButtons");

       //Hide the bug selection UI at startup
       bugSelectionUI = GameObject.Find("BugSelectionUI");
       bugSelectionUI.SetActive(false);

    }

    // Update is called once per frame
    void Update()
    {
        if(TimerScript.GetCurrentTime() <= 0)
        {
            Submit();
        }

        //Might want to make these into coroutines to delay the zoom a bit
        //Linearly interpolates between the default camera view and the zoomed view. Updates each frame for a smoother zoom effect
        if (zoomingIn)
        {
            Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, zoomedFOV, Time.deltaTime * zoomInSpeed);
            if (Camera.main.orthographicSize <= zoomedFOV)
            {
                zoomingIn = false;
            }
        }

        if (zoomingOut)
        {
            Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, defaultFOV, Time.deltaTime * zoomOutSpeed);
            if (Camera.main.orthographicSize >= defaultFOV)
            {
                zoomingOut = false;
                Camera.main.orthographicSize = defaultFOV;
            }
        }
    }

    //Public method called by timer once it hits 0
    public static void TimerSubmit() => GameManager.instance.Submit();

    //Public method for a bug to call once it has been clicked
    public void BugClicked(GameObject bug)
    {
        //Zooms camera in on bug
        Camera.main.orthographic = true;
        Camera.main.transform.position = Camera.main.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y - Mathf.Floor(Screen.height/12), Input.mousePosition.z));
        zoomingIn = true;

        bugSelectionUI.SetActive(true);
        submitButton.gameObject.SetActive(false);

        //Hide the ruler when bug has been clicked
        GameObject ruler = GameObject.Find("Ruler");
        Image rulerImage = ruler.GetComponent<Image>();
        var tempColor = rulerImage.color;
        tempColor.a = 0f;
        rulerImage.color = tempColor;

        //returnObject.SetActive(true);
        //Currently disabled
        returnButton = returnObject.GetComponent<Button>();
        returnAction += ReturnFromClick;
        returnButton.onClick.AddListener(returnAction);

        InputField measurementInput = bugSelectionUI.GetComponentInChildren<InputField>();
        measurementInput.onEndEdit.AddListener(delegate {EvaluateMeasurement(measurementInput); });

        Button bugUISubmitButton = bugSelectionUI.GetComponentInChildren<Button>();
        bugUISubmitButton.onClick.AddListener(delegate {BugUISubmit(); });

        Utilities.PauseBugs();
        TimerScript.PauseTime();
        MagnifyGlass.DisableZoom();

        currentBugScript = bug.GetComponent<Bug>();
        selectedBug = currentBugScript.classification;
    }

    //Checks if user correctly identified the highlighted bug. Displays the result as a text popup
    public void BugSelectionUI(string bugName)
    {

        if (selectedBug != null && currentBugScript != null)
        {
            if (selectedBug == bugName)
            {
                ScoreScript.AddScore(currentBugScript.points);
                currentBugScript.SetCorrectColor();
                StartCoroutine(Utilities.PopupMessage("Correct!", 1));
            }
            else
            {
                currentBugScript.SetIncorrectColor();
                StartCoroutine(Utilities.PopupMessage("Incorrect", 1));
            }
        }
        bugHasBeenCategorized = true;
        bugButtons.SetActive(false);

    }

    //For when the user is done with a branch, also called when timer runs out
    void Submit()
    {
        //Iterate to get the next scene
        sceneIterator++;
        //Score is persistant between levels for now, but might want to change this
        totalScore += calcTotalScore();

        TimerScript.SetCurrentTime(80);
        selectedBug = null;

        //If we're on the last level, display the game over screen. Otherwise go to next level
        if (sceneIterator == spawnedScenes.Length)
        {
            playerScore = ScoreScript.scoreValue;

            //Hide the game interface
            GameObject mainInterface = GameObject.Find("LevelUI");
            mainInterface.SetActive(false);

            //Make the gameover screen visible
            gameOver.SetActive(true);

            //Update the score value and display it to the game over screen
            Text scoreText = GameObject.Find("YourScore").GetComponent<Text>();
            scoreText.text += playerScore.ToString();

            Text totalScoreText = GameObject.Find("TotalScore").GetComponent<Text>();
            totalScoreText.text += totalScore.ToString() + " possible points";

            //Finds the play again button from the scene and adds an event listener
            playAgainButton = GetComponentInChildren<Button>();
            playAgainAction += PlayAgain;
            playAgainButton.onClick.AddListener(playAgainAction);
        }
        else
        {
            SceneManager.LoadScene(spawnedScenes[sceneIterator]);
        }


    }

    void PlayAgain()
    {
        //Resets the score and goes back to the first scene.
        //Creates new instance of the game manager. Levels should be random again on replay
        ScoreScript.scoreValue = 0;
        Destroy(gameObject);
        SceneManager.LoadScene(spawnedScenes[0]);
    }

    //Can be used for return button as well as after submitting a bug
    private void ReturnFromClick()
    {
        //Reset camera
        //Camera.main.orthographicSize = defaultFOV;
        zoomingIn = false;
        zoomingOut = true;
        Camera.main.transform.position = defaultCameraPosition;

        //Hide bug selection screen and bring back normal UI
        bugSelectionUI.GetComponentInChildren<InputField>().ActivateInputField();
        bugButtons.SetActive(true);
        bugSelectionUI.SetActive(false);
        submitButton.gameObject.SetActive(true);
        returnObject.SetActive(false);
        TimerScript.ResumeTime();
        Utilities.ResumeBugs();
        MagnifyGlass.EnableZoom();
        MagnifyGlass.ResetCounter();
        currentBugScript = null;

        bugHasBeenCategorized = false;
        measurementGiven = false;

        GameObject ruler = GameObject.Find("Ruler");
        Image rulerImage = ruler.GetComponent<Image>();
        var tempColor = rulerImage.color;
        tempColor.a = 171/255f;
        rulerImage.color = tempColor;
    }

    //Helper method that iterates through all the bugs on the screen and calculates their potential score value
    private int calcTotalScore()
    {
        int tempScore = 0;
        Bug[] bugs = GameObject.FindObjectsOfType<Bug>();
        foreach (Bug bug in bugs)
        {
            tempScore += bug.points;
        }
        //Currently doubles the score, as correctly identifying a bug currently counts as 10 points.
        //This will need to be adjusted eventually
        return 3*tempScore;
    }

    private void EvaluateMeasurement(InputField input){
        float approximatedBugLength = float.Parse(input.text);
        float actualBugLength = currentBugScript.lengthInMM;
        float minBound = 0;
        float maxBound = actualBugLength * 2;
        int scoreValue = 0;

        if(approximatedBugLength >= maxBound){
          //Do nothing
        } else if (approximatedBugLength >= actualBugLength){

          float distance = maxBound - approximatedBugLength;
          float accuracyPercent = distance/actualBugLength;
          scoreValue = (int)Mathf.Round(accuracyPercent * (float)currentBugScript.points);

        } else if (approximatedBugLength > minBound){

          float distance = approximatedBugLength;
          float accuracyPercent = distance/actualBugLength;
          scoreValue = (int)Mathf.Round(accuracyPercent * (float)currentBugScript.points);
        }

        measurementGiven = true;
        ScoreScript.AddScore(scoreValue);
        input.text = "Length";
        input.DeactivateInputField();
    }

    private void BugUISubmit(){
      if(measurementGiven && bugHasBeenCategorized){
        ReturnFromClick();
      } else {
        //Might want to send an alert to the user eventually
        //StartCoroutine(Utilities.PopupMessage("Must select a bug type and measurement", 2));
      }
    }

}
