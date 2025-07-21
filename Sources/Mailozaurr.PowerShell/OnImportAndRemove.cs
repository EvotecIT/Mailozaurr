using System;
using System.IO;
using System.Reflection;
#if NET5_0_OR_GREATER
using System.Runtime.Loader;
#endif

/// <summary>
/// OnModuleImportAndRemove is a class that implements the IModuleAssemblyInitializer and IModuleAssemblyCleanup interfaces.
/// This class is used to handle the assembly resolve event when the module is imported and removed.
/// </summary>
public class OnModuleImportAndRemove : IModuleAssemblyInitializer, IModuleAssemblyCleanup {
    /// <summary>
    /// OnImport is called when the module is imported.
    /// </summary>
    public void OnImport() {
        if (IsNetFramework()) {
            AppDomain.CurrentDomain.AssemblyResolve += MyResolveEventHandler;
        }
#if NET5_0_OR_GREATER
        else {
            AssemblyLoadContext.Default.Resolving += ResolveAlc;
        }
#endif
    }

    /// <summary>
    /// OnRemove is called when the module is removed.
    /// </summary>
    /// <param name="module"></param>
    public void OnRemove(PSModuleInfo module) {
        if (IsNetFramework()) {
            AppDomain.CurrentDomain.AssemblyResolve -= MyResolveEventHandler;
        }
#if NET5_0_OR_GREATER
        else {
            AssemblyLoadContext.Default.Resolving -= ResolveAlc;
        }
#endif
    }

    /// <summary>
    /// MyResolveEventHandler is a method that handles the AssemblyResolve event.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    private static Assembly? MyResolveEventHandler(object? sender, ResolveEventArgs args) {
        var libDirectory = Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location);
        var directoriesToSearch = new List<string> { libDirectory };

        if (Directory.Exists(libDirectory)) {
            directoriesToSearch.AddRange(Directory.GetDirectories(libDirectory, "*", SearchOption.AllDirectories));
        }

        var requestedAssemblyName = new AssemblyName(args.Name).Name + ".dll";

        foreach (var directory in directoriesToSearch) {
            var assemblyPath = Path.Combine(directory, requestedAssemblyName);

            if (File.Exists(assemblyPath)) {
                try {
                    return Assembly.LoadFrom(assemblyPath);
                } catch (BadImageFormatException ex) {
                    Console.WriteLine($"Failed to load assembly from {assemblyPath}: {ex.Message}");
                }
            }
        }

        return null;
    }

#if NET5_0_OR_GREATER
    private sealed class LoadContext : AssemblyLoadContext {
        private readonly string _assemblyDir;

        public LoadContext(string assemblyDir)
            : base(name: "Mailozaurr", isCollectible: false) {
            _assemblyDir = assemblyDir;
        }

        protected override Assembly? Load(AssemblyName assemblyName) {
            string asmPath = Path.Combine(_assemblyDir, $"{assemblyName.Name}.dll");
            return File.Exists(asmPath) ? LoadFromAssemblyPath(asmPath) : null;
        }
    }

    private static readonly string _assemblyDir =
        Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location)!;

    private static readonly LoadContext _alc = new LoadContext(_assemblyDir);

    private static Assembly? ResolveAlc(AssemblyLoadContext defaultAlc, AssemblyName assemblyToResolve) {
        string asmPath = Path.Combine(_assemblyDir, $"{assemblyToResolve.Name}.dll");
        if (IsSatisfyingAssembly(assemblyToResolve, asmPath)) {
            return _alc.LoadFromAssemblyName(assemblyToResolve);
        }

        return null;
    }

    private static bool IsSatisfyingAssembly(AssemblyName requiredAssemblyName, string assemblyPath) {
        if (requiredAssemblyName.Name == "Mailozaurr.PowerShell" || !File.Exists(assemblyPath)) {
            return false;
        }

        AssemblyName asmToLoadName = AssemblyName.GetAssemblyName(assemblyPath);

        return string.Equals(asmToLoadName.Name, requiredAssemblyName.Name, StringComparison.OrdinalIgnoreCase)
            && asmToLoadName.Version >= requiredAssemblyName.Version;
    }
#endif

    /// <summary>
    /// Determine if the current runtime is .NET Framework
    /// </summary>
    /// <returns></returns>
    private bool IsNetFramework() {
        return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);
    }

    // Determine if the current runtime is .NET Core
    private bool IsNetCore() {
        return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determine if the current runtime is .NET 5 or higher
    /// </summary>
    /// <returns></returns>
    private bool IsNet5OrHigher() {
        return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 5", StringComparison.OrdinalIgnoreCase) ||
               System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 6", StringComparison.OrdinalIgnoreCase) ||
               System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 7", StringComparison.OrdinalIgnoreCase) ||
               System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 8", StringComparison.OrdinalIgnoreCase) ||
               System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 9", StringComparison.OrdinalIgnoreCase);
    }
}