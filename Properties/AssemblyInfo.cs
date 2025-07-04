using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MelonLoader;
using RumblePhotoAlbum; // The namespace of the mod

[assembly: MelonInfo(typeof(RumblePhotoAlbum.MainClass), RumblePhotoAlbum.BuildInfo.ModName, RumblePhotoAlbum.BuildInfo.ModVersion, RumblePhotoAlbum.BuildInfo.Author)]
[assembly: VerifyLoaderVersion(0, 6, 5, true)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonColor(255, 255, 31, 90)]
[assembly: MelonAuthorColor(255, 255, 31, 90)]

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyDescription(RumblePhotoAlbum.BuildInfo.Description)]
[assembly: AssemblyCopyright("Copyright ©  2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("621d30a5-8fa1-4d87-9826-92c0149b033e")]
