/*
* VRChat Avatar Material Swapper
* Copyright (C) 2026  Torinyan
*
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

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
    internal class MatSwapEnsureAssetFolderExists : AssetPostprocessor
    {
        private const string CInitializedFile = "ProjectSettings/_torinyan_matswap.lock";
        private const string CResourcePath = "Packages/com.torinyan.materialswapper/Resources/";
        private readonly static Dictionary<string, string> CDefaultMappings = new() {
            { $"{CResourcePath}BAN_Default.json", $"{MaterialSwapper.CAssetPath}BAN_Default.json" },
            { $"{CResourcePath}{MaterialSwapper.CTemplateFileName}", $"{MaterialSwapper.CAssetPath}{MaterialSwapper.CTemplateFileName}" }
        };

        static void OnPostprocessAllAssets(string[] imports, string[] deletes, string[] moves, string[] movedFromAssets, bool domainReload) {
            if (!Directory.Exists(MaterialSwapper.CAssetPath)) {
                try {
                    File.Delete(CInitializedFile);
                    Directory.CreateDirectory(MaterialSwapper.CAssetPath);
                } catch (Exception ex) {
                    Debug.Log($"Error creating the Material Bindings folder: {ex.Message}");
                    return;
                }
            }

            if (!File.Exists(CInitializedFile)) {
                try {
                    File.WriteAllText(CInitializedFile, string.Empty);
                } catch (Exception ex) {
                    Debug.Log($"Error copying default binhdings to Material Bindings folder: {ex.Message}");
                    return;
                }

                AssetDatabase.StartAssetEditing();

                foreach (var (srcPath, destPath) in CDefaultMappings) {
                    try {
                        if (File.Exists(srcPath) && !File.Exists(destPath))
                            AssetDatabase.CopyAsset(srcPath, destPath);
                    } catch (Exception ex) {
                        Debug.Log($"Error copying default binhdings to Material Bindings folder: {ex.Message}");
                    }
                }

                AssetDatabase.StopAssetEditing();
            }
        }
    }

    public class MaterialSwapper : EditorWindow
    {
        internal const string CAssetPath = "Assets/[Torinyan] Tools/MaterialSwapper/";
        internal const string CTemplateFileName = "Template.json";
        private const string CJsonSearch = "*.json";

        private class MaterialBindings
        {
            public class AddonInfo
            {
                [JsonProperty(Required = Required.Always)]
                public string Name;
                [JsonProperty(Required = Required.Always)]
                public string PrefabPath;
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public string InstallAt = string.Empty;
            }

            public class BindingInfo
            {
                [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
                public string PrefabPath = string.Empty;
                [JsonProperty(Required = Required.Always)]
                public string ObjectPath;
                [JsonProperty(Required = Required.Always)]
                public string[] Materials;
            }

            [JsonProperty(Required = Required.Always)]
            public string Name;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public string DependsOn = string.Empty;
            [JsonProperty(Required = Required.Always)]
            public AddonInfo[] Addons;
            [JsonProperty(Required = Required.Always)]
            public BindingInfo[] Bindings;
        }

        private static readonly Vector2 _windowSizeDefault = new(500f, 71f);
        private static readonly Vector2 _guiElementSpacing = new(0f, 21f);
        private Vector2 _windowSize;

        private readonly List<MaterialBindings> _materialOptions = new();
        private readonly Dictionary<string, (bool Enabled, MaterialBindings.AddonInfo info)> _addonPrefabs = new();
        private readonly List<VRCAvatarDescriptor> _avatars = new();
        private string[] _avatarNames = new[] { "Custom (Drag&Drop below)" };
        private int _selectedAvatarId = 0;
        private VRCAvatarDescriptor _selectedAvatar;

        [MenuItem("Tools/Torinyan/Material Swapper")]
        public static void ShowWindow() =>
            GetWindow<MaterialSwapper>(true, "[Torinyan] Material Swapper", true);

        void OnEnable() =>
            UpdateOptions();

        void OnHierarchyChange() =>
            UpdateOptions();

        void OnGUI() {
            minSize = _windowSize;
            maxSize = _windowSize;

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            {
                _selectedAvatarId = EditorGUILayout.Popup("Avatar Select", _selectedAvatarId, _avatarNames);
            }
            if (EditorGUI.EndChangeCheck() && _selectedAvatarId > 0)
                _selectedAvatar = _avatars[_selectedAvatarId];

            EditorGUI.BeginChangeCheck();
            {
                _selectedAvatar = EditorGUILayout.ObjectField("Avatar Object", _selectedAvatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            }
            if (EditorGUI.EndChangeCheck()) {
                if (_selectedAvatar == null) {
                    _selectedAvatarId = 0;
                } else {
                    int selectedIdx = _avatars.FindIndex(x => x.name.Equals(_selectedAvatar.name, StringComparison.InvariantCulture));
                    _selectedAvatarId = selectedIdx > -1 ? selectedIdx : 0;
                }
            }

            EditorGUILayout.Space();

            for (int i = 0; i < _addonPrefabs.Count; i++) {
                var (addonName, addonInfo) = _addonPrefabs.ElementAt(i);
                addonInfo.Enabled = EditorGUILayout.ToggleLeft($"Add {addonName}", addonInfo.Enabled);
                _addonPrefabs[addonName] = addonInfo;
            }

            EditorGUILayout.Space(12f);
            EditorGUI.BeginDisabledGroup(_selectedAvatar == null);
            {
                foreach (var info in _materialOptions) {
                    if (GUILayout.Button($"Set `{info.Name}` Materials"))
                        PerformSwap(info);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void UpdateOptions() {
            _addonPrefabs.Clear();
            _materialOptions.Clear();

            foreach (var jsonFile in Directory.EnumerateFiles(CAssetPath, CJsonSearch, SearchOption.TopDirectoryOnly)) {
                // We skip the template file
                if (jsonFile.Contains(CTemplateFileName, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                var info = JsonConvert.DeserializeObject<MaterialBindings>(File.ReadAllText(jsonFile));

                if (!string.IsNullOrWhiteSpace(info.DependsOn) && !File.Exists(info.DependsOn))
                    continue;

                _materialOptions.Add(info);
            }

            foreach (var info in _materialOptions) {
                foreach (var addon in info.Addons) {
                    if (File.Exists(addon.PrefabPath))
                        _addonPrefabs.Add(addon.Name, (false, addon));
                }
            }

            _windowSize = _windowSizeDefault + (_guiElementSpacing * (_materialOptions.Count + _addonPrefabs.Count));
            UpdateAvatarList();
        }

        private void UpdateAvatarList() {
            var oldAvatarName = _selectedAvatar == null ? null : _selectedAvatar.gameObject.name;
            _avatars.Clear();
            List<GameObject> roots = new();

            for (int i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);

                if (scene == null)
                    continue;

                roots.AddRange(scene.GetRootGameObjects());
            }

            var names = new List<string>() { "Custom (Drag&Drop below)" };

            foreach (var rootGO in roots) {
                if (rootGO.TryGetComponent<VRCAvatarDescriptor>(out var avatar)) {
                    _avatars.Add(avatar);
                    names.Add(rootGO.name);
                }
            }

            if (!string.IsNullOrWhiteSpace(oldAvatarName)) {
                var oldNameIdx = names.FindIndex(x => x.Equals(oldAvatarName, StringComparison.InvariantCulture));
                _selectedAvatarId = oldNameIdx > -1 ? oldNameIdx : 0;
            } else
                _selectedAvatarId = 0;

            _avatarNames = names.ToArray();

            if (_selectedAvatarId > 0)
                _selectedAvatar = _avatars[_selectedAvatarId];
        }

        private void PerformSwap(MaterialBindings matType) {
            if (_selectedAvatar == null)
                return;

            var avatarTransform = _selectedAvatar.transform;

            Undo.SetCurrentGroupName($"[Torinyan] Perform material swap `{matType.Name}`");
            int group = Undo.GetCurrentGroup();

            foreach (var (addonName, addonInfo) in _addonPrefabs) {
                if (addonInfo.Enabled) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(addonInfo.info.PrefabPath);

                    if (prefab == null || avatarTransform.Find(prefab.name) != null)
                        return;

                    var newGO = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    Undo.RegisterCreatedObjectUndo(newGO, $"Create new {addonName} object");
                    Undo.RecordObject(newGO, $"Reparent new {addonName} object");

                    var parent = string.IsNullOrWhiteSpace(addonInfo.info.InstallAt)
                        ? avatarTransform
                        : avatarTransform.Find(addonInfo.info.InstallAt);
                    newGO.transform.SetParent(parent);
                    newGO.transform.position += avatarTransform.position;
                }
            }

            foreach (var binding in matType.Bindings) {
                if (!string.IsNullOrWhiteSpace(binding.PrefabPath) && !File.Exists(binding.PrefabPath))
                    continue;

                var curObj = avatarTransform.Find(binding.ObjectPath);

                if (curObj == null || !curObj.TryGetComponent<Renderer>(out var renderer))
                    continue;

                var matLen = binding.Materials.Length;
                var materials = new List<Material>(matLen);

                for (int i = 0; i < matLen; i++) {
                    if (!File.Exists(binding.Materials[i])) {
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
