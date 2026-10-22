using System;
using System.IO;
using System.Linq;
using System.Threading;
using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
namespace FormatWheel;
public static class Cutout
{
    public static void Run(string input,string output,CancellationToken ct)
    {
        using var source=new MagickImage(input);source.AutoOrient();using var resized=source.Clone();resized.Resize(new MagickGeometry(320,320){IgnoreAspectRatio=true});
        resized.Depth=8;resized.ColorSpace=ColorSpace.sRGB;resized.ColorType=ColorType.TrueColor;resized.Alpha(AlphaAction.Remove);
        var rgb=resized.ToByteArray(MagickFormat.Rgb);if(rgb.Length!=320*320*3)throw new InvalidDataException($"Unexpected RGB size {rgb.Length}, dimensions {resized.Width}x{resized.Height}");float maximum=Math.Max(1,rgb.Max(v=>(int)v));float[] mean=[.485f,.456f,.406f],std=[.229f,.224f,.225f];
        var tensor=new DenseTensor<float>(new[]{1,3,320,320});for(int y=0;y<320;y++)for(int x=0;x<320;x++)for(int c=0;c<3;c++)tensor[0,c,y,x]=(rgb[(y*320+x)*3+c]/maximum-mean[c])/std[c];
        using var options=new SessionOptions{InterOpNumThreads=1,IntraOpNumThreads=Math.Max(1,Math.Min(4,Environment.ProcessorCount))};using var session=new InferenceSession(Assets.File("u2netp.onnx"),options);
        using var runOptions=new RunOptions();using var registration=ct.Register(()=>runOptions.Terminate=true);ct.ThrowIfCancellationRequested();
        using var result=session.Run(new[]{NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(),tensor)},session.OutputNames,runOptions);
        var mask=result.First().AsTensor<float>().ToArray();float min=mask.Min(),range=Math.Max(.000001f,mask.Max()-min);byte[] bytes=mask.Select(v=>(byte)Math.Clamp((v-min)/range*255,0,255)).ToArray();
        using var alpha=new MagickImage(bytes,new MagickReadSettings{Width=320,Height=320,Format=MagickFormat.Gray,Depth=8});alpha.Resize(new MagickGeometry(source.Width,source.Height){IgnoreAspectRatio=true});source.Alpha(AlphaAction.Set);source.Composite(alpha,CompositeOperator.CopyAlpha);ct.ThrowIfCancellationRequested();source.Write(output);
    }
}
