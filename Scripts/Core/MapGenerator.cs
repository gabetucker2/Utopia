using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine.Serialization;


public class MapGenerator : MonoBehaviour
{
    public string heightMapEditName;
    public int heightMapEditIndex;
    public Prime prime;
    public Camera minimapCamera;
    public int minimapUnitSpan;
    public Transform minimapIcon;

    [Range(0f, 0.1f)] public float topHexColorMultiplier, sideHexColorMultiplier;

    public Transform mapContainer;
    public GameObject chunkPrefab;
    public GameObject oceanPrefab;

    [Range(1, 6)] public int oceanSize;
    [Tooltip("Chunks per map")] [Range(1, 42)] public int mapSize;
    [Tooltip("Tiles per chunk (must be even)")] [Range(1, 42)] public int chunkSize;//cannot have more than 65,534 verts (65,534 < (chunkSize^2 * vertsPerTile) )
    [Tooltip("Units per tile")] [Range(1, 10)] public int hexSize;//must be static
    [Range(0f, 1f)] public float heightRounder;
    [Range(1, 100)] public int hexHeight;

    public MapProperties mapProperties;

    //private List<Vector3> chunkPositions;

    private float chunkConstant = 42;

    [SerializeField] public List<HeightMap> heightMaps;

    [HideInInspector] [SerializeField] private List<GameObject> chunkObjs = new List<GameObject>();
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> hexTriangles = new List<int>(), squareTriangles = new List<int>();
    private List<Color> colors = new List<Color>();

    private float hexLength, hexBottom, hexWidth, hexWidthPartial, hexLeftFull, hexLeftPartial, hexCenterWidth, hexCenterLength;
    private List<Vector3> defHexVerts = new List<Vector3>();

    private void OnValidate()
    {
        minimapCamera.orthographicSize = minimapUnitSpan * hexSize;
        minimapIcon.localScale = new Vector3Int(hexSize, hexSize, 1);

        hexLength = Mathf.Sqrt(3f / 4f) * hexSize;
        hexBottom = 0f * hexSize;
        hexWidth = 1f * hexSize;
        hexWidthPartial = 0.75f * hexSize;
        hexLeftFull = 0f * hexSize;
        hexLeftPartial = hexWidthPartial / 3f;
        hexCenterLength = hexLength / 2f;
        hexCenterWidth = hexLeftPartial * 2f;

        defHexVerts = new List<Vector3> {//                             T - T - B
            new Vector3(hexCenterWidth, 0f, hexCenterLength),//c        0 - / - /
            new Vector3(hexLeftPartial, 0f, hexLength),//lt             1 - 7 - 13
            new Vector3(hexWidthPartial, 0f, hexLength),//rt            2 - 8 - 14
            new Vector3(hexWidth, 0f, hexCenterLength),//r              3 - 9 - 15
            new Vector3(hexWidthPartial, 0f, hexBottom),//rb            4 - 10- 16
            new Vector3(hexLeftPartial, 0f, hexBottom),//lb             5 - 11- 17
            new Vector3(hexLeftFull, 0f, hexCenterLength)//l            6 - 12- 18
        };

        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += () =>
            {
                RegenerateMap();
            };
        }
    }

    private static List<int> defHexTriangles = new List<int> {//applies to 0-6
        0, 1, 2,
        0, 2, 3,
        0, 3, 4,
        0, 4, 5,
        0, 5, 6,
        0, 6, 1
    }, defSquareTriangles = new List<int> {//applies to all
        //top face
        7, 14, 8,
        14, 7, 13,
        //bot face
        10, 17, 11,
        17, 10, 16,
        //right-top face
        8, 15, 9,
        15, 8, 14,
        //right-bot face
        9, 16, 10,
        16, 9, 15,
        //left-top face
        12, 13, 7,
        13, 12, 18,
        //left-bot face
        11, 18, 12,
        18, 11, 17
    };
    
    public void RegenerateSeeds()
    {
        foreach (HeightMap hm in heightMaps)
        {
            hm.seed = UnityEngine.Random.Range(0f, 10000f);
        }

        RegenerateMap();
    }

    public void HeightMapSave()
    {
        void UpdateValues(HeightMapSave hms)
        {
            hms.name = heightMapEditName;
            hms.mapProperties = mapProperties.Clone();
            hms.heightMaps = new List<HeightMap>();
            foreach (HeightMap hm in heightMaps) { hms.heightMaps.Add(hm.Clone()); }
        }

        if (prime.heightMapSaves.Count - 1 >= heightMapEditIndex && heightMapEditIndex >= 0) {//if index exists in saves
            UpdateValues(prime.heightMapSaves[heightMapEditIndex]);
        }
        else if (prime.heightMapSaves.Count == heightMapEditIndex) {//if index is 1 more than exists, meaning to make a new save
            HeightMapSave thisSave = new HeightMapSave();
            UpdateValues(thisSave);
            prime.heightMapSaves.Add(thisSave);
        }
        else { Debug.LogError("INVALID HEIGHTMAP EDIT INDEX"); }
    }

    public void HeightMapLoad()
    {
        if (heightMapEditIndex >= 0 && prime.heightMapSaves.Count - 1 >= heightMapEditIndex)
        {
            HeightMapSave thisSave = prime.heightMapSaves[heightMapEditIndex];
            heightMapEditName = thisSave.name;
            mapProperties = thisSave.mapProperties.Clone();
            heightMaps = new List<HeightMap>();
            foreach (HeightMap hm in thisSave.heightMaps) { heightMaps.Add(hm.Clone()); }
        }
        else { Debug.LogError("INVALID HEIGHTMAP EDIT INDEX"); }

        RegenerateMap();
    }

    public void HeightMapErase()
    {
        if (heightMapEditIndex >= 0 && prime.heightMapSaves.Count - 1 >= heightMapEditIndex)
        {
            prime.heightMapSaves.RemoveAt(heightMapEditIndex);
        }
        else { Debug.LogError("INVALID HEIGHTMAP EDIT INDEX"); }
    }

    public void RemoveAll()
    {
        for (int c = 0; c < chunkObjs.Count; c++)
        {
            DestroyImmediate(chunkObjs[c]);
        }

        foreach (GameObject ocean in prime.oceans)
        {
            DestroyImmediate(ocean);
        }

        chunkObjs.Clear();
        prime.chunks.Clear();
        prime.oceans.Clear();
    }

    public void RegenerateMap()
    {
        //HOUSEKEEPING FOR MAKING OBJ COUNT MATCH AMOUNT IN THIS SCENE

        RemoveAll();

        Transform chunksContainer = mapContainer.Find("Chunks");

        for (int c = 0; c < mapSize * mapSize; c++)//foreach chunk there will be in this map
        {
            //create the new obj
            GameObject chunkObj = Instantiate(chunkPrefab);
            chunkObj.transform.SetParent(chunksContainer);
            chunkObj.name = chunkObjs.Count.ToString();
            chunkObjs.Add(chunkObj);
        }

        float heightSquasher = mapProperties.heightSquasher;

        //MAP MAKER
        int i = 0;//each chunk
        for (int a = 0; a < mapSize; a++)//chunks wide
        {
            for (int b = 0; b < mapSize; b++, i++)//chunks tall
            {
                //SET UP CHUNK SETTINGS
                GameObject chunkObj = chunkObjs[i];
                Mesh chunkMesh = new Mesh();
                chunkObj.GetComponent<MeshFilter>().sharedMesh = chunkMesh;
                chunkObj.GetComponent<MeshCollider>().sharedMesh = chunkMesh;

                Chunk chunk = new Chunk(i.ToString(), chunkObj, new Vector3(), new List<Tile>());
                prime.chunks.Add(chunk);

                vertices.Clear();
                hexTriangles.Clear();
                squareTriangles.Clear();
                colors.Clear();

                //MAKE MAP
                int j = 0;//each tile
                for (int x = 0; x < chunkSize; x++)//width chunk
                {
                    for (int z = 0; z < chunkSize; z++, j++)//height chunk
                    {

                        //CREATE HEXAGON
                        Vector3 thisHexPos = new Vector3();

                        //RANDOM HEIGHT DISTRIBUTION
                        Material topMaterial = null, sideMaterial = null;
                        float extremestPerlinActual = -1; float extremestPerlinVal = -1; int highestPerlinKey = -1;
                        float y = 0f;
                        foreach (HeightMap heightMap in heightMaps)
                        {
                            if (heightMap.enabled)
                            {
                                float thisPerlin = Mathf.PerlinNoise(
                                    ((((a * chunkSize) + x) * heightMap.waveLength) / chunkConstant) + heightMap.seed,
                                    ((((b * chunkSize) + z) * heightMap.waveLength) / chunkConstant) + heightMap.seed);

                                if (!heightMap.allowExtremes) { thisPerlin = Mathf.Clamp(thisPerlin, 0f, 1f); }

                                if (thisPerlin <= heightMap.clamp)
                                {
                                    float oldPerlin = (thisPerlin - heightMap.clamp) / heightMap.clamp * -1;//fake heights for hard-edgeded objects for sake of height mat rendering
                                    if (heightMap.hardEdges) { thisPerlin = (heightMap.clamp + 1f) / (thisPerlin + 1f); }
                                    else { thisPerlin = (thisPerlin - heightMap.clamp) / heightMap.clamp * -1; }

                                    for (int e = 0; e < heightMap.exponentiations - 1; e++)
                                    {
                                        thisPerlin *= thisPerlin * heightMap.exponentiations;
                                    }

                                    if (Math.Abs(thisPerlin * heightMap.materialStrength) > extremestPerlinVal)
                                    {
                                        extremestPerlinActual = Math.Abs(Mathf.Clamp(oldPerlin, 0f, 1f));
                                        extremestPerlinVal = Math.Abs(thisPerlin * heightMap.materialStrength);
                                        highestPerlinKey = heightMaps.IndexOf(heightMap);
                                    }
                                    y += thisPerlin * heightMap.amplitude * hexSize * heightSquasher;
                                }
                            }
                        }

                        //MATERIAL SETTER
                        HeightMap thisHeightMap = heightMaps[highestPerlinKey];
                        float thisTotalDists = thisHeightMap.heightMaterials.Sum(v => v.heightSpace);
                        float thisCurrentDist = 0f;
                        HeightMap.HeightMaterials thisHeightMaterial = null;
                        foreach (HeightMap.HeightMaterials hm in thisHeightMap.heightMaterials)
                        {
                            thisCurrentDist += hm.heightSpace / thisTotalDists;

                            if (thisCurrentDist - (hm.heightSpace / thisTotalDists) <= extremestPerlinActual && extremestPerlinActual <= thisCurrentDist)
                            {
                                thisHeightMaterial = hm;
                                y += hm.heightOffset * thisHeightMap.amplitude * hexSize * heightSquasher;
                                break;
                            }
                        }

                        if (heightRounder != 0f) { y = Mathf.CeilToInt(y * (1f / heightRounder)) / (1f / heightRounder); }

                        //overflow to highest element
                        topMaterial = thisHeightMaterial != null ? thisHeightMaterial.topMaterial : thisHeightMap.heightMaterials[thisHeightMap.heightMaterials.Count - 1].topMaterial;
                        sideMaterial = thisHeightMaterial != null ? thisHeightMaterial.sideMaterial : thisHeightMap.heightMaterials[thisHeightMap.heightMaterials.Count - 1].sideMaterial;

                        //VERTICES
                        int r = 0;
                        for (int q = 0; q < 3; q++)//top verts / top verts / bottom verts
                        {
                            List<Vector3> thisHexVerts = new List<Vector3>();
                            float downOffset;
                            if (q < 2) /*top hex verts*/ { downOffset = 0f; }
                            else /*bottom hex verts*/
                            { downOffset = -hexHeight * heightSquasher * hexSize; }

                            for (int v = 0; v < defHexVerts.Count; v++)
                            {
                                int thisV = v;

                                if (!(q > 0 && thisV == 0))//don't add center vert for second top or bottom hexagons
                                {

                                    float lengthOffset;
                                    if (((a * chunkSize) + x) % 2 == 0) /*even*/ { lengthOffset = hexBottom; }
                                    else /*odd*/ { lengthOffset = hexLength / 2f; }

                                    r++;
                                    if (q == 0) /*top verts*/ { colors.Add(new Color(topMaterial.color.r - (r * topHexColorMultiplier), topMaterial.color.g - (r * topHexColorMultiplier), topMaterial.color.b - (r * topHexColorMultiplier))); }
                                    else /*side verts*/ { colors.Add(new Color(sideMaterial.color.r - (r * sideHexColorMultiplier), sideMaterial.color.g - (r * sideHexColorMultiplier), sideMaterial.color.b - (r * sideHexColorMultiplier))); }//dampen with each new vertex

                                    Vector3 thisPos = defHexVerts[thisV] + new Vector3((a * chunkSize * hexWidthPartial) + (x * hexWidthPartial), y + downOffset, (b * chunkSize * hexLength) + (z * hexLength) + lengthOffset);
                                    thisHexVerts.Add(thisPos);
                                    if (thisV == 0) { thisHexPos = thisPos; }//assuming c is defHexVerts[0], set position to top/middle of hex
                                }
                            }

                            vertices.AddRange(thisHexVerts);//+7, +6, +6
                        }

                        //HEXTRIANGLES
                        List<int> thisHexTriangles = new List<int>();
                        for (int t = 0; t < defHexTriangles.Count / 3; t++)//foreach triangle t, found by dividing count of ints by 3
                        {
                            for (int p = t * 3; p < ((t + 1) * 3); p++)//foreach triangle point p below next triangle iteration
                            {
                                thisHexTriangles.Add(defHexTriangles[p] + (j * ((defHexVerts.Count * 3) - 2)));
                            }
                        }
                        hexTriangles.AddRange(thisHexTriangles);

                        //SQUARETRIANGLES
                        List<int> thisSquareTriangles = new List<int>();
                        for (int t = 0; t < defSquareTriangles.Count / 3; t++)//foreach triangle t, found by dividing count of ints by 3
                        {
                            for (int p = t * 3; p < ((t + 1) * 3); p++)//foreach triangle point p below next triangle iteration
                            {
                                thisSquareTriangles.Add(defSquareTriangles[p] + (j * ((defHexVerts.Count * 3) - 2)));
                            }
                        }
                        squareTriangles.AddRange(thisSquareTriangles);

                        //ADD TO DATABASE
                        chunk.tiles.Add(new Tile(j.ToString(), thisHexPos, true, new List<int>(), topMaterial, sideMaterial));//keep in mind that pos is top of hex

                    }
                }

                //UPDATE CHUNK MESH WITH NEW INFO
                chunk.position = new Vector3(chunk.tiles.Average(v => v.position.x), chunk.tiles.Average(v => v.position.y), chunk.tiles.Average(v => v.position.z));//average of its constituents

                chunkMesh.vertices = vertices.ToArray();
                List<int> triangles = hexTriangles; triangles.AddRange(squareTriangles); chunkMesh.triangles = triangles.ToArray();
                chunkMesh.colors = colors.ToArray();
                chunkMesh.RecalculateNormals();
            }
        }
        
        float bulkHeight = hexSize * mapProperties.heightSquasher;
        Vector3 oceanScale = new Vector3((chunkSize * hexWidthPartial) / 2f, (chunkSize * hexLength) / 2f, 1f);//y and z are flipped for ocean
        Transform oceansContainer = mapContainer.Find("Oceans");
        int thisChunk = 0;
        int thisOcean = 0;
        
        for (int ox = 0; ox < oceanSize; ox++)
        {
            for (int oz = 0; oz < oceanSize; oz++, thisOcean++)
            {
                for (int cx = 0; cx < mapSize; cx++)
                {
                    for (int cz = 0; cz < mapSize; cz++, thisChunk++)
                    {
                        int localChunk = thisChunk - (mapSize * mapSize * thisOcean);
                        Transform ocean = Instantiate(oceanPrefab).transform;
                        ocean.name = thisChunk.ToString();
                        prime.oceans.Add(ocean.gameObject);
                        
                        float
                            thisBaseMult = mapSize * chunkSize,
                            thisWidthOffset = hexWidthPartial * thisBaseMult * ox, thisLengthOffset = hexLength * thisBaseMult * oz,
                            centerSubBase = oceanSize * thisBaseMult * ((-Mathf.Pow(2, -oceanSize + 2f) + 2.5f) / 6f);
                        if (oceanSize == 1) { centerSubBase = 0f; }
                        float centerSubWidth = centerSubBase * hexWidthPartial, centerSubLength = centerSubBase * hexLength;

                        ocean.position = new Vector3(prime.chunks[localChunk].position.x + thisWidthOffset - centerSubWidth, mapProperties.oceanLevel * bulkHeight, (prime.chunks[localChunk].position.z + thisLengthOffset - centerSubLength));
                        ocean.localScale = oceanScale;
                        ocean.SetParent(oceansContainer);

                        Material oceanMat = new Material(ocean.GetComponent<MeshRenderer>().sharedMaterial);
                        oceanMat.SetFloat("MainNormalTileSize", ocean.localScale.x / bulkHeight);
                        oceanMat.SetFloat("SecondaryNormalTileSize", ocean.localScale.x / bulkHeight);
                        oceanMat.SetFloat("Depth", mapProperties.oceanDepth * bulkHeight);
                        oceanMat.SetFloat("Strength", mapProperties.oceanStrength * bulkHeight);
                        oceanMat.SetFloat("Displacement", mapProperties.oceanDisplacement);
                        oceanMat.SetFloat("MoveMultiplier", mapProperties.oceanMoveMultiplier / chunkSize);
                        oceanMat.SetFloat("XNoiseOffset", -((ox * mapSize) + cx));//1 unit is 1 gradient away
                        oceanMat.SetFloat("ZNoiseOffset", -((oz * mapSize) + cz));//1 unit is 1 gradient away
                        oceanMat.SetFloat("PosNoiseWavelength", chunkSize * mapProperties.wavelengthMultiplier);
                        oceanMat.SetColor("DeepWaterColor", mapProperties.deepWaterColor);
                        oceanMat.SetColor("ShallowWaterColor", mapProperties.shallowWaterColor);
                        ocean.GetComponent<MeshRenderer>().sharedMaterial = oceanMat;

                    }
                }    
            }
        }
    }
}
