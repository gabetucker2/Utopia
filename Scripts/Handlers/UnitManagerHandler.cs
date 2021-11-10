using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UnitManager))]
public class UnitManagerHandler : Editor
{
    public override void OnInspectorGUI()
    {
        UnitManager unitManager = (UnitManager)target;

        if (GUILayout.Button("Create Unit"))
        {
            unitManager.CreateManualUnit();
        }

        DrawDefaultInspector();
    }
}
