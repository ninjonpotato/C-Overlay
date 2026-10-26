
using System;
using System.IO;
using System.Runtime.Intrinsics.X86;
using Vortice.D3DCompiler;

namespace screengrabber_wpf
{
    public static class ShaderCompiler
    {
        public static string[] Shaders = ["blackAndWhite","invertShader","Dvd"];
        public enum ShaderNames
        {
            BW, INVERT,DVD
        }
        private static int _shaderIndex = (int)ShaderNames.BW;

        public static ShaderNames SelectedShader
        {
            get => (ShaderNames)_shaderIndex;
            set => _shaderIndex = (int)value;
        }

        public static string currentShader
        {
            get => Shaders[_shaderIndex] + ".hlsl";
            set => _shaderIndex = Array.IndexOf(
                Shaders, value.Replace(".hlsl", "")
            );
        }

        public static byte[] CompilePixelShader()
        {
            string path = Path.Combine(
                AppContext.BaseDirectory,
                currentShader);

            string source = File.ReadAllText(path);

            var result = Compiler.Compile(
                source,
                entryPoint: "PSMain",
                sourceName: currentShader,
                profile: "ps_5_0");

            return result.ToArray();
        }

        public static byte[] CompileVertexShader()
        {
            string path = Path.Combine(
             AppContext.BaseDirectory,
             currentShader);

          /*  System.Diagnostics.Debug.WriteLine($"Keresett fájl: {path}");
            System.Diagnostics.Debug.WriteLine($"Létezik: {File.Exists(path)}");*/

            string source = File.ReadAllText(path);

            var result = Compiler.Compile(
                source,
                entryPoint: "VSMain",
                sourceName: currentShader,
                profile: "vs_5_0");

            return result.ToArray();
        }
    }
}