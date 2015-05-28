using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameLogic : MonoBehaviour {
  // Defaults
  public KeyCode quitKey = KeyCode.Escape;
  public Texture fadeInTexture = null;
  public float speedRotationIncrement = 0.05f;
  private OVRPlayerController playerController = null;
  private OVRCameraRig cameraController = null;
  public string layerName = "Default";
  private bool visionMode = true;
  OVRGridCube gridCube = null;
  OVRDebugInfo debugInfo = null;

  // Game Specific
  // Resources
  public GameObject distalReference;

  public TextMesh timeText;
  public TextMesh countdownText;
  public TextMesh scoreText;
  public TextMesh scoreDeltaText;
  public TextMesh scoreMultiplierText;
  public TextMesh customerText;
  public TextMesh chatTimeText;

  public TextMesh topText;
  public TextMesh topLeftText;
  public TextMesh topRightText;
  public TextMesh bottomText;
  public TextMesh bottomLeftText;
  public TextMesh bottomRightText;
  public TextMesh leftText;
  public TextMesh rightText;

  // Constants
  const int GAME_PERIOD = 60;
  const int GAME_COUNTDOWN = 5;
  const float DISTAL_TOLERANCE = 1.0f;
  const float ROOM_RADIUS = 5.0f;
  const float CUSTOMER_SPEED = 5.0f;
  const float SCORE_BASE_MULTIPLIER = 50.0f;

  const int NEUTRAL = -1;
  const int TOP = 0;
  const int BOTTOM = 1;
  const int LEFT = 2;
  const int RIGHT = 3;
  const int TOP_LEFT = 4;
  const int TOP_RIGHT = 5;
  const int BOTTOM_LEFT = 6;
  const int BOTTOM_RIGHT = 7;
  const int CENTER = 8;

  const float FFF = 255f;
  private static Color RED = new Color(237 / FFF, 85 / FFF, 101 / FFF);
  private static Color ORANGE = new Color(230 / FFF, 126 / FFF, 34 / FFF);
  private static Color YELLOW = new Color(255 / FFF, 206 / FFF, 84 / FFF);
  private static Color GREEN = new Color(160 / FFF, 212 / FFF, 104 / FFF);
  private static Color TEAL = new Color(72 / FFF, 207 / FFF, 173 / FFF);
  private static Color BLUE = new Color(79 / FFF, 192 / FFF, 233 / FFF);
  private static Color INDIGO = new Color(93 / FFF, 156 / FFF, 236 / FFF);
  private static Color PURPLE = new Color(155 / FFF, 89 / FFF, 182 / FFF);
  private static Color HIDDEN = new Color(0.0f, 0.0f, 0.0f, 0.0f);

  private static Vector3 CUSTOMER_START_POSITION = new Vector3(0.0f, 0.0f, 5.0f);
  private static Vector3 CUSTOMER_PULSE_SCALE = new Vector3(0.15f, 0.15f);

  private static Vector3 CHAT_TIME_OFFSET = new Vector3(0.5f, -0.25f);

  // Variables
  private bool gameRunning = false;

  private float score = 0;
  private float scoreMultiplier = 1.0f;

  private int chatPeriod = 5;
  private int chatStart = 0;
  private int chatTimeLeft = 0;
  private int timeStart = 0;
  private int timeLeft = 0;
  private float countdownTimeStart = 0.0f;

  private float distalX = 0.0f;
  private float distalY = 0.0f;
  private int distalPosition = 0;

  private Color[] colors;

  private bool customerLocked = true;
  private int customerGoalPosition;

  #if SHOW_DK2_VARIABLES
  private string strVisionMode = "Vision Enabled: ON";
  #endif

  #region MonoBehaviour Message Handlers
  void Awake() {
    OVRCameraRig[] cameraControllers;
    cameraControllers = gameObject.GetComponentsInChildren<OVRCameraRig>();

    if (cameraControllers.Length == 0) {
      Debug.LogWarning("OVRMainMenu: No OVRCameraRig attached.");
    } else if (cameraControllers.Length > 1) {
      Debug.LogWarning("OVRMainMenu: More then 1 OVRCameraRig attached.");
    } else {
      cameraController = cameraControllers [0];
    }       

    OVRPlayerController[] playerControllers;
    playerControllers = gameObject.GetComponentsInChildren<OVRPlayerController>();

    if (playerControllers.Length == 0) {
      Debug.LogWarning("OVRMainMenu: No OVRPlayerController attached.");
    } else if (playerControllers.Length > 1) {
      Debug.LogWarning("OVRMainMenu: More then 1 OVRPlayerController attached.");
    } else {
      playerController = playerControllers [0];
    }
  }

  void Start() {
    if (Application.isEditor == false) {
      Cursor.visible = false; 
      Cursor.lockState = CursorLockMode.Locked;
    }

    if (cameraController != null) {
      gridCube = gameObject.AddComponent<OVRGridCube>();
      gridCube.SetOVRCameraController(ref cameraController);

      debugInfo = gameObject.AddComponent<OVRDebugInfo>();
      if (playerController != null) {
        debugInfo.SetPlayerController(ref playerController);
      }
    }

    gameRunning = true;
    timeLeft = 0;
    
    scoreDeltaText.text = "";
    
    colors = new Color[8]{RED, ORANGE, YELLOW, GREEN, TEAL, BLUE, INDIGO, PURPLE};
    ResetChat();
  }

  void Update() {
    UpdateRecenterPose();

    UpdateVisionMode();

    if (playerController != null) {
      UpdateSpeedAndRotationScaleMultiplier();
    }

    if (Input.GetKeyDown(KeyCode.F11)) {
      Screen.fullScreen = !Screen.fullScreen;
    }

    if (Input.GetKeyDown(KeyCode.M)) {
      OVRManager.display.mirrorMode = !OVRManager.display.mirrorMode;
    }

    if (Input.GetKeyDown(quitKey)) {
      Application.Quit();
    }

    // Main Game Logic
    UpdateGameLogic();
  }
  #endregion

  // Main Game Logic
  void UpdateGameLogic() {
    if (!gameRunning) {
      UpdateCountdown();

      return;
    }

    if (timeLeft <= 0) {
      ListenForReset();

      return;
    }

    UpdateTime();
    UpdateCustomer();
    UpdateChatTime();
    UpdateDistalReference();
    HandleDistalPosition();
    FadeScoreDelta();
    FadeCountdown();
  }

  void UpdateCountdown() {
    float countdownTimeLeft = Mathf.CeilToInt(GAME_COUNTDOWN - (Time.time - countdownTimeStart));
    countdownText.text = countdownTimeLeft > 0 ? countdownTimeLeft.ToString() : "GO!";
    countdownText.color = countdownTimeLeft > 0 ? Color.white : GREEN;
    if (countdownTimeLeft < 0) {
      gameRunning = true;
      timeLeft = GAME_PERIOD;
      chatTimeLeft = 5;
      chatPeriod = 5;
      timeStart = Mathf.FloorToInt(Time.time);
      chatStart = Mathf.FloorToInt(Time.time);
    }
  }

  void ListenForReset() {
    if (Input.GetKeyDown(KeyCode.R)) {
      ResetGame();
    }
  }

  void ResetGame() {
    gameRunning = false;
    countdownTimeStart = Time.time;

    UpdateScore(0.0f);
    UpdateMultiplier(1.0f);

    scoreDeltaText.color = HIDDEN;
  }
  
  void UpdateTime() {
    int currentTime = Mathf.FloorToInt(Time.time - timeStart);
    int currentTimeElapsed = GAME_PERIOD - currentTime;
    
    if (timeLeft != currentTimeElapsed) {
      timeLeft = currentTimeElapsed;
      PulseCustomer();
    }
    
    timeText.text = "Time Left: " + timeLeft;
    
    chatTimeLeft = chatPeriod - (Mathf.FloorToInt(Time.time) - chatStart);
    if (chatTimeLeft <= 0) {
      UpdateScore(-(chatPeriod * SCORE_BASE_MULTIPLIER * scoreMultiplier * 0.5f));
      ResetChat();
    }
  }

  void UpdateMultiplier(float multiplier) {
    scoreMultiplier = multiplier;

    scoreMultiplierText.text = "Multiplier: x" + Mathf.RoundToInt(scoreMultiplier);
  }

  void UpdateScore(float delta) {
    score += delta;

    var positive = delta > 0;

    scoreText.text = "NPS Score: " + Mathf.RoundToInt(score);
    scoreDeltaText.text = (positive ? "+" : "") + Mathf.RoundToInt(delta).ToString();
    scoreDeltaText.color = positive ? GREEN : RED;
  }

  void ResetChat() {
    customerLocked = false;
    customerText.transform.position = CUSTOMER_START_POSITION;
    customerGoalPosition = Random.Range(0, colors.Length);

    if (timeLeft > 50) {
      chatPeriod = 5;
      UpdateMultiplier(1.0f);
    } else if (timeLeft > 30) {
      chatPeriod = 4;
      UpdateMultiplier(2.0f);
    } else {
      chatPeriod = 3;
      UpdateMultiplier(3.0f);
    }

    UpdateColors();

    chatStart = Mathf.FloorToInt(Time.time);
  }

  void PulseCustomer() {
    customerText.transform.localScale = CUSTOMER_PULSE_SCALE;
  }

  void UpdateCustomer() {
    if (customerText.transform.localScale.x > 0.1f) {
      customerText.transform.localScale *= 0.95f;
    }

    if (customerLocked) {
      customerText.transform.position = Vector3.MoveTowards(customerText.transform.position, distalReference.transform.position / 2, 0.5f);
    } else if (customerText.transform.position.z > 1) {
      customerText.transform.Translate(Vector3.back * Time.deltaTime * CUSTOMER_SPEED);
    }

    scoreDeltaText.transform.position = distalReference.transform.position / 3;
  }

  void UpdateChatTime() {
    chatTimeText.transform.position = customerText.transform.position + CHAT_TIME_OFFSET;
    chatTimeText.transform.localScale = customerText.transform.localScale;
    chatTimeText.text = chatTimeLeft.ToString();
  }

  void UpdateDistalReference() {
    Vector3 distalVector = distalReference.transform.position;
    distalX = distalVector.x;
    distalY = distalVector.y;

    if (Mathf.Abs(distalX) < DISTAL_TOLERANCE && Mathf.Abs(distalY) < DISTAL_TOLERANCE) {
      distalPosition = CENTER;
    } else if (Mathf.Abs(distalX) < DISTAL_TOLERANCE && distalY > ROOM_RADIUS - DISTAL_TOLERANCE) {
      distalPosition = TOP;
    } else if (Mathf.Abs(distalX) < DISTAL_TOLERANCE && distalY < -(ROOM_RADIUS - DISTAL_TOLERANCE)) {
      distalPosition = BOTTOM;
    } else if (distalX > ROOM_RADIUS - DISTAL_TOLERANCE && Mathf.Abs(distalY) < DISTAL_TOLERANCE) {
      distalPosition = RIGHT;
    } else if (distalX < -(ROOM_RADIUS - DISTAL_TOLERANCE) && Mathf.Abs(distalY) < DISTAL_TOLERANCE) {
      distalPosition = LEFT;
    } else if (distalX > ROOM_RADIUS - DISTAL_TOLERANCE && distalY > ROOM_RADIUS - DISTAL_TOLERANCE) {
      distalPosition = TOP_RIGHT;
    } else if (distalX > ROOM_RADIUS - DISTAL_TOLERANCE && distalY < -(ROOM_RADIUS - DISTAL_TOLERANCE)) {
      distalPosition = BOTTOM_RIGHT;
    } else if (distalX < -(ROOM_RADIUS - DISTAL_TOLERANCE) && distalY > ROOM_RADIUS - DISTAL_TOLERANCE) {
      distalPosition = TOP_LEFT;
    } else if (distalX < -(ROOM_RADIUS - DISTAL_TOLERANCE) && distalY < -(ROOM_RADIUS - DISTAL_TOLERANCE)) {
      distalPosition = BOTTOM_LEFT;
    } else {
      distalPosition = NEUTRAL;
    }
  }

  void HandleDistalPosition() {
    switch (distalPosition) {
      case NEUTRAL:
        break;
      case CENTER:
        customerLocked = true;
        break;
      default:
        if (!customerLocked) {
          break;
        }

        if (distalPosition == customerGoalPosition) {
          UpdateScore((chatPeriod - (Time.time - chatStart)) * SCORE_BASE_MULTIPLIER * scoreMultiplier);
        } else {
          UpdateScore(-(chatPeriod * SCORE_BASE_MULTIPLIER * scoreMultiplier));
        }

        ResetChat();
        break;
    }
  }

  void UpdateColors() {
    Shuffle(colors);

    customerText.color = colors[customerGoalPosition];
    topText.color = colors[TOP];
    topLeftText.color = colors[TOP_LEFT];
    topRightText.color = colors[TOP_RIGHT];
    bottomText.color = colors[BOTTOM];
    bottomLeftText.color = colors[BOTTOM_LEFT];
    bottomRightText.color = colors[BOTTOM_RIGHT];
    leftText.color = colors[LEFT];
    rightText.color = colors[RIGHT];
  }

  void FadeScoreDelta() {
    var deltaColor = scoreDeltaText.color;
    if (deltaColor.a > 0.9f) {
      scoreDeltaText.color = new Color(deltaColor.r, deltaColor.g, deltaColor.b, deltaColor.a - 0.003f);
    } else if (deltaColor.a > 0.0f) {
      scoreDeltaText.color = new Color(deltaColor.r, deltaColor.g, deltaColor.b, deltaColor.a - 0.075f);
    }
  }
  
  void FadeCountdown() {    
    if (countdownText.color.a > 0.0f) {
      countdownText.color = new Color(GREEN.r, GREEN.g, GREEN.b, countdownText.color.a - 0.025f);
    }
  }

  void Shuffle(Color[] unshuffledColors) {
    for (int c = 0; c < unshuffledColors.Length; c++) {
      Color temp = colors[c];
      int r = Random.Range(c, colors.Length);
      unshuffledColors[c] = unshuffledColors[r];
      unshuffledColors[r] = temp;
    }
  }

  void UpdateVisionMode() {
    if (Input.GetKeyDown(KeyCode.F2)) {
      visionMode ^= visionMode;
      OVRManager.tracker.isEnabled = visionMode;            
    }
  }

  void UpdateSpeedAndRotationScaleMultiplier() {
    float moveScaleMultiplier = 0.0f;
    playerController.GetMoveScaleMultiplier(ref moveScaleMultiplier);

    if (Input.GetKeyDown(KeyCode.Alpha7)) {
      moveScaleMultiplier -= speedRotationIncrement;
    } else if (Input.GetKeyDown(KeyCode.Alpha8)) {
      moveScaleMultiplier += speedRotationIncrement;
    }

    playerController.SetMoveScaleMultiplier(moveScaleMultiplier);

    float rotationScaleMultiplier = 0.0f;
    playerController.GetRotationScaleMultiplier(ref rotationScaleMultiplier);

    if (Input.GetKeyDown(KeyCode.Alpha9)) {
      rotationScaleMultiplier -= speedRotationIncrement;
    } else if (Input.GetKeyDown(KeyCode.Alpha0)) {
      rotationScaleMultiplier += speedRotationIncrement;
    }

    playerController.SetRotationScaleMultiplier(rotationScaleMultiplier);
    debugInfo.UpdateSpeedAndRotationScaleMultiplier(moveScaleMultiplier, rotationScaleMultiplier);
  }    
      
  void UpdateRecenterPose() {
    if (Input.GetKeyDown(KeyCode.R))
      OVRManager.display.RecenterPose();
  }
}
