using Microsoft.JSInterop;
using System.Reflection;
using System.Runtime.Loader;

namespace BlazorAppLazyAssembly.Services;
public class MyLazyAssemblyLoader
{
    const string GetLazyAssemblies = "window.Blazor._internal.getLazyAssemblies";
    const string ReadLazyAssemblies = "window.Blazor._internal.readLazyAssemblies";
    const string ReadLazyPDBs = "window.Blazor._internal.readLazyPdbs";

    private readonly IJSUnmarshalledRuntime _jsRuntime;
    private HashSet<string> _loadedAssemblyCache;

    public MyLazyAssemblyLoader(IJSRuntime jsRuntime)
    {
        this._jsRuntime = (IJSUnmarshalledRuntime)jsRuntime;
        this._loadedAssemblyCache = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName().Name + ".dll"));
    }

    public Task<IEnumerable<Assembly>> LoadAssembliesAsync(IEnumerable<string> assembliesToLoad)
    {
        return LoadAssembliesInClientAsync(assembliesToLoad);
    }

    private async Task<IEnumerable<Assembly>> LoadAssembliesInClientAsync(IEnumerable<string> assembliesToLoad)
    {
        var newAssembliesToLoad = assembliesToLoad.Where(x => !_loadedAssemblyCache.Contains(x)).ToArray();
        if (!newAssembliesToLoad.Any())
            return Array.Empty<Assembly>();

        var loadCount = (int)await this._jsRuntime.InvokeUnmarshalled<string[], Task<object>>(
           GetLazyAssemblies,
           newAssembliesToLoad);
        if (loadCount == 0)
            return Array.Empty<Assembly>();

        var loadDLLs = this._jsRuntime.InvokeUnmarshalled<byte[][]>(ReadLazyAssemblies);
        var loadPDBs = this._jsRuntime.InvokeUnmarshalled<byte[][]>(ReadLazyPDBs);
        var loadAssemblies = loadDLLs.Zip(loadPDBs, (dll, pdb) =>
             pdb.Length == 0 ?
                 AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(dll)) :
                 AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(dll), new MemoryStream(pdb))
         ).ToArray();
        Array.ForEach(loadAssemblies, x => this._loadedAssemblyCache.Add(x.GetName().Name + ".dll"));
        return loadAssemblies;
    }
}
