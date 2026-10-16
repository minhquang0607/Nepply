using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Nepply.Models;
using Nepply.Services;
using Nepply.Views;

internal static class MaterialAppearanceTests
{
    internal static void Run(Action<string,bool> check,Action<int> pump,string output,Action<FrameworkElement,int,int,string> render)
    {
        var card=new Border {Style=(Style)Application.Current.FindResource("MetricCard"),Width=280,Height=150};
        var label=new TextBlock {Text="CPU     28%   48°C",FontSize=24,VerticalAlignment=VerticalAlignment.Center};
        label.SetResourceReference(TextBlock.ForegroundProperty,"AccentBrush");card.Child=label;
        var root=new Grid {Width=340,Height=220};root.SetResourceReference(Panel.BackgroundProperty,"SurfaceBrush");root.Children.Add(card);
        var host=new Window {Content=root,SizeToContent=SizeToContent.WidthAndHeight,Left=-10000,Top=-10000,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            host.Show();pump(20);
            foreach(var theme in new[]{ThemeOption.Dark,ThemeOption.Light})
            foreach(var name in new[]{"Champagne","Gold","Platinum","Rose Gold","Copper","Titanium","Neon Cyan","Neon Pink"})
            {
                var settings=new AppSettings {Theme=theme,Accent=name,DynamicMaterial=false,GlowStrength=.3};
                ThemeService.ApplyAccessibility(settings,false,false);host.UpdateLayout();
                bool neon=name.StartsWith("Neon ");
                var material=card.Background as LinearGradientBrush;
                check($"Material {theme}/{name} supports diagonal repeating bands",material!=null && material.EndPoint.Y>material.StartPoint.Y && material.SpreadMethod==GradientSpreadMethod.Repeat);
                check($"Material {theme}/{name} reflections are symmetric",material!=null && material.GradientStops.Zip(material.GradientStops.Reverse()).All(pair=>pair.First.Color==pair.Second.Color && Math.Abs(pair.First.Offset+pair.Second.Offset-1)<.0001));
                check($"Material {theme}/{name} is wired to real card style",material?.GradientStops.Count==17 && MetallicSurface.GetAccent(card)!=null);
                check($"Material {theme}/{name} visible glow",card.Effect is DropShadowEffect shadow && shadow.Opacity>0 && shadow.Color==((SolidColorBrush)Application.Current.Resources["AccentSourceBrush"]).Color);
                var foreground=((SolidColorBrush)label.Foreground).Color;
                check($"Material {theme}/{name} readable across bands",material!=null && material.GradientStops.All(stop=>ThemeService.Contrast(foreground,stop.Color)>=4.5));
                render(root,340,220,Path.Combine(output,$"material-{theme}-{name.Replace(' ','-')}.png"));
                settings.GlowStrength=0;ThemeService.ApplyAccessibility(settings,false,false);
                check($"Material {theme}/{name} zero glow removes effect",card.Effect==null);
            }
            var moving=new AppSettings {Accent="Champagne",DynamicMaterial=true};
            ThemeService.ApplyAccessibility(new AppSettings {Accent="Gold",DynamicMaterial=false},false,true);
            host.UpdateLayout();
            foreach(var size in new[]{new Size(128,128),new Size(280,150),new Size(400,100)})
            {
                var sample=((LinearGradientBrush)card.Background).CloneCurrentValue();
                var phase=new TranslateTransform(); sample.RelativeTransform=phase;
                var tile=new Border {Width=size.Width,Height=size.Height,Background=sample};
                byte[] Raster(double value)
                {
                    phase.X=value;phase.Y=value;
                    tile.Measure(size);tile.Arrange(new Rect(size));tile.UpdateLayout();
                    var bitmap=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32);
                    bitmap.Render(tile);
                    var bytes=new byte[(int)(size.Width*size.Height*4)];
                    bitmap.CopyPixels(bytes,(int)size.Width*4,0);return bytes;
                }
                var start=Raster(0);var end=Raster(1);var beforeEnd=Raster(.9999);var middle=Raster(.5);
                check($"Sweep seam is continuous for {size.Width}x{size.Height}",start.Zip(end).All(p=>Math.Abs(p.First-p.Second)<=1)
                    && start.Zip(beforeEnd).All(p=>Math.Abs(p.First-p.Second)<=1));
                check($"Sweep visibly progresses for {size.Width}x{size.Height}",!start.SequenceEqual(middle));
            }
            moving.MetallicEnabled=false;moving.NeonEnabled=false;
            ThemeService.ApplyAccessibility(moving,false,true);host.UpdateLayout();
            check("Metallic off keeps flat surface and no motion",card.Background is LinearGradientBrush flat && flat.GradientStops.Select(s=>s.Color).Distinct().Count()==1 && !flat.RelativeTransform.HasAnimatedProperties);
            moving.Accent="Neon Cyan";moving.NeonEnabled=true;
            ThemeService.ApplyAccessibility(moving,false,true);host.UpdateLayout();
            check("Metallic off preserves neon independently",card.Background is LinearGradientBrush neonSurface && neonSurface.GradientStops.Count==3 && card.Effect is DropShadowEffect && !neonSurface.RelativeTransform.HasAnimatedProperties);
            moving.Accent="Champagne";moving.MetallicEnabled=true;
            ThemeService.ApplyAccessibility(moving,false,true);host.UpdateLayout();
            var shift=((LinearGradientBrush)card.Background).RelativeTransform;
            check("Real metric card starts requested material motion",shift.HasAnimatedProperties);
            pump(150);
            var movingShift=(TranslateTransform)shift;
            check("Diagonal sweep shares one phase on both axes",movingShift.X>0 && Math.Abs(movingShift.X-movingShift.Y)<.000001);
            ThemeService.ApplyAccessibility(moving,false,true);host.UpdateLayout();
            check("Identical appearance refresh does not restart sweep",ReferenceEquals(shift,((LinearGradientBrush)card.Background).RelativeTransform));
            host.Hide();pump(20);
            check("Hidden metric card stops material clock",!shift.HasAnimatedProperties);
            host.Show();pump(2100);
            check("Visible material continues a repeating sweep",((LinearGradientBrush)card.Background).RelativeTransform.HasAnimatedProperties);
            ThemeService.ApplyAccessibility(moving,true,true);
            check("Real metric card high contrast has no glow",card.Effect==null && ReferenceEquals(card.Background,SystemColors.WindowBrush));
            check("High contrast disables icon neon",Application.Current.Resources["IconNeonEffect"]==null);
            moving.NeonEnabled=false;ThemeService.ApplyAccessibility(moving,false,true);
            check("Neon toggle also removes desktop icon glow",Application.Current.Resources["IconNeonEffect"]==null);
            moving.NeonEnabled=true;ThemeService.ApplyAccessibility(moving,false,true);
            check("Icon neon has a frozen bounded halo",Application.Current.Resources["IconNeonEffect"] is DropShadowEffect {IsFrozen:true,ShadowDepth:0,BlurRadius:18,Opacity:>0 and <=.8});
        }
        finally {host.Close();ThemeService.Apply(new());}
    }
}
