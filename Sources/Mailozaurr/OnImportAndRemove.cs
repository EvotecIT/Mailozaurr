using System;
using System.IO;
using System.Management.Automation;
using System.Reflection;

public class OnModuleImportAndRemove : IModuleAssemblyInitializer, IModuleAssemblyCleanup {
    public void OnImport() {
#if FRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve += MyResolveEventHandler;
#endif
    }

    public void OnRemove(PSModuleInfo module) {
#if FRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve -= MyResolveEventHandler;
#endif
    }

    private static Assembly MyResolveEventHandler(object sender, ResolveEventArgs args) {
        // These are known to be problematic in .NET Framework, force it to use our packaged dlls.
        if (args.Name.StartsWith("System.Memory,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "System.Memory.dll");
            return Assembly.LoadFile(binPath);
        } else if (args.Name.StartsWith("System.Runtime.CompilerServices.Unsafe,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "System.Runtime.CompilerServices.Unsafe.dll");
            return Assembly.LoadFile(binPath);
        } else if (args.Name.StartsWith("System.Numerics.Vectors,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "System.Numerics.Vectors.dll");
            return Assembly.LoadFile(binPath);
        } else if (args.Name.StartsWith("System.Drawing.Common,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "System.Drawing.Common.dll");
            return Assembly.LoadFile(binPath);
        } else if (args.Name.StartsWith("System.Buffers,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "System.Buffers.dll");
            return Assembly.LoadFile(binPath);
        } else if (args.Name.StartsWith("System.ValueTuple,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "System.ValueTuple.dll");
            return Assembly.LoadFile(binPath);
        } else if (args.Name.StartsWith("System.Text.Encoding.CodePages,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "System.Text.Encoding.CodePages.dll");
            return Assembly.LoadFile(binPath);
        } else if (args.Name.StartsWith("System.IO.Packaging,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "System.IO.Packaging");
            return Assembly.LoadFile(binPath);
        } else if (args.Name.StartsWith("DocumentFormat.OpenXml,")) {
            string binPath = Path.Combine(Path.GetDirectoryName(typeof(OnModuleImportAndRemove).Assembly.Location), "DocumentFormat.OpenXml.dll");
            return Assembly.LoadFile(binPath);
        }
        return null;
    }
}