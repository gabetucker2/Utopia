using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorHandler : Editor
{
    public override void OnInspectorGUI()
    {
        MapGenerator mapGenerator = (MapGenerator)target;

        if (GUILayout.Button("Regenerate Map"))
        {
            mapGenerator.RegenerateMap();
        }

        if (GUILayout.Button("Regenerate Seeds"))
        {
            mapGenerator.RegenerateSeeds();
        }

        if (GUILayout.Button("Remove Map"))
        {
            mapGenerator.RemoveAll();
        }

        if (GUILayout.Button("Save Map"))
        {
            mapGenerator.HeightMapSave();
        }

        if (GUILayout.Button("Load Map"))
        {
            mapGenerator.HeightMapLoad();
        }

        if (GUILayout.Button("Erase Map Save"))
        {
            mapGenerator.HeightMapErase();
        }

        DrawDefaultInspector();
    }
}
