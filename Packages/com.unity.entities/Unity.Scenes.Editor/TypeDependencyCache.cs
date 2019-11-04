#define USE_FILE_BASED_TYPECACHE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
using AssetImportContext = UnityEditor.Experimental.AssetImporters.AssetImportContext;

#if ENABLE_SUBSCENE_IMPORTER

#if USE_FILE_BASED_TYPECACHE
[InitializeOnLoad]
public class TypeDependencyCache : MonoBehaviour 
{
    const string TypeCacheDir = "Assets/TypeDependencyCache";

    static TypeDependencyCache()
    {
        TypeManager.Initialize();
        UpdateAllFiles();
    }

    static HashSet<string> AllTypesInCache()
    {
        var set = new HashSet<string>();

        if (!Directory.Exists(TypeCacheDir))
            return set;

        foreach (var file in Directory.EnumerateFiles(TypeCacheDir, "*.hash", SearchOption.TopDirectoryOnly))
        {
            set.Add( file.Replace("\\", "/").ToLower());
        }

        return set;
    }
    
    static bool UpdateAllFiles()
    {
        var currentSet = AllTypesInCache();
        TypeManager.Initialize();
        int typeCount = TypeManager.GetTypeCount();
        bool changed = false;
        for (int i = 1; i < typeCount; ++i)
        {
            var typeInfo = TypeManager.GetTypeInfo(i);
            currentSet.Remove(TypePath(typeInfo.Type).ToLower());
            if (UpdateTypeInfoFile(typeInfo))
                changed = true;
        }

        foreach (var path in currentSet)
        {
            File.Delete(path);
            File.Delete(path+".meta");
            changed = true;
        }
        
        return changed;
    }

    public static void AddDependency(AssetImportContext ctx, ComponentType type)
    {
        var typeHashPath = TypePath(type.GetManagedType());
        ctx.DependsOnSourceAsset(typeHashPath);
    }

    static string TypePath(Type type) => $"{TypeCacheDir}/{type.FullName}.hash";
    
    private static unsafe bool UpdateTypeInfoFile(TypeManager.TypeInfo typeInfo)
    {
        var hash = typeInfo.StableTypeHash;

        if (!Directory.Exists(TypeCacheDir))
            Directory.CreateDirectory(TypeCacheDir);

        string typeName = typeInfo.Type.FullName;
        string typePath = TypePath(typeInfo.Type);
        var  hashBytes = BitConverter.GetBytes(hash);
        if (File.Exists(typePath) && File.ReadAllBytes(typePath).SequenceEqual(hashBytes))
            return false;
        
        Hash128 metaGuid = new Hash128();
        fixed (char* buf = typeName)
        {
            metaGuid.Value.x = math.hash(buf, sizeof(char)*typeName.Length);
            metaGuid.Value.y = math.hash(buf, sizeof(char)*typeName.Length, 0x96a755e2);
            metaGuid.Value.z = math.hash(buf, sizeof(char)*typeName.Length, 0x4e936206);
            metaGuid.Value.w = math.hash(buf, sizeof(char)*typeName.Length, 0xac602639);
        }
        File.WriteAllBytes(typePath, hashBytes);
        File.WriteAllText(typePath + ".meta", 
            $"fileFormatVersion: 2\nguid: {metaGuid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
        return true;
    }
}
#else
[InitializeOnLoad]
public class TypeDependencyCache : MonoBehaviour
{
    static TypeDependencyCache()
    {
        TypeManager.Initialize();
        UnityEditor.Experimental.AssetDatabaseExperimental.UnregisterCustomDependencyPrefixFilter("DOTSType/");
        int typeCount = TypeManager.GetTypeCount();

        for (int i = 1; i < typeCount; ++i)
        {
            var typeInfo = TypeManager.GetTypeInfo(i);
            var hash = typeInfo.StableTypeHash;
            UnityEditor.Experimental.AssetDatabaseExperimental.RegisterCustomDependency(TypeString(typeInfo.Type), new UnityEngine.Hash128(hash, hash));
        }
    }

    public static void AddDependency(AssetImportContext ctx, ComponentType type)
    {
        var typeString = TypeString(type.GetManagedType());
        ctx.DependsOnCustomDependency(typeString);
    }

    static string TypeString(Type type) => $"DOTSType/{type.FullName}";
}
#endif
#endif
