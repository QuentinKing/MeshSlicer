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

        if (GUILayout.Button("Slice This"))
        {
            meshSlicer.SliceCurrentMesh();
        }

        if (GUILayout.Button("Slice All Children"))
        {
            meshSlicer.SliceAllChildren();
        }

        if (GUILayout.Button("Test Stuff"))
        {
            Debug.LogError(Vector3.SignedAngle(Vector3.right, Vector3.up, Vector3.forward));
            Debug.LogError(Vector3.SignedAngle(Vector3.right, Vector3.down, Vector3.forward));
            Debug.LogError(Vector3.SignedAngle(Vector3.right, Vector3.up, Vector3.back));
            Debug.LogError(Vector3.SignedAngle(Vector3.right, Vector3.down, Vector3.back));
        }
    }
}
