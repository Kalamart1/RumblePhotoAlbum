using MelonLoader;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RumblePhotoAlbum;

/**
* <summary>
* Structure containing all of the data of a spawned picture.
* </summary>
*/
public class PictureData : PhotoAPI.PictureDataInternal
{
    /**
    * <summary>
    * Removes the picture from the scene. No file gets actually deleted from the disk.
    * </summary>
    */
    public void delete()
    {
        PhotoAPI.DeletePicture(this);
    }
}

/**
* <summary>
* Application programming interface to allow other mods to create and delete pictures.
* </summary>
*/
public class PhotoAPI : MainClass
{
    /**
    * <summary>
    * Creates a new picture in the scene. The path, position and rotation are mandatory arguments.
    * The picture will NOT be recorded in the mod's configuration file, so it will disappear on the next scene change.
    * </summary>
    */
    public static PictureData CreatePicture(string path, Vector3 position, Vector3 rotation,
        float width = 0, float height = 0, float? padding = null, float? thickness = null, Color? color = null, bool? alpha = null)
    {
        PictureData pictureData = new PictureData
        {
            path = path,
            position = position,
            rotation = rotation,
            width = width,
            height = width,
            padding = padding ?? defaultPadding,
            thickness = thickness ?? defaultThickness,
            color = color ?? defaultColor,
            alpha = alpha ?? enableAlpha
        };
        CreatePicture(ref pictureData, photoAlbum.transform);
        return pictureData;
    }

    /**
    * <summary>
    * Removes the picture from the scene. No file gets actually deleted from the disk.
    * </summary>
    */
    public static void DeletePicture(PictureData pictureData)
    {
        deletePicture(pictureData, true);
    }
}