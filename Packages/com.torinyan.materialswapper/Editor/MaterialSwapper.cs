#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace com.torinyan.MatSwap.Editor
{
    public class MaterialSwapper : EditorWindow
    {
        private class MaterialBindings
        {
            public class AddonInfo
            {
                public string Name;
                public string PrefabPath;
            }
            public class BindingInfo
            {
                public string PrefabPath;
                public string ObjectPath;
                public string[] Materials;
            }

            public string Name;
            public AddonInfo[] Addons;
            public BindingInfo[] Bindings;
        }

        private const string CMaterialBindingsPath = "Packages/com.torinyan.materialswapper/Resources/";
        private const string CJsonSearch = "*.json";

        private static readonly Vector2 _windowSizeDefault = new(500f, 71f);
        private static readonly Vector2 _guiElementSpacing = new(0f, 21f);
        private Vector2 _windowSize;

        private readonly Dictionary<string, MaterialBindings> _materialOptions = new();
        private readonly Dictionary<string, (bool Enabled, GameObject Prefab)> _addonPrefabs = new();
        private readonly List<VRCAvatarDescriptor> _avatars = new();
        private string[] _avatarNames = Array.Empty<string>();
        private int _selectedAvatarId = 0;
        private VRCAvatarDescriptor _selectedAvatar;

        [MenuItem("Tools/Torinyan/Material Swapper")]
        public static void ShowWindow() =>
            GetWindow<MaterialSwapper>(true, "[Torinyan] Material Swapper", true);

        void OnEnable() =>
            UpdateOptions();

        void OnHierarchyChange() =>
            UpdateOptions();

        void OnGUI()
        {
            minSize = _windowSize;
            maxSize = _windowSize;

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            {
                _selectedAvatarId = EditorGUILayout.Popup("Avatar Select", _selectedAvatarId, _avatarNames);
            }
            if (EditorGUI.EndChangeCheck() && _avatars.Count > 0)
                _selectedAvatar = _avatars[_selectedAvatarId];

            _selectedAvatar = EditorGUILayout.ObjectField("Avatar Object", _selectedAvatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;

            EditorGUILayout.Space();

            for (int i = 0; i < _addonPrefabs.Count; i++)
            {
                var (addonName, addonInfo) = _addonPrefabs.ElementAt(i);
                addonInfo.Enabled = EditorGUILayout.ToggleLeft($"Add {addonName}", addonInfo.Enabled);
                _addonPrefabs[addonName] = addonInfo;
            }

            EditorGUILayout.Space(12f);
            EditorGUI.BeginDisabledGroup(
                _avatars.Count <= 0 ||
                _selectedAvatarId > _avatars.Count ||
                _avatars[_selectedAvatarId] == null
            );
            {
                foreach (var (name, _) in _materialOptions)
                {
                    if (GUILayout.Button($"Set `{name}` Materials"))
                        PerformSwap(name);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void UpdateOptions()
        {
            _addonPrefabs.Clear();
            _materialOptions.Clear();

            foreach (var jsonFile in Directory.EnumerateFiles(CMaterialBindingsPath, CJsonSearch, SearchOption.TopDirectoryOnly))
            {
                var matBinding = JsonConvert.DeserializeObject<MaterialBindings>(File.ReadAllText(jsonFile));
                _materialOptions.Add(matBinding.Name, matBinding);
            }

            foreach (var (_, info) in _materialOptions)
            {
                foreach (var addon in info.Addons)
                {
                    if (!File.Exists(addon.PrefabPath))
                        continue;

                    _addonPrefabs.Add(
                        addon.Name,
                        (false, AssetDatabase.LoadAssetAtPath<GameObject>(addon.PrefabPath))
                    );
                }
            }

            _windowSize = _windowSizeDefault + (_guiElementSpacing * (_materialOptions.Count + _addonPrefabs.Count));
            UpdateAvatarList();
        }

        private void UpdateAvatarList()
        {
            var oldAvatarName = _selectedAvatar == null ? null : _selectedAvatar.gameObject.name;
            _avatars.Clear();
            List<GameObject> roots = new();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene == null) continue;

                roots.AddRange(scene.GetRootGameObjects());
            }

            var names = new List<string>();

            foreach (var rootGO in roots)
            {
                if (rootGO.TryGetComponent<VRCAvatarDescriptor>(out var avatar))
                {
                    _avatars.Add(avatar);
                    names.Add(rootGO.name);
                }
            }

            if (!string.IsNullOrWhiteSpace(oldAvatarName))
            {
                var oldNameIdx = names.FindIndex(x => x.Equals(oldAvatarName, StringComparison.InvariantCulture));
                _selectedAvatarId = oldNameIdx > -1 ? oldNameIdx : 0;
            }
            else
                _selectedAvatarId = 0;

            if (_avatars.Count > 0)
            {
                _avatarNames = names.ToArray();
                _selectedAvatar = _avatars[_selectedAvatarId];
            }
            else
            {
                _avatarNames = new[] { "No Avatars Detected" };
                _selectedAvatar = null;
            }
        }

        private void PerformSwap(string matType)
        {
            var bindings = _materialOptions[matType].Bindings;
            var avatarTransform = _selectedAvatar.transform;

            Undo.SetCurrentGroupName($"[Torinyan] Perform material swap `{matType}`");
            int group = Undo.GetCurrentGroup();

            foreach (var (addonName, addonInfo) in _addonPrefabs)
            {
                if (addonInfo.Enabled && addonInfo.Prefab != null && avatarTransform.Find(addonInfo.Prefab.name) == null)
                {
                    var newGO = PrefabUtility.InstantiatePrefab(addonInfo.Prefab) as GameObject;
                    Undo.RegisterCreatedObjectUndo(newGO, $"Create new {addonName} object");
                    Undo.RecordObject(newGO, $"Reparent new {addonName} object");
                    newGO.transform.SetParent(avatarTransform);
                }
            }

            foreach(var binding in bindings)
            {
                if (!string.IsNullOrWhiteSpace(binding.PrefabPath) && !File.Exists(binding.PrefabPath))
                    continue;

                var curObj = avatarTransform.Find(binding.ObjectPath);

                if (curObj == null || !curObj.TryGetComponent<Renderer>(out var renderer))
                    continue;

                var matLen = binding.Materials.Length;
                var materials = new List<Material>(matLen);

                for (int i = 0; i < matLen; i++)
                {
                    if (!File.Exists(binding.Materials[i]))
                    {
                        materials.Add(renderer.sharedMaterials[i]);
                        continue;
                    }

                    materials.Add(AssetDatabase.LoadAssetAtPath<Material>(binding.Materials[i]));
                }

                Undo.RecordObject(renderer, $"Swapping materials for `{binding.ObjectPath}`");
                renderer.SetSharedMaterials(materials);

                if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

                EditorUtility.SetDirty(renderer);
            }

            Undo.CollapseUndoOperations(group);
        }
    }
}
#endif
