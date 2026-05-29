using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BreadMachineCrabMaterialSetup
{
    private const string ModelPath = "Assets/Models/BreadMachineCrab/BreadMachineCrab.fbx";
    private const string ManifestPath = "Assets/Materials/AI/BreadMachineCrab/BreadMachineCrabMaterialMap.tsv";
    private const string MaterialsFolder = "Assets/Materials/AI/BreadMachineCrab/Materials";

    [MenuItem("Tools/AI/Setup Bread Machine Crab Materials")]
    public static void Apply()
    {
        List<MaterialMapRow> rows = ReadManifest();
        if (rows.Count == 0)
        {
            Debug.LogWarning("BreadMachineCrab material setup skipped because the material map is empty.");
            return;
        }

        EnsureFolder(MaterialsFolder);
        ImportTextures(rows);
        CreateOrUpdateMaterials(rows);
        RemapModelMaterials(rows);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("BreadMachineCrab materials setup complete. Materials: " + rows.Count);
    }

    private static List<MaterialMapRow> ReadManifest()
    {
        List<MaterialMapRow> rows = new List<MaterialMapRow>();
        if (!File.Exists(ManifestPath))
        {
            return rows;
        }

        string[] lines = File.ReadAllLines(ManifestPath);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            string[] parts = lines[i].Split('\t');
            if (parts.Length < 3)
            {
                continue;
            }

            rows.Add(new MaterialMapRow(parts[0].Trim(), parts[1].Trim(), parts[2].Trim()));
        }

        return rows;
    }

    private static void ImportTextures(IEnumerable<MaterialMapRow> rows)
    {
        foreach (MaterialMapRow row in rows)
        {
            AssetDatabase.ImportAsset(row.TexturePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(row.TexturePath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                dirty = true;
            }

            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                dirty = true;
            }

            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static void CreateOrUpdateMaterials(IEnumerable<MaterialMapRow> rows)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        foreach (MaterialMapRow row in rows)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(row.TexturePath);
            if (texture == null)
            {
                Debug.LogWarning("BreadMachineCrab texture was not imported: " + row.TexturePath);
                continue;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(row.MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(row.MaterialPath)
                };
                AssetDatabase.CreateAsset(material, row.MaterialPath);
            }

            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            SetTextureIfPresent(material, "_BaseMap", texture);
            SetTextureIfPresent(material, "_MainTex", texture);
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetColorIfPresent(material, "_Color", Color.white);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.35f);
            EditorUtility.SetDirty(material);
        }
    }

    private static void RemapModelMaterials(List<MaterialMapRow> rows)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("BreadMachineCrab model importer was not found at " + ModelPath);
            return;
        }

        Dictionary<string, MaterialMapRow> rowsByName = new Dictionary<string, MaterialMapRow>();
        foreach (MaterialMapRow row in rows)
        {
            rowsByName[row.SourceMaterialName] = row;
            rowsByName[StripUnityDuplicateSuffix(row.SourceMaterialName)] = row;
        }

        int remappedCount = 0;
        Material[] importedMaterials = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().ToArray();
        foreach (Material importedMaterial in importedMaterials)
        {
            string sourceName = importedMaterial.name;
            if (!rowsByName.TryGetValue(sourceName, out MaterialMapRow row) &&
                !rowsByName.TryGetValue(StripUnityDuplicateSuffix(sourceName), out row))
            {
                continue;
            }

            Material externalMaterial = AssetDatabase.LoadAssetAtPath<Material>(row.MaterialPath);
            if (externalMaterial == null)
            {
                continue;
            }

            AssetImporter.SourceAssetIdentifier identifier =
                new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName);
            importer.AddRemap(identifier, externalMaterial);
            remappedCount++;
        }

        if (remappedCount == 0)
        {
            Debug.LogWarning("BreadMachineCrab material setup did not find imported FBX materials to remap.");
            return;
        }

        importer.SaveAndReimport();
        Debug.Log("BreadMachineCrab model material remaps applied: " + remappedCount);
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string StripUnityDuplicateSuffix(string value)
    {
        return Regex.Replace(value, @"\.\d{3}$", string.Empty);
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private readonly struct MaterialMapRow
    {
        public readonly string SourceMaterialName;
        public readonly string TexturePath;
        public readonly string MaterialPath;

        public MaterialMapRow(string sourceMaterialName, string texturePath, string materialPath)
        {
            SourceMaterialName = sourceMaterialName;
            TexturePath = texturePath;
            MaterialPath = materialPath;
        }
    }
}
