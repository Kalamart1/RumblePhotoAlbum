using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RumblePhotoAlbum;

public partial class MainClass : MelonMod
{
    // constants
    private const string UserDataPath = "UserData/RumblePhotoAlbum";
    private const string picturesFolder = "pictures";
    private const string configFile = "config.json";
    private const float imageOffset = 0.001f; // put the image 1mm in front of the frame

    // variables
    private static JObject root = null;
    private static string fullPath = Path.Combine(Application.dataPath, "..", UserDataPath, configFile);

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
    private static IEnumerator<WaitForSeconds> LoadAlbum(string sceneName)
    {
        Log($"Reading from disk");
        PicturesList = new List<PictureData>();
        JArray album = null;
        JToken sceneObj = null;
        try
        {
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
            if (!root.TryGetValue(sceneName, out sceneObj) || sceneObj.Type != JTokenType.Object)
            {
                LogWarn($"No valid entry found for scene \"{sceneName}\". Creating an empty object.");
                sceneObj = new JObject();
                root[sceneName] = sceneObj;
            }


            album = sceneObj["album"] as JArray ?? new JArray();

            photoAlbum = new GameObject();
            photoAlbum.name = "PhotoAlbum";
        }
        catch (Exception ex)
        {
            LogError($"Failed to load or parse {configFile}: {ex.Message}");
        }

        if (album is null || sceneObj is null)
        {
            yield break;
        }

        // Validate album entries
        var cleanedAlbum = new JArray();
        foreach (var entry in album)
        {
            try
            {
                PictureData pictureData = ParsePictureData(entry);
                Log($"Creating picture {pictureData.path}");

                if (pictureData is null)
                    continue;

                CreatePictureBlock(ref pictureData, photoAlbum.transform);
                if (pictureData.obj != null)
                {
                    cleanedAlbum.Add(entry);
                    pictureData.jsonConfig = cleanedAlbum[cleanedAlbum.Count - 1];
                }
                else
                {
                    LogWarn($"Removed missing file: {pictureData.path}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to parse entry: {ex.Message}");
                continue;
            }

            yield return new WaitForSeconds(0.02f); // Yield to avoid freezing the game
        }

        try
        {
            reloadStash();
            sceneObj["album"] = cleanedAlbum;

            // Save back the modified config
            File.WriteAllText(fullPath, root.ToString(Formatting.Indented));

            stashJson = (JArray)root[currentScene]["stash"];
            albumJson = (JArray)root[currentScene]["album"];
        }
        catch (Exception ex)
        {
            LogError($"Failed to update configuration file: {ex.Message}");
        }
    }

    /**
    * <summary>
    * Reloads the stash accordingly to what's in the "pictures" folder.
    * </summary>
    */
    private static void reloadStash()
    {
        JObject sceneObj = (JObject)root[currentScene];
        JArray stash = (JArray)sceneObj["stash"] ?? new JArray();
        JArray album = sceneObj["album"] as JArray ?? new JArray();
        HashSet<string> albumSet = new(album
                .Where(e => e["path"] != null)
                .Select(e => e["path"].ToString())
            );

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
            }
        }

        // Rebuild updated stash/album
        sceneObj["stash"] = new JArray(cleanedStash);
    }

    /**
    * <summary>
    * Creates a Texture2D from an image file, blending it on top of a predetermined
    * color in order to remove alpha transparency. This way it looks like it has
    * transparency when superposed on top of a colored background.
    * </summary>
    */
    private static PictureData ParsePictureData(JToken pictureJson)
    {
        if (pictureJson == null || pictureJson.Type != JTokenType.Object)
            throw new ArgumentException("Invalid JSON object for PictureData.");

        JObject obj = (JObject)pictureJson;

        PictureData pictureData = new PictureData();

        // Required fields
        pictureData.path = obj.Value<string>("path");
        if (string.IsNullOrEmpty(pictureData.path))
        {
            throw new ArgumentException($"Missing field \"path\"");
        }

        pictureData.position = ParseVector3(obj["position"], "position");
        pictureData.rotation = ParseVector3(obj["rotation"], "rotation");

        // Optional fields with defaults
        pictureData.width = Math.Min(obj.Value<float?>("width") ?? 0, maxPictureSize);
        pictureData.height = Math.Min(obj.Value<float?>("height") ?? 0, maxPictureSize);
        pictureData.padding = obj.Value<float?>("padding") ?? defaultPadding;
        pictureData.thickness = obj.Value<float?>("thickness") ?? defaultThickness;

        pictureData.color = defaultColor; // Default color
        if (obj.TryGetValue("color", out JToken colorToken))
            pictureData.color = ParseColor(colorToken);

        pictureData.alpha = obj.Value<bool?>("alpha") ?? enableAlpha;
        pictureData.visible = obj.Value<bool?>("visible") ?? visibility;

        return pictureData;
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
            return Hex2Color(hex);
        }

        throw new ArgumentException("PictureData: 'color' must be [r,g,b,a?] or hex string.");
    }

    /**
    * <summary>
    * Parses a hex string as a color.
    * </summary>
    */
    private static Color Hex2Color(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            return color;
        }
        throw new ArgumentException("PictureData: 'color' must be [r,g,b,a?] or hex string.");
    }

    /**
    * <summary>
    * Creates the GameObject for a framed picture in the scene.
    * </summary>
    */
    protected static void CreatePictureBlock(ref PictureData pictureData, Transform parent)
    {
        string filePath = pictureData.path;
        if (!File.Exists(filePath))
        {
            // if the path is not absolute, assume it's relative to the pictures folder
            string globalPicturePath = Path.Combine(Application.dataPath, "..", UserDataPath, picturesFolder, pictureData.path);
            if (!File.Exists(globalPicturePath))
            {
                Log($"file doesn't exist");
                return; // File does not exist, cannot create picture block
            }
            else
            {
                filePath = globalPicturePath;
            }
        }

        // Load texture from image file, with the frame's color as background
        Texture2D imageTexture = null;
        if (pictureData.alpha)
        {
            imageTexture = LoadFlattenedTexture(filePath, pictureData.color);
        }
        else
        {
            imageTexture = LoadTexture(filePath);
        }

        int pictureLayer = pictureData.visible?
            LayerMask.NameToLayer("UI") // No collision, visible
            : LayerMask.NameToLayer("PlayerFade"); // No collision, invisible

        float aspectRatio = (float)imageTexture.height / imageTexture.width;
        if (pictureData.width == 0 && pictureData.height == 0)
        {
            if (aspectRatio > 1) // vertical image
            {
                pictureData.height = defaultSize;
            }
            else // horizontal image
            {
                pictureData.width = defaultSize;
            }
        }
        if (pictureData.width == 0)
        {
            pictureData.width = (pictureData.height - 2*pictureData.padding) / aspectRatio + 2*pictureData.padding;
        }
        else
        {
            pictureData.height = (pictureData.width - 2*pictureData.padding) * aspectRatio + 2*pictureData.padding;
        }

        GameObject obj = new GameObject();
        obj.layer = pictureLayer;
        obj.name = $"PictureBlock: {Path.GetFileNameWithoutExtension(pictureData.path)}";
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pictureData.position;
        obj.transform.localRotation = Quaternion.Euler(pictureData.rotation);

        // Create frame
        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.layer = pictureLayer;
        frame.name = "frame";
        frame.transform.SetParent(obj.transform, false);
        frame.transform.localScale = new Vector3(pictureData.width, pictureData.height, pictureData.thickness);
        frame.transform.localPosition = new Vector3(0f, 0f, pictureData.thickness / 2);

        Renderer frameRenderer = frame.GetComponent<Renderer>();
        frameRenderer.material.shader = Shader.Find("Shader Graphs/RUMBLE_Prop");
        frameRenderer.material.SetColor("_Overlay", pictureData.color);

        // Create quad with the image on it
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.layer = pictureLayer;
        quad.name = "picture";
        quad.transform.SetParent(obj.transform, false);
        quad.transform.localScale = new Vector3(pictureData.width - 2*pictureData.padding,
                                                 pictureData.height - 2*pictureData.padding,
                                                 1f);

        // Picture positioned 1mm in front of the frame (local +Z)
        quad.transform.localPosition = new Vector3(0f, 0f, -imageOffset);
        quad.transform.localRotation = Quaternion.identity;

        Renderer quadRenderer = quad.GetComponent<Renderer>();
        quadRenderer.material.shader = Shader.Find("Shader Graphs/RUMBLE_Prop");
        quadRenderer.material.SetTexture("_Albedo", imageTexture);

        // Make the picture interactable
        pictureData.obj = obj;
        pictureData = pictureData;
        PicturesList.Add(pictureData);

        CreateActionButtons(pictureData);
    }
    /**
    * <summary>
    * Creates the action buttons on top of the picture frame.
    * </summary>
    */
    private static void CreateActionButtons(PictureData pictureData)
    {
        var frame = pictureData.obj.transform.GetChild(0).gameObject;

        float buttonSize = pictureData.width / 6;
        Vector3 buttonScale = new Vector3(10 * buttonSize, pictureData.thickness / 0.03f, 10 * buttonSize);
        float buttonHeight = pictureData.height / 2 + buttonSize * 0.6f;

        GameObject actionButtons = new GameObject();
         actionButtons.name = "actionButtons";
        actionButtons.transform.localScale = Vector3.one;

        System.Action action = () => stashPicture(pictureData);
        GameObject stashButton = NewFriendButton("stash", "Stash", action);
        stashButton.transform.localScale = buttonScale;
        stashButton.transform.SetParent(actionButtons.transform, true);
        stashButton.transform.localPosition = new Vector3(-pictureData.width / 2 + buttonSize / 2, 0, 0);
        stashButton.transform.localRotation = Quaternion.Euler(new Vector3(90f, 90f, -90));

        action = () => togglePictureVisibility(pictureData);
        GameObject visibilityButton = NewFriendButton("visibility", pictureData.visible ? "Hide" : "Show", action);
        visibilityButton.transform.localScale = buttonScale;
        visibilityButton.transform.SetParent(actionButtons.transform, true);
        visibilityButton.transform.localPosition = new Vector3(0, 0, 0);
        visibilityButton.transform.localRotation = Quaternion.Euler(new Vector3(90f, 90f, -90));

        action = () => deletePicture(pictureData, false);
        GameObject deleteButton = NewFriendButton("delete", "Delete", action);
        deleteButton.transform.localScale = buttonScale;
        deleteButton.transform.SetParent(actionButtons.transform, true);
        deleteButton.transform.localPosition = new Vector3(pictureData.width / 2 - buttonSize / 2, 0, 0);
        deleteButton.transform.localRotation = Quaternion.Euler(new Vector3(90f, 90f, -90));

        actionButtons.transform.SetParent(frame.transform);
        actionButtons.transform.localScale = new Vector3(1 / frame.transform.localScale.x,
                                                         1 / frame.transform.localScale.y,
                                                         1 / frame.transform.localScale.z);
        actionButtons.transform.localPosition = new Vector3(0, buttonHeight / pictureData.height, 0);
        actionButtons.transform.localRotation = Quaternion.Euler(Vector3.zero);
        actionButtons.SetActive(false);
    }

    /**
    * <summary>
    * Create a Texture2D from an image file, blending it on top of a predetermined
    * color in order to remove alpha transparency. This way it looks like it has
    * transparency when superposed on top of a colored background.
    * </summary>
    */
    private static Texture2D LoadTexture(string path)
    {
        // Load image into Texture2D with alpha channel
        byte[] data = File.ReadAllBytes(path);
        Texture2D output = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        output.LoadImage(data);
        return output;
    }

    /**
    * <summary>
    * Create a Texture2D from an image file, blending it on top of a predetermined
    * color in order to remove alpha transparency. This way it looks like it has
    * transparency when superposed on top of a colored background.
    * </summary>
    */
    private static Texture2D LoadFlattenedTexture(string path, Color background)
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

    /**
    * <summary>
    * Update the json config for a picture in the album, with the new position and size.
    * </summary>
    */
    private static void UpdatePictureConfig(PictureData pictureData)
    {
        if (pictureData.jsonConfig is null)
        {
            return;
        }
        Vector3 position = pictureData.obj.transform.position;
        Vector3 rotation = pictureData.obj.transform.eulerAngles;
        // Update position and rotation (overwrite with new arrays)
        pictureData.jsonConfig["position"] = new JArray { position.x, position.y, position.z };
        pictureData.jsonConfig["rotation"] = new JArray { rotation.x, rotation.y, rotation.z };

        // Check if "height" exists, then update; otherwise update "width"
        if (pictureData.jsonConfig["height"] != null)
        {
            pictureData.jsonConfig["height"] = pictureData.height;
        }
        else
        {
            pictureData.jsonConfig["width"] = pictureData.width;
        }
        if (pictureData.jsonConfig["visible"] != null || pictureData.visible!=visibility)
        {
            pictureData.jsonConfig["visible"] = pictureData.visible;
        }

        // Save full file back to disk
        File.WriteAllText(fullPath, root.ToString(Formatting.Indented));
    }
}
