using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MeshSlicer))]
public class MeshSlicerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MeshSlicer meshSlicer = (MeshSlicer)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Inspector Functions", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset Mesh"))
        {
            meshSlicer.ResetMesh();
        }

        if (GUILayout.Button("Slice Mesh"))
        {
            meshSlicer.SliceMesh();
        }
    }
}
