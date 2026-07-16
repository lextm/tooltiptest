using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ToolTipTest;

class ImageRenderingTest : TestCase
{
    Image? bitmapImageProbe, vectorImageProbe;
    Rectangle? drawingBrushProbe;

    public ImageRenderingTest()
    {
        Name = "Image Rendering";
        Description = "Verifies that DrawingImage, DrawingBrush, and BitmapImage render correctly through LibreWPF's portable composition";
    }

    public override void Setup()
    {
        var panel = new StackPanel { Margin = new Thickness(24), Orientation = Orientation.Horizontal, Background = Brushes.White };

        bitmapImageProbe = new Image { Width = 96, Height = 96, Stretch = Stretch.Fill, Source = CreateProbePngBitmapSource(), Margin = new Thickness(0, 0, 32, 0) };
        vectorImageProbe = new Image { Width = 96, Height = 96, Stretch = Stretch.Fill, Source = CreateProbeDrawingImage(), Margin = new Thickness(0, 0, 32, 0) };
        drawingBrushProbe = new Rectangle { Width = 96, Height = 96, Fill = CreateProbeDrawingBrush(), Stroke = Brushes.Black, StrokeThickness = 1 };

        panel.Children.Add(bitmapImageProbe);
        panel.Children.Add(vectorImageProbe);
        panel.Children.Add(drawingBrushProbe);
        ContentRoot = panel;
    }

    public override void Run()
    {
        CreateContentWindow("ToolTipTest Image Probe", 360, 260);
        if (bitmapImageProbe == null || vectorImageProbe == null || drawingBrushProbe == null)
        {
            Log("IMAGE_TEST: FAIL reason=null-probes");
            SetResult(false);
            return;
        }

        LogImageProbeReady();

        try
        {
            TestWindow?.UpdateLayout();

            var bitmapPass = VerifyBitmapRendering();
            var imagePass = VerifyDrawingImageRendering();
            var brushPass = VerifyDrawingBrushRendering();

            var pass = bitmapPass && imagePass && brushPass;
            Log("IMAGE_RESULT: " + (pass ? "PASS" : "FAIL") + " bitmap=" + bitmapPass + " image=" + imagePass + " brush=" + brushPass);
            SetResult(pass);
        }
        catch (Exception ex)
        {
            Log("IMAGE_RESULT: FAIL exception=" + ex.GetType().Name + ": " + (ex.InnerException?.Message ?? ex.Message.Split('\n')[0]));
            SetResult(false);
        }
    }

    void LogImageProbeReady()
    {
        if (bitmapImageProbe == null || vectorImageProbe == null || drawingBrushProbe == null) return;
        var bitmapTopLeft = bitmapImageProbe.PointToScreen(new Point(0, 0));
        var imageTopLeft = vectorImageProbe.PointToScreen(new Point(0, 0));
        var brushTopLeft = drawingBrushProbe.PointToScreen(new Point(0, 0));
        Log("IMAGE_TEST_READY bitmap=" + bitmapTopLeft.X + "," + bitmapTopLeft.Y + "," + bitmapImageProbe.ActualWidth + "," + bitmapImageProbe.ActualHeight + " image=" + imageTopLeft.X + "," + imageTopLeft.Y + "," + vectorImageProbe.ActualWidth + "," + vectorImageProbe.ActualHeight + " brush=" + brushTopLeft.X + "," + brushTopLeft.Y + "," + drawingBrushProbe.ActualWidth + "," + drawingBrushProbe.ActualHeight);
    }

    bool VerifyBitmapRendering()
    {
        if (bitmapImageProbe == null) return false;
        var bmp = new RenderTargetBitmap((int)bitmapImageProbe.ActualWidth, (int)bitmapImageProbe.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(bitmapImageProbe);
        var pixels = new byte[(int)bitmapImageProbe.ActualWidth * (int)bitmapImageProbe.ActualHeight * 4];
        bmp.CopyPixels(pixels, (int)bitmapImageProbe.ActualWidth * 4, 0);

        var magenta = 0; var orange = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i]; var g = pixels[i + 1]; var r = pixels[i + 2];
            if (r > 180 && b > 180 && g < 80) magenta++;
            if (r > 180 && g > 80 && g < 180 && b < 80) orange++;
        }
        Log("bitmap pixels: magenta=" + magenta + " orange=" + orange);
        return magenta > 400 && orange > 400;
    }

    bool VerifyDrawingImageRendering()
    {
        if (vectorImageProbe == null) return false;
        var bmp = new RenderTargetBitmap((int)vectorImageProbe.ActualWidth, (int)vectorImageProbe.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(vectorImageProbe);
        var pixels = new byte[(int)vectorImageProbe.ActualWidth * (int)vectorImageProbe.ActualHeight * 4];
        bmp.CopyPixels(pixels, (int)vectorImageProbe.ActualWidth * 4, 0);

        var red = 0; var green = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i]; var g = pixels[i + 1]; var r = pixels[i + 2];
            if (r > 180 && g < 90 && b < 90) red++;
            if (g > 150 && r < 120 && b < 120) green++;
        }
        Log("drawing image pixels: red=" + red + " green=" + green);
        return red > 400 && green > 400;
    }

    bool VerifyDrawingBrushRendering()
    {
        if (drawingBrushProbe == null) return false;
        var bmp = new RenderTargetBitmap((int)drawingBrushProbe.ActualWidth, (int)drawingBrushProbe.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(drawingBrushProbe);
        var pixels = new byte[(int)drawingBrushProbe.ActualWidth * (int)drawingBrushProbe.ActualHeight * 4];
        bmp.CopyPixels(pixels, (int)drawingBrushProbe.ActualWidth * 4, 0);

        var blue = 0; var yellow = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i]; var g = pixels[i + 1]; var r = pixels[i + 2];
            if (b > 150 && r < 120 && g < 120) blue++;
            if (r > 160 && g > 160 && b < 120) yellow++;
        }
        Log("drawing brush pixels: blue=" + blue + " yellow=" + yellow);
        return blue > 400 && yellow > 400;
    }

    static DrawingImage CreateProbeDrawingImage()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(Brushes.Red, null, Geometry.Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z")));
        group.Children.Add(new GeometryDrawing(Brushes.Lime, null, Geometry.Parse("M 4,4 L 12,4 L 12,12 L 4,12 Z")));
        return new DrawingImage(group);
    }

    static DrawingBrush CreateProbeDrawingBrush()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(Brushes.Blue, null, Geometry.Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z")));
        group.Children.Add(new GeometryDrawing(Brushes.Yellow, null, Geometry.Parse("M 4,4 L 12,4 L 12,12 L 4,12 Z")));
        return new DrawingBrush(group) { Stretch = Stretch.Fill, TileMode = TileMode.None };
    }

    static BitmapSource CreateProbePngBitmapSource()
    {
        const string pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAIUlEQVR4nGP438DwnxLMMBwNACK8eNSAkWHAwKdEehsAANHgnd9KEgaZAAAAAElFTkSuQmCC";
        using var stream = new MemoryStream(Convert.FromBase64String(pngBase64), writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
