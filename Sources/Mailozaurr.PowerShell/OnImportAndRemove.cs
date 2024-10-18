using System;
using System.IO;
using System.Management.Automation;
using System.Reflection;

public class OnModuleImportAndRemove : IModuleAssemblyInitializer, IModuleAssemblyCleanup {
    public void OnImport() {
        if (IsNetFramework()) {
            AppDomain.CurrentDomain.AssemblyResolve += MyResolveEventHandler;
        }
    }

    public void OnRemove(PSModuleInfo module) {
        if (IsNetFramework()) {
            AppDomain.CurrentDomain.AssemblyResolve -= MyResolveEventHandler;
        }
    }

    private static Assembly MyResolveEventHandler(object sender, ResolveEventArgs args) {
        //This code is used to resolve the assemblies
        //Console.WriteLine($"Resolving {args.Name}");
        var directoryPath = Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location);
        var filesInDirectory = Directory.GetFiles(directoryPath);

        foreach (var file in filesInDirectory) {
            var fileName = Path.GetFileName(file);
            var assemblyName = Path.GetFileNameWithoutExtension(file);

            if (args.Name.StartsWith(assemblyName)) {
                //Console.WriteLine($"Loading {args.Name} assembly {fileName}");
                return Assembly.LoadFile(file);
            }
        }
        return null;
    }

    private bool IsNetFramework() {
        // Get the version of the CLR
        Version clrVersion = System.Environment.Version;
        // Check if the CLR version is 4.x.x.x
        return clrVersion.Major == 4;
    }

    private bool IsNetCore() {
        return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNet5OrHigher() {
        return System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 5", StringComparison.OrdinalIgnoreCase) ||
               System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 6", StringComparison.OrdinalIgnoreCase) ||
               System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 7", StringComparison.OrdinalIgnoreCase) ||
               System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET 8", StringComparison.OrdinalIgnoreCase);
    }
}