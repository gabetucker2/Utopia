using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class UnitManager : MonoBehaviour
{
    [SerializeField] public UnitParticularVariables newUnitParticularVariables;
    public Prime prime;
    public Transform unitContainer;
    private MapGenerator mapGenerator;
    public GameObject indicatorPrefab;

    public List<Class> classes;
    public List<Team> teams;

    private float thisY, thisSize;


    public void CreateUnit(UnitParticularVariables UnitParticularVariables)
    {

        //MAKE UNIT DATA
        Unit thisUnit = new Unit(prime.nextUnitKey, UnitParticularVariables, new UnitUniversalVariables("", "", null, null, null, false), GetClassProperties(UnitParticularVariables.unitClass, UnitParticularVariables.unitProfession, UnitParticularVariables.unitSpecialty));
        prime.units.Add(thisUnit);
        prime.nextUnitKey++;

        //CREATE PHYSICAL UNIT
        if (!thisUnit.particularVariables.isPlayer)//assuming the player physical stuff already exists
        {
            thisUnit.particularVariables.GO = Instantiate(thisUnit.particularVariables.unitPrefab);
            thisUnit.particularVariables.GO.transform.SetParent(unitContainer);
        }
        else
        {
            thisUnit.particularVariables.GO = prime.playerGO;
        }

        //SET TRANS
        thisUnit.universalVariables.trans = thisUnit.particularVariables.GO.transform.Find("Body");//trans
        if (!thisUnit.particularVariables.isPlayer)
        {
            
            StartCoroutine(TranslateToTile(false, -1f, thisUnit, thisUnit.universalVariables.trans));//set unit default pos if not player
            
            //SET REST OF UNIVERSAL VARIABLES
            thisUnit.universalVariables.trans.position = prime.units[0].universalVariables.trans.position;
        
        }//offset position right behind the current player
        thisUnit.universalVariables.minimapIcon = thisUnit.universalVariables.trans.Find("MinimapIcon").GetComponent<SpriteRenderer>();//mmicon
        //indicator
        GameObject thisIndicatorGO = Instantiate(indicatorPrefab);
        thisIndicatorGO.name = "Indicator";
        Material indicatorMat = Instantiate(thisIndicatorGO.GetComponent<MeshRenderer>().material);
        thisIndicatorGO.GetComponent<MeshRenderer>().material = indicatorMat;
        Color teamColor = teams.Find(v => v.name == thisUnit.particularVariables.teamName).color;
        thisIndicatorGO.transform.SetParent(thisUnit.particularVariables.GO.transform);
        thisUnit.universalVariables.tileIndicator = thisIndicatorGO;

        //SET UNIT STUFF TO TEAM COLOR
        if (!thisUnit.particularVariables.isPlayer)
        {
            thisUnit.universalVariables.minimapIcon.color = new Color(teamColor.r, teamColor.g, teamColor.b, 1f);
            indicatorMat.SetColor("PrimaryColor", new Color(teamColor.r, teamColor.g, teamColor.b, indicatorMat.GetColor("PrimaryColor").a));
        }

    }

    public void CreateManualUnit()//create unit from inspector
    {
        CreateUnit(newUnitParticularVariables);
    }

    public UnitProperties GetClassProperties(string uclass, string uprofession, string uspecialty)
    {
        return classes.Find(v => v.name == uclass).professions.Find(v => v.name == uprofession).specialties.Find(v => v.name == uspecialty).baseProperties;
    }

    public IEnumerator TranslateToTile(bool doLerp, float lerpTime, Unit thisUnit, Transform unitTrans)//translation for individual cases, not A* Pathfinding
    {
        //MAKE SURE WE AREN'T INTERRUPTING ANOTHER TRANSLATION, AND IF WE ARE, THERE WAS A BREAK FIRST
        bool startNewTranslation = false;

        for (int i = 0; i < 2; i++)
        {
            if(i == 0)//if first frame
            {
                if (thisUnit.universalVariables.isTranslating) { break; }//something else is translating and hasn't been broken; don't worry about waiting to check for next frame
                
                yield return new WaitForEndOfFrame();
            }
            else if (!thisUnit.universalVariables.isTranslating)//if second frame, see if this frame isn't translating; if it isn't and the previous frame isn't, it knows to start a new translation
            {
                startNewTranslation = true;
            }
        }

        if (startNewTranslation)//if not interrupting another translation
        {
            //PERFORM MAIN FUNCTION
            //set up vars
            thisUnit.universalVariables.isTranslating = true; prime.playerRB.isKinematic = true; prime.playerRB.useGravity = false;

            Chunk thisChunk = prime.chunks.Find(c => c.name == thisUnit.universalVariables.thisChunkName);
            Tile thisTile = thisChunk.tiles.Find(t => t.name == thisUnit.universalVariables.thisTileName);

            float unitHeight = unitTrans.localScale.y;
            Vector3 endPos = thisTile.position + new Vector3(0f, unitHeight, 0f);

            bool isEscaping = false;

            //move
            if (!doLerp)//TELEPORT
            {
                unitTrans.position = endPos;

                thisUnit.universalVariables.isTranslating = false;
            }
            else//LERP
            {
                Vector3 startPos = unitTrans.position;

                for (float l = 0; l < 1f; l += Time.deltaTime / lerpTime)
                {
                    if (thisUnit.universalVariables.isTranslating)
                    {
                        unitTrans.position = Vector3.Lerp(startPos, endPos, l);

                        yield return new WaitForEndOfFrame();
                    }
                    else//to break this translation, set it to false for a frame during this translation; this gives this translation time to stop before the next translation starts
                    {
                        isEscaping = true;
                        break;
                    }
                }
            }

            if (!isEscaping) { unitTrans.position = endPos; thisUnit.universalVariables.isTranslating = false; prime.playerRB.isKinematic = false; prime.playerRB.useGravity = true; }//only conclude function if it ended without being broken
        }
    }

    public void BreakTranslation(Unit thisUnit)
    {
        thisUnit.universalVariables.isTranslating = false;
    }


    private void Start()
    {
        mapGenerator = prime.mapGenerator;
        thisSize = mapGenerator.hexSize;
        thisY = thisSize * mapGenerator.heightRounder * 0.1f;
    }

    List<KeyValuePair<int, Tile>> prevTiles = new List<KeyValuePair<int, Tile>>(), currentTiles = new List<KeyValuePair<int, Tile>>();

    private bool OccupiableTile(Tile thisTile)
    {
        if (thisTile.occupantUnits.Count == 0) { return true; }
        else { return false; }
    }

    private void Update()
    {
        currentTiles.Clear();

        foreach (Unit unit in prime.units)
        {
            Transform unitTrans = unit.universalVariables.trans;

            //GET CLOSEST TILE
            List<string> depositedthisChunks = new List<string>();
            Chunk chunk = null;
            Tile tile = null;
            while (chunk == null)//backup in case entire chunk is or all chunks are unoccupiable
            {
                Vector3 castratedUnitTransPos = unitTrans.position - new Vector3(0f, unitTrans.position.y, 0f);

                float closestDist = float.MaxValue;
                foreach (Chunk thisChunk in prime.chunks)
                {
                    Vector3 castratedThisChunkPos = thisChunk.position - new Vector3(0f, thisChunk.position.y, 0f);

                    float thisMag = Vector3.SqrMagnitude(castratedUnitTransPos - castratedThisChunkPos);//castrate = making y difference 0 in SqrMag so that ravenes on part of a chunk don't disqualify a close part of the chunk from being considered as closest
                    
                    if (thisMag < closestDist && !depositedthisChunks.Contains(thisChunk.name))
                    {
                        chunk = thisChunk;
                        closestDist = thisMag;
                    }
                }


                closestDist = float.MaxValue;
                if (chunk != null)
                {
                    foreach (Tile thisTile in chunk.tiles)
                    {
                        float thisMag = Vector3.SqrMagnitude(unitTrans.position - thisTile.position);

                        if (thisMag < closestDist && (thisTile.occupiable || thisTile.occupantUnits.Contains(unit.key)))
                        {
                            tile = thisTile;
                            closestDist = thisMag;
                        }
                    }
                }

                if (tile == null)//or chunk == null
                {
                    try { depositedthisChunks.Add(chunk.name); } catch { Debug.LogError("NO UNDEPOSITED CHUNKS REMAIN-ESCAPING SCRIPT"); break; }
                    chunk = null;
                }
            }

            if (chunk == null) { Debug.LogError("ALL TILES ARE UNOCCUPIABLE"); }
            else if (unit.universalVariables.thisChunkName != chunk.name || unit.universalVariables.thisTileName != tile.name)//if this closest chunk/tile is different from last and thisTile was found
            {

                //PERFORM FUNCTION
                unit.universalVariables.thisChunkName = chunk.name;
                unit.universalVariables.thisTileName = tile.name;

                KeyValuePair<int, Tile> thisPair = new KeyValuePair<int, Tile>(unit.key, tile);
                currentTiles.Add(thisPair);
                if (prevTiles.Exists(v => v.Key == thisPair.Key))//remove this key from occupantUnits in the previous tile this unit was on
                {
                    Tile prevTile = prevTiles.Find(v => v.Key == thisPair.Key).Value;
                    prevTile.occupantUnits.Remove(unit.key);
                    prevTile.occupiable = OccupiableTile(prevTile);
                }//else should be true iff the unit was just created
                tile.occupantUnits.Add(unit.key);//add this key to the new tile
                tile.occupiable = OccupiableTile(tile);

                Transform thisHexTrans = unit.universalVariables.tileIndicator.transform;
                thisHexTrans.localScale = new Vector3(thisSize, thisY, thisSize);
                thisHexTrans.position = tile.position;

            }
        }

        prevTiles = currentTiles;
    }
}
