using System.Reflection;
using System.Runtime.Loader;

namespace CodeQuest.Engine;

internal sealed class PlayerLoadContext() : AssemblyLoadContext("PlayerCode", isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
