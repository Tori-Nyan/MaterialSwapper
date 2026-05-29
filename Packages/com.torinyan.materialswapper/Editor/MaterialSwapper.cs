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
                    MaterialSwapper.Log($"Error creating the Material Bindings folder: {ex.Message}", LogType.Exception);
                    return;
                }
            }

            if (!File.Exists(CInitializedFile)) {
                try {
                    File.WriteAllText(CInitializedFile, string.Empty);
                } catch (Exception ex) {
                    MaterialSwapper.Log($"Error copying default binhdings to Material Bindings folder: {ex.Message}", LogType.Exception);
                    return;
                }

                AssetDatabase.StartAssetEditing();

                foreach (var (srcPath, destPath) in CDefaultMappings) {
                    try {
                        if (File.Exists(srcPath) && !File.Exists(destPath))
                            AssetDatabase.CopyAsset(srcPath, destPath);
                    } catch (Exception ex) {
                        MaterialSwapper.Log($"Error copying default binhdings to Material Bindings folder: {ex.Message}", LogType.Exception);
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
            GetWindow<MaterialSwapper>(true, "[Torinyan] Material Swapper v1.0.6", true);

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
                _selectedAvatar = _avatars[_selectedAvatarId - 1];

            EditorGUI.BeginChangeCheck();
            {
                _selectedAvatar = EditorGUILayout.ObjectField("Avatar Object", _selectedAvatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            }
            if (EditorGUI.EndChangeCheck()) {
                if (_selectedAvatar == null) {
                    _selectedAvatarId = 0;
                } else {
                    _selectedAvatarId = _avatars.FindIndex(x =>
                        x.gameObject.name.Equals(_selectedAvatar.gameObject.name, StringComparison.Ordinal)
                    ) + 1; // +1 so if we didn't find it (-1), we select custom (0)
                }
            }

            if (_addonPrefabs.Count > 0)
                EditorGUILayout.Space();

            for (int i = 0; i < _addonPrefabs.Count; i++) {
                var (addonPrefabPath, addonInfo) = _addonPrefabs.ElementAt(i);
                addonInfo.Enabled = EditorGUILayout.ToggleLeft($"Add {addonInfo.info.Name}", addonInfo.Enabled);
                _addonPrefabs[addonPrefabPath] = addonInfo;
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

        internal static void Log(string msg, LogType logType = LogType.Log) {
            var outMsg = $"{logType} - <color=#ff6961ff>[MatSwap]</color> {msg}";

            switch (logType) {
                case LogType.Exception:
                case LogType.Error: {
                    Debug.LogError(outMsg);
                    return;
                }
                case LogType.Assert: {
                    Debug.LogAssertion(outMsg);
                    return;
                }
                case LogType.Warning: {
                    Debug.LogWarning(outMsg);
                    return;
                }
                default: {
                    Debug.Log(outMsg);
                    return;
                }
            }
        }

        private void UpdateOptions() {
            var oldAddons = _addonPrefabs.Keys.ToArray();
            List<string> foundAddons = new();
            _materialOptions.Clear();

            foreach (var jsonFile in Directory.EnumerateFiles(CAssetPath, CJsonSearch, SearchOption.TopDirectoryOnly)) {
                // We skip the template file
                if (jsonFile.Contains(CTemplateFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = JsonConvert.DeserializeObject<MaterialBindings>(File.ReadAllText(jsonFile));

                if (!string.IsNullOrWhiteSpace(info.DependsOn) && !File.Exists(info.DependsOn))
                    continue;

                _materialOptions.Add(info);
            }

            foreach (var info in _materialOptions) {
                foreach (var addon in info.Addons) {
                    if (File.Exists(addon.PrefabPath)) {
                        foundAddons.Add(addon.PrefabPath);

                        if (!_addonPrefabs.ContainsKey(addon.PrefabPath))
                            _addonPrefabs.Add(addon.PrefabPath, (false, addon));
                    }
                }
            }

            // Remove any addon that no longer exists
            foreach (var item in oldAddons.Where(x => !foundAddons.Contains(x))) {
                _addonPrefabs.Remove(item);
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

            if (string.IsNullOrWhiteSpace(oldAvatarName)) {
                _selectedAvatarId = 0;
            } else {
                var oldNameIdx = names.FindIndex(x => x.Equals(oldAvatarName, StringComparison.Ordinal));
                _selectedAvatarId = oldNameIdx > -1 ? oldNameIdx : 0;
            }

            _avatarNames = names.ToArray();

            if (_selectedAvatarId > 0)
                _selectedAvatar = _avatars[_selectedAvatarId - 1];
        }

        private void PerformSwap(MaterialBindings matType) {
            if (_selectedAvatar == null) {
                Debug.LogError("Target avatar is NULL");
                return;
            }

            var avatarTransform = _selectedAvatar.transform;

            Undo.SetCurrentGroupName($"[Torinyan] Perform material swap `{matType.Name}`");
            int group = Undo.GetCurrentGroup();
            AddSelectedAddons(avatarTransform);

            foreach (var binding in matType.Bindings) {
                if (!string.IsNullOrWhiteSpace(binding.PrefabPath) && !File.Exists(binding.PrefabPath))
                    continue; // Silently skip, no need to spam the logs with this

                var curObj = avatarTransform.Find(binding.ObjectPath);

                if (curObj == null || !curObj.TryGetComponent<Renderer>(out var renderer))
                    continue; // Silently skip, no need to spam the logs with this

                var oldMaterials = renderer.sharedMaterials;
                var matLen = Math.Max(binding.Materials.Length, oldMaterials.Length);
                var materials = new List<Material>(matLen);

                for (int i = 0; i < matLen; i++) {
                    // Little bit of a safety-net to avoid out-of-bounds
                    if (i >= binding.Materials.Length || !File.Exists(binding.Materials[i])) {
                        if (i < oldMaterials.Length)
                            materials.Add(oldMaterials[i]);

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

        private void AddSelectedAddons(Transform targetAvatar) {
            if (targetAvatar == null) {
                Log("Target avatar is NULL", LogType.Error);
                return;
            }

            foreach (var (_, addonInfo) in _addonPrefabs) {
                if (addonInfo.Enabled) {
                    var installAtTransform = string.IsNullOrWhiteSpace(addonInfo.info.InstallAt)
                        ? targetAvatar
                        : targetAvatar.Find(addonInfo.info.InstallAt);

                    if (installAtTransform == null) {
                        Log($"Addon `{addonInfo.info.Name}` InstallAt location `{addonInfo.info.InstallAt}` not found, skipping", LogType.Warning);
                        continue;
                    }

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(addonInfo.info.PrefabPath);

                    if (prefab == null) {
                        Log($"Addon `{addonInfo.info.Name}` failed to load, skipping", LogType.Warning);
                        continue;
                    }

                    if (installAtTransform.Find(prefab.name) != null) {
                        Log($"Addon `{addonInfo.info.Name}` already exists, skipping", LogType.Warning);
                        continue;
                    }

                    var newGO = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    Undo.RegisterCreatedObjectUndo(newGO, $"Create new {addonInfo.info.Name} object");
                    Undo.RecordObject(newGO, $"Reparent new {addonInfo.info.Name} object");

                    newGO.transform.SetParent(installAtTransform);
                    // Fix positional offset of the avatar.  For some reason Unity doesn't auto-fix this..
                    newGO.transform.position += targetAvatar.position;
                }
            }
        }
    }
}
#endif
