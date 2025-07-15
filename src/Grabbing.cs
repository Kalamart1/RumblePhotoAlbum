using MelonLoader;
using UnityEngine;
using RumbleModdingAPI;

namespace RumblePhotoAlbum;

public partial class MainClass : MelonMod
{
    private static bool[] grip = { false, false }; // whether the grip is pressed on each controller
    private static bool[] holding = { false, false }; // whether the picture is being held by each hand
    private static GameObject[] hand = { null, null }; // left and right hand GameObjects
    private static PictureData currentlyModified = null; // a single picture can be modified at once
    private static GameObject resizingHandle = null; // parent to the picture when resizing

    private const float hold_distance = 0.05f; // Distance to consider holding a picture

    /**
    * <summary>
    * Initialize the GameObject variables, and reset the holding status.
    * </summary>
    */
    private static void InitGrabbing()
    {
        Transform playerTr = Calls.Players.GetPlayerController().gameObject.transform.GetChild(1);
        grip[0] = false;
        grip[1] = false;
        holding[0] = false;
        holding[1] = false;
        hand[0] = playerTr.GetChild(1).gameObject;
        hand[1] = playerTr.GetChild(2).gameObject;
        currentlyModified = null;
        resizingHandle = new GameObject("ResizingHandle");
        resizingHandle.transform.SetParent(playerTr, true);
    }

    /**
    * <summary>
    * To be called regularly. Checks the triggers on both hands
    * and changes the holding state accordingly.
    * </summary>
    */
    private static void ProcessGrabbing()
    {
        bool grabbingChanged = ProcessGrabbing(0) || ProcessGrabbing(1);
        if (grabbingChanged)
        {
            if (currentlyModified is not null) // if a picture is being held
            {
                UpdatePictureParent(currentlyModified);
                if (!holding[0] && !holding[1])
                {
                    currentlyModified = null; // Reset currently modified picture if not holding
                }
            }
        }
    }

    /**
    * <summary>
    * Check the grip on the controller of one hand,
    * and update the holding status of this hand accordingly.
    * </summary>
    */
    private static bool ProcessGrabbing(int index)
    {
        bool holdingChanged = CheckIfGripChanged(index);
        if (holdingChanged) // if the grip status changed
        {
            bool holding_old = holding[index];
            UpdateHolding(index); // check if that impacted any picture holding
            holdingChanged = (holding[index] != holding_old);
        }
        return holdingChanged; // Return true if holding state changed
    }

    /**
    * <summary>
    * Returns true if the grip status of the controller was changed (pressed or released),
    * and updates the grip status accordingly.
    * </summary>
    */
    private static bool CheckIfGripChanged(int index)
    {
        bool grip_new = false;
        // consider the grip active if either the trigger or
        // the grip is pressed on the controller
        if (index == 0)
        {
            grip_new = (Calls.ControllerMap.LeftController.GetTrigger() > 0.5f ||
                Calls.ControllerMap.LeftController.GetGrip() > 0.5f);
        }
        else if (index == 1)
        {
            grip_new = (Calls.ControllerMap.RightController.GetTrigger() > 0.5f ||
                Calls.ControllerMap.RightController.GetGrip() > 0.5f);
        }

        bool gripChanged = (grip_new != grip[index]);

        if (grip_new && !grip[index])
        {
            // Start grabbing
            grip[index] = true;
        }
        else if (!grip_new && grip[index])
        {
            // Stop grabbing
            grip[index] = false;
        }
        return gripChanged; // Return true if grip state changed
    }

    /**
    * <summary>
    * Updates the holding status of a hand by checking if it's close enough to a picture.
    * </summary>
    */
    private static void UpdateHolding(int index)
    {
        if (PicturesList is null)
        {
            return;
        }
        if (!grip[index]) // If the grip is not pressed, holding is impossible
        {
            holding[index] = false;
            return;
        }

        // if there is already a picture we're holding, ignore all other pictures
        if (currentlyModified is not null)
        {
            float dst = DistanceToPictureSurface(hand[index], currentlyModified);
            holding[index] = (dst < hold_distance); // less than 5cm away
        }
        else // if not, find the closest picture within the hold distance
        {

            float dst_min = hold_distance;
            foreach (PictureData pictureData in PicturesList)
            {
                if (pictureData.obj is null)
                {
                    LogWarn($"Framed picture {pictureData.framedPicture.path} has no GameObject associated with it.");
                    continue;
                }
                float dst = DistanceToPictureSurface(hand[index], pictureData);
                if (dst < dst_min)
                {
                    holding[index] = true;
                    dst_min = dst;
                    currentlyModified = pictureData; // Update currently modified picture
                }
            }
            if (currentlyModified is not null)
            {
                string side = (index == 0) ? "left" : "right";
            }
        }
    }

    /**
    * <summary>
    * Get the distance from the hand to the frame's edge.
    * </summary>
    */
    public static float DistanceToPictureSurface(GameObject hand, PictureData pictureData)
    {
        Vector3 handPos = hand.transform.position;

        Transform picTransform = pictureData.obj.transform.GetChild(0);
        Vector3 center = picTransform.position;
        Quaternion rotation = picTransform.rotation;

        // Physical box half-size
        Vector3 extents = new Vector3(pictureData.framedPicture.width,
                                          pictureData.framedPicture.height,
                                          pictureData.framedPicture.thickness) * 0.5f;

        // Transform hand into the space of the picture
        Vector3 localHandPos = Quaternion.Inverse(rotation) * (handPos - center);

        // get distance along each axis
        float dx = Mathf.Max(0f, Mathf.Abs(localHandPos.x) - extents.x);
        float dy = Mathf.Max(0f, Mathf.Abs(localHandPos.y) - extents.y);
        float dz = Mathf.Max(0f, Mathf.Abs(localHandPos.z) - extents.z);

        // If all are zero, the hand is inside or touching the box: return 0
        if (dx == 0f && dy == 0f && dz == 0f)
            return 0f;

        // Approximate distance to the surface
        return Mathf.Sqrt(dx*dx + dy*dy + dz*dz);
    }

    /**
    * <summary>
    * Change the parent of the picture's tranform
    * depending on which hands are holding the picture.
    * </summary>
    */
    private static void UpdatePictureParent(PictureData pictureData)
    {
        if (holding[0])
        {
            if (holding[1]) // holding with two hands
            {
                UpdateResizingHandle();
                pictureData.obj.transform.SetParent(resizingHandle.transform, true);
            }
            else // holding in left hand
            {
                pictureData.obj.transform.SetParent(hand[0].transform, true);
            }
        }
        else if (holding[1]) // holding in right hand
        {
            pictureData.obj.transform.SetParent(hand[1].transform, true);
        }
        else
        {
            // If not holding, return to default parent
            pictureData.obj.transform.SetParent(photoAlbum.transform, true);
            // TODO save picture new position and size on disk
        }
    }

    /**
    * <summary>
    * To be called regularly. Checks if the image is currently being held by two hands,
    * and if such is the case, updates it to get resized and rotated by them.
    * </summary>
    */
    private static void UpdateResizingIfNeeded()
    {
        if (holding[0] && holding[1]) // holding with two hands
        {
            // update the scale and position of the handle in between the two hands
            UpdateResizingHandle();
            UpdatePictureSize(currentlyModified);
        }
    }

    /**
    * <summary>
    * Update the position, rotation and scale of the resizing handle
    * </summary>
    */
    private static void UpdateResizingHandle()
    {
        Transform left = hand[0].transform;
        Transform right = hand[1].transform;

        // get middle position between the two hands
        Vector3 midPos = (left.localPosition + right.localPosition) * 0.5f;

        // X axis: from left to right hand
        Vector3 xAxis = (right.localPosition - left.localPosition).normalized;

        // Y axis: the averaged hand "up", but projected onto plane perpendicular to x
        Quaternion avgRotation = Quaternion.Slerp(left.localRotation, right.localRotation, 0.5f);
        Vector3 yAxis = Vector3.ProjectOnPlane(avgRotation * Vector3.up, xAxis).normalized;

        // Z axis: completing the right-handed basis
        Vector3 zAxis = Vector3.Cross(xAxis, yAxis).normalized;
        yAxis = Vector3.Cross(zAxis, xAxis); // guarantee orthonormality

        // construct picture rotation from this basis
        Quaternion rotation = Quaternion.LookRotation(zAxis, yAxis);

        // set proportional scale
        float distance = Vector3.Distance(left.localPosition, right.localPosition);
        resizingHandle.transform.localScale = Vector3.one * distance;

        // apply the new position and rotation
        resizingHandle.transform.localPosition = midPos;
        resizingHandle.transform.localRotation = rotation;
    }

    /**
    * <summary>
    * Update the size of each element of the picture as it is being resized
    * </summary>
    */
    private static void UpdatePictureSize(PictureData pictureData)
    {
        // get the global scale of the picture
        float scale = pictureData.obj.transform.localScale.x *
                      resizingHandle.transform.localScale.x;

        FramedPicture framedPicture = pictureData.framedPicture;

        Transform frame = pictureData.obj.transform.GetChild(0);
        Transform quad = pictureData.obj.transform.GetChild(1);
        float aspectRatio = quad.localScale.y / quad.localScale.x;

        // the width of the image is imposed by the width of the frame
        float localQuadWidth = frame.localScale.x - framedPicture.padding / scale;
        // the height is imposed by the aspect ratio of the image
        quad.localScale = new Vector3(localQuadWidth,
                                      localQuadWidth*aspectRatio,
                                      1f);

        // the frame's width is set by resizing, but the height follows the image's height
        frame.localScale = new Vector3(frame.localScale.x,
                                        quad.localScale.y + framedPicture.padding/scale,
                                        framedPicture.thickness/scale);

        framedPicture.width = frame.localScale.x * scale;
        framedPicture.height = frame.localScale.y * scale;

        // move the frame to the back
        frame.transform.localPosition = new Vector3(0f, 0f, framedPicture.thickness / (2*scale));
        // move the image quad to the front
        quad.transform.localPosition = new Vector3(0f, 0f, -imageOffset / scale);
    }
}