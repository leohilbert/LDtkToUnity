﻿using UnityEditor;
using UnityEngine;

#pragma warning disable 0649

namespace LDtkUnity.Editor
{
    public class LDtkNativeExportWindow : EditorWindow
    {
        [SerializeField] private GameObject _project;
        [SerializeField] private string _exportPath;
        
        private SerializedObject _serializedObject;
        private SerializedProperty _projectProp;
        private SerializedProperty _exportPathProp;
        private PathDrawer _pathDrawer;
        private LDtkNativePrefabAssets _clonedAssets;

        public static void CreateWindowWithContext(GameObject ctx)
        {
            LDtkNativeExportWindow window = ConstructWindow();
            window._project = ctx;
            window.OnEnable();
            window.Show();
        }

        private static LDtkNativeExportWindow ConstructWindow()
        {
            LDtkNativeExportWindow window = CreateInstance<LDtkNativeExportWindow>();
            window.titleContent = new GUIContent()
            {
                text = "LDtk Export",
                image = LDtkIconUtility.LoadSimpleIcon()
            };
            window.minSize = new Vector2(250, 140);

            return window;
        }
        
        private void OnEnable()
        {
            _serializedObject = new SerializedObject(this);
            _projectProp = _serializedObject.FindProperty(nameof(_project)); 
            _exportPathProp = _serializedObject.FindProperty(nameof(_exportPath));
        }

        private void OnGUI()
        {
            GUIContent content = new GUIContent("Export Path");

            GUIContent headerContent = new GUIContent()
            {
                text = "Export Native Prefab",
                tooltip = "If you feel like uninstalling the LDtkToUnity package but want to maintain the LDtk project you Unity, you can export a prefab and assets to a folder."
            };

            EditorGUILayout.LabelField(headerContent, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_projectProp);
            
            GameObject obj = (GameObject)_projectProp.objectReferenceValue;
            if (TryInvalidPass(obj))
            {
                return;
            }

            string pathToObject = AssetDatabase.GetAssetPath(obj);
            
            _pathDrawer = new PathDrawer(_exportPathProp, content, pathToObject, "The path to export the prefab and related assets to.");
            string exportPath = _pathDrawer.DrawFolderField();
            
            if (!AssetDatabase.IsValidFolder(exportPath))
            {
                EditorGUILayout.LabelField("Specify a folder path inside of the unity project. (Starting from Assets)");
                EditorGUILayout.LabelField("Leave empty to export relative to the LDtk project's directory");
                return;
            }

            if (!GUILayout.Button("Export"))
            {
                return;
            }
            exportPath += $"/{obj.name}_Export";
            
            //first try creating dupe assets
            LDtkProjectImporter assetImporter = (LDtkProjectImporter)AssetImporter.GetAtPath(pathToObject);
            LDtkArtifactAssets artifactAssets = AssetDatabase.LoadAssetAtPath<LDtkArtifactAssets>(pathToObject);
            _clonedAssets = new LDtkNativePrefabAssets(assetImporter, artifactAssets, exportPath);
            _clonedAssets.GenerateAssets();

            CreateNativePrefab(obj, exportPath);
        }

        private void CreateNativePrefab(GameObject obj, string exportPath)
        {
            //then create prefab and replace all former prefab references with new dupes
            LDtkNativePrefabFactory prefabFactory = new LDtkNativePrefabFactory(_clonedAssets);
            GameObject nativePrefabInstance = prefabFactory.CreateNativePrefabInstance(obj);
            
            string prefabPath = $"{exportPath}/{nativePrefabInstance.name}.prefab";

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(nativePrefabInstance, prefabPath);
            if (prefab)
            {
                Debug.Log($"Exported native prefab to {prefabPath}", prefab);
            }
            else
            {
                Debug.LogError($"Failed to export prefab to {prefabPath}");
            }

            DestroyImmediate(nativePrefabInstance);
        }

        private static bool TryInvalidPass(GameObject obj)
        {
            bool invalid = false;
            
            if (obj == null)
            {
                EditorGUILayout.LabelField("Assign a GameObject to continue");
                return true;
            }

            if (!AssetDatabase.Contains(obj))
            {
                EditorGUILayout.LabelField("This GameObject is not a valid imported prefab from the Project window");
                invalid = true;
            }

            if (!obj.GetComponent<LDtkComponentProject>())
            {
                EditorGUILayout.LabelField("This GameObject is not a valid LDtk Project root");
                invalid = true;
            }

            return invalid;
        }
    }
}