using System;
using System.Runtime.InteropServices;
using System.Text;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D;

namespace My2DEngine.Engine.Rendering.Backends.D3D11
{
    /// <summary>
    /// 여러 presenter가 공통으로 사용하는 런타임 HLSL 셰이더 컴파일 헬퍼다.
    /// D3DCompiler를 통해 문자열로 정의된 HLSL 소스를 GPU 바이트코드(Blob)로 변환한다.
    /// 컴파일 실패 시 오류 메시지를 예외로 변환해 호출자가 즉시 원인을 파악할 수 있게 한다.
    /// </summary>
    internal static class D3D11ShaderCompiler
    {
        /// <summary>
        /// HLSL 소스 코드를 컴파일해 GPU 셰이더 바이트코드 Blob을 반환한다.
        /// 컴파일 오류가 있으면 오류 메시지를 담은 <see cref="InvalidOperationException"/>을 던진다.
        /// </summary>
        /// <param name="source">컴파일할 HLSL 소스 문자열(ASCII 인코딩).</param>
        /// <param name="entryPoint">셰이더 진입점 함수 이름(예: "VSMain", "PSMain").</param>
        /// <param name="targetProfile">셰이더 모델 문자열(예: "vs_5_0", "ps_5_0").</param>
        /// <returns>컴파일된 셰이더 바이트코드를 담은 Blob. 호출자가 Dispose 해야 한다.</returns>
        public static Blob Compile(string source, string entryPoint, string targetProfile)
        {
            byte[] sourceBytes = Encoding.ASCII.GetBytes(source);
            GCHandle handle = GCHandle.Alloc(sourceBytes, GCHandleType.Pinned);
            try
            {
                Result result = Compiler.Compile(
                    handle.AddrOfPinnedObject(),
                    sourceBytes.Length,
                    null,
                    null,
                    null,
                    entryPoint,
                    targetProfile,
                    ShaderFlags.OptimizationLevel3,
                    EffectFlags.None,
                    out Blob shaderBlob,
                    out Blob errorBlob);

                try
                {
                    if (result.Failure)
                    {
                        string errorText = errorBlob != null
                            ? Marshal.PtrToStringAnsi(errorBlob.BufferPointer, errorBlob.BufferSize)
                            : "Unknown shader compilation failure.";
                        throw new InvalidOperationException(errorText);
                    }

                    return shaderBlob;
                }
                finally
                {
                    errorBlob?.Dispose();
                }
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
