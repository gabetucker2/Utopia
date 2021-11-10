using System.Collections.Generic;
using UnityEngine;
using System;

public class Prime : MonoBehaviour
{
    [Header("ADJUST:")]
    public MapGenerator mapGenerator;
    public UnitManager unitManager;

    public GameObject playerGO;
    public Rigidbody playerRB;

    public float UITransitionSeconds;

    [Header("DISPLAY:")]
    public int nextUnitKey = 0;
    [SerializeField] public List<Unit> units;

    public List<GameObject> oceans;
    [SerializeField] public List<Chunk> chunks;
    [SerializeField] public List<HeightMapSave> heightMapSaves;

    private void Awake()
    {
        Cursor.visible = false;

        string uclass = "Gold", uprofession = "Emperor", uspecialty = "/";
        unitManager.CreateUnit(new UnitParticularVariables(playerGO.name, "State", uclass, uprofession, uspecialty, null, playerGO, true));//make starting player
    }
}

//MAP STUFF
[Serializable]
public class HeightMap
{
    [Serializable]
    public class HeightMaterials
    {
        public Material topMaterial;
        public Material sideMaterial;
        [Range(0.01f, 30f)] public float heightSpace;
        [Range(-2.5f, 2.5f)] public float heightOffset;

        public HeightMaterials(Material tm, Material sm, float hs, float ho)
        {
            topMaterial = tm; sideMaterial = sm; heightSpace = hs; heightOffset = ho;
        }
    }

    public string name = "_UNNAMED";
    [Range(0f, 10000f)] public float seed;
    [Range(-15f, 15f)] public float amplitude;
    [Range(30f, 0f)] public float waveLength;
    [Range(0.01f, 1f)] public float clamp;
    [Range(1f, 4f)] public int exponentiations;
    public bool hardEdges, allowExtremes, enabled;
    [SerializeField] public List<HeightMaterials> heightMaterials;
    [Range(0f, 5f)] public float materialStrength;

    public HeightMap Clone()
    {
        return (HeightMap)MemberwiseClone();
    }
}

[Serializable]
public class MapProperties
{
    [Range(1f, 0f)] public float heightSquasher;
    [Range(-75f, 75f)] public float oceanLevel;
    public float oceanDepth, oceanStrength;
    [Range(0f, 10f)] public float oceanDisplacement;
    [Range(1f, 0.01f)] public float wavelengthMultiplier;
    [Range(2f, 0.01f)] public float oceanMoveMultiplier;
    public Color deepWaterColor, shallowWaterColor;

    public MapProperties Clone()
    {
        return (MapProperties)MemberwiseClone();
    }
}

[Serializable] public class Tile
{
    public string name;
    public Vector3 position;
    public bool occupiable = true;
    public List<int> occupantUnits;
    public Material topMaterial, sideMaterial;

    public Tile (string n, Vector3 p, bool o, List<int> ou, Material tm, Material sm)
    {
        name = n; position = p; occupiable = o; occupantUnits = ou; topMaterial = tm; sideMaterial = sm;
    }
}

[Serializable] public class Chunk
{
    public string name;//ensure that each name is a key
    public GameObject GO;
    public Vector3 position;
    [SerializeField] public List<Tile> tiles;

    public Chunk (string n, GameObject _GO, Vector3 p, List<Tile> t)
    {
        name = n; GO = _GO; position = p; tiles = t;
    }
}

[Serializable] public class HeightMapSave
{
    public string name;
    public MapProperties mapProperties;
    public List<HeightMap> heightMaps;
}

//LIGHTING STUFF
[Serializable]
public class TimeLightingSetting
{
    public string settingName;
    [Range(0, 24)] public int startTime;
    public Color skyColor, ambientSkyColor, ambientEquatorColor, ambientGroundColor, fogColor;
    [Tooltip("Relative to one unit")] [Range(0f, 300f)] public float fogStart, fogEnd;

    public TimeLightingSetting(string thisName, int thisTime, Color thisSkyColor, Color thisAmbientSkyColor, Color thisAmbientEquatorColor, Color thisAmbientGroundColor, Color thisFogColor, float thisFogStart, float thisFogEnd)
    {
        settingName = thisName;
        startTime = thisTime;
        skyColor = thisSkyColor;
        ambientSkyColor = thisAmbientSkyColor;
        ambientEquatorColor = thisAmbientEquatorColor;
        ambientGroundColor = thisAmbientGroundColor;
        fogColor = thisFogColor;
        fogStart = thisFogStart;
        fogEnd = thisFogEnd;
    }
}

//UNIT STUFF
[Serializable] public class Unit
{
    public int key;
    [SerializeField] public UnitParticularVariables particularVariables;
    [SerializeField] public UnitUniversalVariables universalVariables;
    [SerializeField] public UnitProperties properties;

    public Unit(int _key, UnitParticularVariables referenceparticularVariables, UnitUniversalVariables referenceuniversalVariables, UnitProperties referenceProperties)
    {
        key = _key;
        particularVariables = referenceparticularVariables;
        universalVariables = referenceuniversalVariables;
        properties = referenceProperties;
    }
}

[Serializable]
public class UnitParticularVariables//universals
{
    public string name;
    public string teamName;
    public string unitClass, unitProfession, unitSpecialty;
    public GameObject unitPrefab;
    [Header("SCRIPT SETS THESE:")]
    public GameObject GO;
    public bool isPlayer;

    public UnitParticularVariables(string _name, string _teamName, string _unitClass, string _unitProfession, string _unitSpecialty, GameObject _unitPrefab, GameObject _GO, bool _isPlayer)
    {
        name = _name; teamName = _teamName; unitClass = _unitClass; unitProfession = _unitProfession; unitSpecialty = _unitSpecialty; unitPrefab = _unitPrefab; GO = _GO; isPlayer = _isPlayer;
    }
}

[Serializable]
public class UnitUniversalVariables//universals
{
    public string thisChunkName, thisTileName;
    public Transform trans;
    public SpriteRenderer minimapIcon;
    public GameObject tileIndicator;
    public bool isTranslating;

    public UnitUniversalVariables(string _thisChunkName, string _thisTileName, Transform _trans, SpriteRenderer _minimapIcon, GameObject _tileIndicator, bool _isTranslating)
    {
        thisChunkName = _thisChunkName; thisTileName = _thisTileName; trans = _trans; minimapIcon = _minimapIcon; tileIndicator = _tileIndicator; isTranslating = _isTranslating;
    }
}

[Serializable]
public class UnitProperties//particulars
{
    [Tooltip("0 is none, 10 is one tile per second")] [Range(0f, 20f)] public float moveSpeed;
}

[Serializable]
public class Class
{
    [Serializable]
    public class Profession
    {
        [Serializable]
        public class Specialty
        {
            public string name;
            [Tooltip("-1 means infinite")] public int maxAmount;

            public UnitProperties baseProperties;
        }

        [Tooltip("'/' means none")] public string name;
        public List<Specialty> specialties;
    }

    public string name;
    public List<Profession> professions;
}

[Serializable]
public class Team
{
    public string name;
    public Color color;
}

//UNIT BEHAVIORS
[Serializable]
public class BehaviorCondition
{

}

[Serializable]
public class BehaviorTree
{
    public string name;
    public string overrideName;
    [SerializeField] public List<BehaviorCondition> behaviorConditions;
}
