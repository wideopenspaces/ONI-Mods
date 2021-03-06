﻿using Klei;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CrystalBiome
{
    public class TextureAtlasHandler
    {
        private static TextureAtlasHandler instance = null;
        private static readonly object _lock = new object();
        TextureAtlasHandler() { }

        public static TextureAtlasHandler Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new TextureAtlasHandler();
                    }
                    return instance;
                }
            }
        }

        private bool loaded = false;
        
        public TextureAtlas GetTextureAtlas(string name)
        {
            if (!loaded)
            {
                string executingAssemblyPath = Assembly.GetExecutingAssembly().Location;
                string executingAssemblyDirectory = Path.GetDirectoryName(executingAssemblyPath);
                Assets.TextureAtlases.AddRange(loadDirectory(FileSystem.Normalize(Path.Combine(executingAssemblyDirectory, "atlas"))));
                loaded = true;
            }

            return Assets.GetTextureAtlas(name);
        }

        private List<TextureAtlas> loadDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return new List<TextureAtlas>();
            }

            List<TextureAtlas> textureAtlasList = new List<TextureAtlas>();
            foreach (FileInfo file in new DirectoryInfo(directoryPath).GetFiles())
            {
                TextAsset text = null;
                Texture2D texture = null;
                foreach (UnityEngine.Object asset in AssetBundle.LoadFromFile(file.FullName).LoadAllAssets())
                {
                    if (asset is Texture2D)
                    {
                        texture = asset as Texture2D;
                    }
                    else if (asset is TextAsset)
                    {
                        text = asset as TextAsset;
                    }
                }
                if (text == null)
                {
                    DebugUtil.LogWarningArgs(string.Format("could not load atlas file, skipping {0}", file.Name));
                }
                if (texture == null)
                {
                    DebugUtil.LogWarningArgs(string.Format("could not load texture file, skipping {0}", file.Name));
                }
                TextureAtlas atlas = makeTextureAtlas(text, texture);
                atlas.name = file.Name;
                textureAtlasList.Add(atlas);
            }
            return textureAtlasList;
        }

        public TextureAtlas makeTextureAtlas(TextAsset jsonData, Texture2D texture)
        {
            TextureAtlas atlas = TextureAtlas.CreateInstance<TextureAtlas>();
            atlas.texture = texture;
            JObject parsed = JObject.Parse(jsonData.text);
            List<TextureAtlas.Item> items = new List<TextureAtlas.Item>();
            foreach (JObject jItem in parsed["Base"]["items"]["Array"])
            {
                TextureAtlas.Item item = new TextureAtlas.Item();
                item.name = jItem["data"]["name"].Value<string>();
                item.name = item.name.Substring(1, item.name.Length - 2);
                item.uvBox = new Vector4(jItem["data"]["uvBox"]["x"].Value<float>(),
                    jItem["data"]["uvBox"]["y"].Value<float>(),
                    jItem["data"]["uvBox"]["z"].Value<float>(),
                    jItem["data"]["uvBox"]["w"].Value<float>());
                List<int> indices = new List<int>();
                foreach (JObject indexItem in jItem["data"]["indices"]["Array"])
                {
                    indices.Add(indexItem["data"].Value<int>());
                }
                item.indices = indices.ToArray();
                List<Vector2> uvs = new List<Vector2>();
                foreach (JObject uvItem in jItem["data"]["uvs"]["Array"])
                {
                    uvs.Add(new Vector2(uvItem["data"]["x"].Value<float>(),
                        uvItem["data"]["y"].Value<float>()));
                }
                item.uvs = uvs.ToArray();
                List<Vector3> vertices = new List<Vector3>();
                foreach (JObject vertexItem in jItem["data"]["vertices"]["Array"])
                {
                    vertices.Add(new Vector3(vertexItem["data"]["x"].Value<float>(),
                        vertexItem["data"]["y"].Value<float>()));
                }
                item.vertices = vertices.ToArray();
                items.Add(item);
            }
            atlas.items = items.ToArray();
            return atlas;
        }
    }
}
