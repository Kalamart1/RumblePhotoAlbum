using MelonLoader;
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Linq;

namespace RumblePhotoAlbum;

public partial class MainClass : MelonMod
{
    // constants
    private const string UserDataPath = "UserData/RumblePhotoAlbum";
    private const string picturesFolder = "pictures";
    private const string configFile = "config.json";

    /**
    * <summary>
    * Creates the necessary folders in UserData if they don't exist.
    * </summary>
    */
    private static void EnsureUserDataFolders()
    {
        string picturesPath = Path.Combine(Application.dataPath, "..", UserDataPath, picturesFolder);
        Directory.CreateDirectory(UserDataPath);
        Directory.CreateDirectory(picturesPath);
    }

    /**
    * <summary>
    * Reads the config file, updates it if necessary (missing pictures in the stash,
    * extra images that don't exist on disk), and creates the objects in the scene.
    * </summary>
    */
    private static void LoadAlbum(string sceneName)
    {
        string fullPath = Path.Combine(Application.dataPath, "..", UserDataPath, configFile);

        try
        {
            JObject root = null;
            if (!File.Exists(fullPath))
            {
                LogWarn($"Creating new configuration file at: {fullPath}.");
                root = new JObject();
            }
            else
            {
                string json = File.ReadAllText(fullPath);
                root = JObject.Parse(json);
            }


            // if the field with this scen name doesn't exist, create it
            if (!root.TryGetValue(sceneName, out JToken sceneToken) || sceneToken.Type != JTokenType.Object)
            {
                LogWarn($"No valid entry found for scene \"{sceneName}\". Creating an empty object.");
                sceneToken = new JObject();
                root[sceneName] = sceneToken;
            }

            var sceneObj = (JObject)sceneToken;

            // Ensure "stash" and "album" exist
            JArray stash = sceneObj["stash"] as JArray ?? new JArray();
            JArray album = sceneObj["album"] as JArray ?? new JArray();


            // Normalize paths in JSON to match disk
            HashSet<string> stashSet = new(stash.Select(s => s.ToString()));
            HashSet<string> albumSet = new(album
                .Where(e => e["path"] != null)
                .Select(e => e["path"].ToString())
            );

            photoAlbum = new GameObject();
            photoAlbum.name = "PhotoAlbum";

            // Validate album entries
            var cleanedAlbum = new JArray();
            foreach (var entry in album)
            {
                try
                {
                    FramedPicture framedPicture = ParsePictureData(entry);

                    if (framedPicture is null)
                        continue;

                    if (!File.Exists(framedPicture.path))
                    {
                        // if the path is not absolute, assume it's relative to the pictures folder
                        string globalPicturePath = Path.Combine(Application.dataPath, "..", UserDataPath, picturesFolder, framedPicture.path);
                        if (!File.Exists(globalPicturePath))
                        {
                            LogWarn($"Removed missing file: {framedPicture.path}");
                            continue;
                        }
                        else
                        {
                            framedPicture.path = globalPicturePath;
                            cleanedAlbum.Add(entry);
                        }
                    }

                    GameObject obj = CreatePictureBlock(framedPicture);
                }
                catch (Exception ex)
                {
                    LogError($"Failed to parse entry: {ex.Message}");
                    continue;
                }
            }


            // Validate stash entries
            var cleanedStash = new HashSet<string>();
            foreach (var entry in stash)
            {
                string picturePath = entry.ToString();
                if (!File.Exists(picturePath))
                {
                    // if the path is not absolute, assume it's relative to the pictures folder
                    string globalPicturePath = Path.Combine(Application.dataPath, "..", UserDataPath, picturesFolder, picturePath);
                    if (!File.Exists(globalPicturePath))
                    {
                        LogWarn($"Removed missing file from stash: {picturePath}");
                        continue;
                    }
                    cleanedStash.Add(picturePath);
                }
            }

            // Get the list of images that are currently used in the album and/or stash
            var usedImages = new HashSet<string>(cleanedStash);
            usedImages.UnionWith(albumSet);

            // Check all image files in the "pictures" folder
            string picturesPath = Path.Combine(Application.dataPath, "..", UserDataPath, picturesFolder);
            var imageFiles = new HashSet<string>(
                Directory.Exists(picturesPath)
                    ? Directory.GetFiles(picturesPath)
                              .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg"))
                              .Select(f => f)
                    : Enumerable.Empty<string>()
            );

            foreach (var file in imageFiles)
            {
                string fileName = Path.GetFileName(file);
                if (!usedImages.Contains(fileName))
                {
                    cleanedStash.Add(fileName);
                    Log($"Adding missing image {fileName} to stash.");
                }
            }

            // Rebuild updated stash/album
            sceneObj["stash"] = new JArray(cleanedStash);
            sceneObj["album"] = cleanedAlbum;

            // Save back the modified config
            File.WriteAllText(fullPath, root.ToString(Formatting.Indented));
        }
        catch (Exception ex)
        {
            LogError($"Failed to load or parse {configFile}: {ex.Message}");
        }
    }

    /**
    * <summary>
    * Creates a Texture2D from an image file, blending it on top of a predetermined
    * color in order to remove alpha transparency. This way it looks like it has
    * transparency when superposed on top of a colored background.
    * </summary>
    */
    private static FramedPicture ParsePictureData(JToken pictureData)
    {
        if (pictureData == null || pictureData.Type != JTokenType.Object)
            throw new ArgumentException("Invalid JSON object for FramedPicture.");

        JObject obj = (JObject)pictureData;

        FramedPicture framedPicture = new FramedPicture();

        // Required fields
        framedPicture.path = obj.Value<string>("path");
        if (string.IsNullOrEmpty(framedPicture.path))
        {
            throw new ArgumentException($"Missing field \"path\"");
        }

        framedPicture.position = ParseVector3(obj["position"], "position");
        framedPicture.rotation = ParseVector3(obj["rotation"], "rotation");

        // Optional fields with defaults
        framedPicture.width = obj.Value<float?>("width") ?? 0;
        framedPicture.height = obj.Value<float?>("height") ?? 0;
        framedPicture.padding = 2 * (obj.Value<float?>("padding") ?? defaultPadding);
        framedPicture.thickness = obj.Value<float?>("thickness") ?? defaultThickness;

        framedPicture.color = defaultColor; // Default color
        if (obj.TryGetValue("color", out JToken colorToken))
            framedPicture.color = ParseColor(colorToken);

        return framedPicture;
    }

    /**
    * <summary>
    * Parses a json token as a Vector3
    * </summary>
    */
    private static Vector3 ParseVector3(JToken token, string fieldName)
    {
        if (token == null)
        {
            throw new ArgumentException($"Missing field \"{fieldName}\"");
        }

        if ( token.Type != JTokenType.Array)
        {
            throw new ArgumentException($"{fieldName}' must be an array [x, y, z] (got {token.ToString()})");
        }

        float[] values = token.ToObject<float[]>();
        if (values.Length != 3)
        {
            throw new ArgumentException($"{fieldName}' must have exactly 3 elements (got {token.ToString()})");
        }

        return new Vector3(values[0], values[1], values[2]);
    }

    /**
    * <summary>
    * Parses a json token as a color, either as a hex string or an array of floats.
    * </summary>
    */
    private static Color ParseColor(JToken token)
    {
        if (token.Type == JTokenType.Array)
        {
            float[] c = token.ToObject<float[]>();
            if (c.Length >= 3)
            {
                return new Color(c[0], c[1], c[2], c.Length >= 4 ? c[3] : 1.0f);
            }
        }
        else if (token.Type == JTokenType.String)
        {
            string hex = token.ToString();
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }
        }

        throw new ArgumentException("FramedPicture: 'color' must be [r,g,b,a?] or hex string.");
    }


    /**
    * <summary>
    * Creates the GameObject for a framed picture in the scene.
    * </summary>
    */
    private static GameObject CreatePictureBlock(FramedPicture framedPicture)
    {
        // Load texture from image file, with the frame's color as background
        Texture2D imageTexture = LoadFlattenedTexture(framedPicture.path, framedPicture.color);
        
        float aspectRatio = (float)imageTexture.height / imageTexture.width;
        if (framedPicture.width == 0 && framedPicture.height == 0)
        {
            if (aspectRatio > 1) // vertical image
            {
                framedPicture.height = defaultSize;
            }
            else // horizontal image
            {
                framedPicture.width = defaultSize;
            }
        }
        if (framedPicture.width == 0)
        {
            framedPicture.width = (framedPicture.height - framedPicture.padding) / aspectRatio + framedPicture.padding;
        }
        else
        {
            framedPicture.height = (framedPicture.width - framedPicture.padding) * aspectRatio + framedPicture.padding;
        }

        float imageOffset = 0.001f; // put the image 1mm in front of the frame

        GameObject obj = new GameObject();
        obj.transform.position = framedPicture.position;
        obj.transform.rotation = Quaternion.Euler(framedPicture.rotation);
        obj.name = $"PictureBlock: {Path.GetFileNameWithoutExtension(framedPicture.path)}";
        obj.transform.SetParent(photoAlbum.transform, true);

        // Create frame
        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "frame";
        frame.transform.SetParent(obj.transform, false);
        frame.transform.localScale = new Vector3(framedPicture.width, framedPicture.height, framedPicture.thickness);
        frame.transform.localPosition = new Vector3(0f, 0f, framedPicture.thickness / 2);

        Renderer frameRenderer = frame.GetComponent<Renderer>();
        frameRenderer.material.shader = Shader.Find("Shader Graphs/RUMBLE_Prop");
        frameRenderer.material.SetColor("_Overlay", framedPicture.color);

        // Create quad with the image on it
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "picture";
        quad.transform.SetParent(obj.transform, false);
        quad.transform.localScale = new Vector3(framedPicture.width - framedPicture.padding,
                                                 framedPicture.height - framedPicture.padding,
                                                 1f);

        // Picture positioned 1mm in front of the frame (local +Z)
        quad.transform.localPosition = new Vector3(0f, 0f, -imageOffset);
        quad.transform.localRotation = Quaternion.identity;

        Renderer quadRenderer = quad.GetComponent<Renderer>();
        quadRenderer.material.shader = Shader.Find("Shader Graphs/RUMBLE_Prop");
        quadRenderer.material.SetTexture("_Albedo", imageTexture);

        framedPicture.obj = obj;

        return obj;
    }

    /**
    * <summary>
    * Create a Texture2D from an image file, blending it on top of a predetermined
    * color in order to remove alpha transparency. This way it looks like it has
    * transparency when superposed on top of a colored background.
    * </summary>
    */
    public static Texture2D LoadFlattenedTexture(string path, Color background)
    {
        // Load image into Texture2D with alpha channel
        byte[] data = File.ReadAllBytes(path);
        Texture2D input = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        input.LoadImage(data);

        // Get all pixels of the image at once
        Color[] inputPixels = input.GetPixels();
        Color[] outputPixels = new Color[inputPixels.Length];

        for (int i = 0; i < inputPixels.Length; i++)
        {
            Color src = inputPixels[i];
            float a = src.a;

            // Alpha blend image over background, so it looks like
            // it has transparency over a similar background
            outputPixels[i] = new Color(
                src.r * a + background.r * (1f - a),
                src.g * a + background.g * (1f - a),
                src.b * a + background.b * (1f - a)
            );
        }

        // Prepare output texture (without transparency)
        Texture2D output = new Texture2D(input.width, input.height, TextureFormat.RGB24, false);
        output.SetPixels(outputPixels);
        output.Apply();

        return output;
    }

}
