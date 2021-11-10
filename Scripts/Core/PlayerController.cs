using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("ADJUST:")]
    public Prime prime;
    private MapGenerator mapGenerator;
    private UnitManager unitManager;
    private Unit playerUnit;
    private Transform playerTrans;
    private Rigidbody playerRB;
    public Transform playerCameraTrans;

    public Transform minimapCameraTrans;
    public Transform minimap;

    [Range(0f, 3f)] public float returnToTileCenterTime;

    [Range(0f, 10f)] public float cameraDistanceConstant;
    [Range(1f, 20f)] public float minimapTilesAcross;
    [Range(0f, 3f)] public float cameraHeightOffset;

    [Range(0f, 44.99f)] public float pitchLeewayMin, pitchLeewayMax;
    [Range(0f, 44.99f)] public float offsetPitchLeewayMin, offsetPitchLeewayMax;

    [Range(0f, 5f)] public float xRotMultiplier, yRotMultiplier;
    [Range(0f, 1f)] public float sidewaysMoveMultiplier;
    [Range(1f, 50f)] public float sprintSpeedMult;
    [Range(0f, 2500f)] public float jumpForce;

    [Header("DISPLAY:")]
    [Range(0f, 20f)] public float moveSpeed;

    private float timeSinceLastMove = 0f;
    private bool hasMoved = true;

    private Vector3 mmStartRot;
    private Vector3 mmStartPos;

    private float pitch = 0f, offsetPitch = 0f, yaw = 0f;
    private float startPitch;

    private void Start()
    {
        playerUnit = prime.units[0];
        playerTrans = playerUnit.universalVariables.trans;
        playerRB = prime.playerRB;
        mapGenerator = prime.mapGenerator;
        unitManager = prime.unitManager;
        playerTrans.position = new Vector3(mapGenerator.mapSize * mapGenerator.chunkSize * mapGenerator.hexSize / 2f, mapGenerator.hexSize * 100f, mapGenerator.mapSize * mapGenerator.chunkSize * mapGenerator.hexSize / 2f);

        startPitch = playerCameraTrans.eulerAngles.x;
        pitch = startPitch;

        mmStartRot = minimapCameraTrans.eulerAngles;
        mmStartPos = minimapCameraTrans.position;
    }

    private void Update()
    {

        //GET UPDATED PROPERTIES
        moveSpeed = playerUnit.properties.moveSpeed;
        float localMoveSpeed = (moveSpeed * mapGenerator.hexSize) / 15f;//divide by approximation to make it move 1 tile per second

        //MOVE WITH AXES
        float vertAxis = Input.GetAxis("Vertical"), horizAxis = Input.GetAxis("Horizontal");
        float transStraight = vertAxis * localMoveSpeed * Time.deltaTime;
        float transSideways = horizAxis * localMoveSpeed * sidewaysMoveMultiplier * Time.deltaTime;
        float thisSprintSpeedMult = 1; if (Input.GetKey("left shift")) { thisSprintSpeedMult = sprintSpeedMult; }
        if (playerRB.isKinematic == false)//kinematic not only locks movement, but indicates scripts are adjusting position
        {
            playerTrans.Translate(transSideways * thisSprintSpeedMult, 0f, transStraight * thisSprintSpeedMult, Space.Self);//apply force

            //SNAP TO NEAREST TILE
            if (timeSinceLastMove >= returnToTileCenterTime && hasMoved)
            {
                StartCoroutine(unitManager.TranslateToTile(true, 0.4f, playerUnit, playerTrans));
                timeSinceLastMove = 0f;
                hasMoved = false;
            }
            else if (vertAxis == 0f && horizAxis == 0f) /*not moving*/ { timeSinceLastMove += Time.deltaTime; }
            else /*moving, reset*/ { timeSinceLastMove = 0f; hasMoved = true; }
        }

        //ROTATE WITH MOUSE
        float thisPitch = pitch - (Input.mouseScrollDelta.y * yRotMultiplier); if (-pitchLeewayMin + startPitch < thisPitch && thisPitch < pitchLeewayMax + startPitch) { pitch = thisPitch; }
        float thisOffsetPitch = offsetPitch - (Input.GetAxis("Mouse Y") * yRotMultiplier); if (-offsetPitchLeewayMin < thisOffsetPitch && thisOffsetPitch < offsetPitchLeewayMax) { offsetPitch = thisOffsetPitch; }
        yaw += Input.GetAxis("Mouse X") * xRotMultiplier;
        float xRot = pitch; float zLen = Mathf.Tan(Mathf.Deg2Rad * xRot) * -cameraDistanceConstant; float yLen = Mathf.Tan(Mathf.Deg2Rad * xRot) * -zLen;

        playerTrans.eulerAngles = new Vector3(0f, yaw, 0f);
        playerCameraTrans.localEulerAngles = new Vector3(pitch + offsetPitch, 0f, 0f);
        playerCameraTrans.localPosition = new Vector3(0f, yLen + (cameraHeightOffset * ((thisOffsetPitch + offsetPitchLeewayMin) / (offsetPitchLeewayMax + offsetPitchLeewayMin))), zLen);//division so offsetpitch gets more prevalent as camera gets closer to head and as fades out becomes neglected

        //JUMP WITH SPACE
        if (Input.GetKeyDown("space")) { playerRB.AddForce(playerTrans.up * jumpForce * mapGenerator.hexSize); }

        //CONSTRAIN MINIMAP
        minimapCameraTrans.rotation = Quaternion.Euler(mmStartRot);
        minimapCameraTrans.position = new Vector3(playerTrans.position.x, mmStartPos.y, playerTrans.position.z);

        //UPDATE MINIMAP SIZE WITH VAR
        minimapCameraTrans.GetComponent<Camera>().orthographicSize = minimapTilesAcross * mapGenerator.hexSize;
    }
}
