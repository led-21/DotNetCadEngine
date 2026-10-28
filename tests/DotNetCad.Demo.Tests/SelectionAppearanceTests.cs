using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DotNetCad.Core.Editor;
using DotNetCad.Core.Entities;
using DotNetCad.Core.Geometry;
using DotNetCad.Demo.Controls;
using Xunit;
namespace DotNetCad.Demo.Tests;
public class SelectionAppearanceTests
{
    [Theory]
    [InlineData("#38BDF8")][InlineData("#FACC15")][InlineData("#F472B6")][InlineData("#67E8F9")]
    [InlineData("#FFFFFF")][InlineData("#000000")][InlineData("#888888")]
    public void SelectionUsesOneUniformAccent(string hex)
    {
        var source=(Color)ColorConverter.ConvertFromString(hex);
        var accent=CadSelectionAppearance.ColorFor(source);
        Assert.Equal(Color.FromRgb(250,204,21),accent);
    }
    [Theory]
    [InlineData("#38BDF8")][InlineData("#FACC15")][InlineData("#FFFFFF")]
    public void SelectedGeometryIsRenderedAboveOverlappingObjectsWithoutChangingColors(string sourceHex)
    {
        Exception? error=null;
        var thread=new Thread(()=>{
            try {
                var doc=new CadDocument();var selected=new CadLine(new(-2,0),new(2,0),colorHex:sourceHex);
                doc.AddEntity(selected);doc.AddEntity(new CadLine(new(-2,0),new(2,0),colorHex:"#EF4444"));
                var vp=new CadViewport {Document=doc,Width=400,Height=240};
                vp.Measure(new Size(400,240));vp.Arrange(new Rect(0,0,400,240));vp.UpdateLayout();
                selected.IsSelected=true;vp.InvalidateVisual();vp.UpdateLayout();
                var bitmap=new RenderTargetBitmap(400,240,96,96,PixelFormats.Pbgra32);bitmap.Render(vp);
                var point=(Point)typeof(CadViewport).GetMethod("ToWpfPoint",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(vp,[new Point2D(0,0)])!;
                var pixels=new byte[4];bitmap.CopyPixels(new Int32Rect((int)point.X,(int)point.Y,1,1),pixels,4,0);
                var accent=CadSelectionAppearance.ColorFor((Color)ColorConverter.ConvertFromString(selected.ColorHex));
                Assert.Equal(accent.R,pixels[2]);Assert.Equal(accent.G,pixels[1]);Assert.Equal(accent.B,pixels[0]);
                bitmap.CopyPixels(new Int32Rect((int)point.X,(int)point.Y+2,1,1),pixels,4,0);
                Assert.Equal(0,pixels[0]);Assert.Equal(0,pixels[1]);Assert.Equal(0,pixels[2]); // halo distinguishes even a yellow source
                Assert.Equal(sourceHex,selected.ColorHex);Assert.Equal("#EF4444",doc.Entities[1].ColorHex);
                var dir=System.IO.Path.Combine(AppContext.BaseDirectory,"selection-validation");System.IO.Directory.CreateDirectory(dir);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file=System.IO.File.Create(System.IO.Path.Combine(dir,"overlap.png"));encoder.Save(file);
            }catch(Exception ex){error=ex;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error!=null)throw error;
    }
}
