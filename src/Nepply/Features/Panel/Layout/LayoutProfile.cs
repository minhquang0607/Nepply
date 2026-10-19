using System.Text.Json;
using Nepply.Models;

namespace Nepply.Services;

// Portable presentation only: never export paths, device IDs, startup or Desktop visibility.
public sealed record LayoutProfile
{
    public int Version { get; init; } = 1;
    public EdgePosition Edge { get; init; }
    public ThemeOption Theme { get; init; }
    public string Accent { get; init; } = "Champagne";
    public string CustomAccent { get; init; } = "#D9BC8B";
    public int Width { get; init; } = 380;
    public int TopWidth { get; init; } = 1440;
    public int TopHeight { get; init; } = 320;
    public double WidthPercent { get; init; }
    public double TopWidthPercent { get; init; }
    public double TopHeightPercent { get; init; }
    public int GridColumns { get; init; } = 4;
    public int GridRows { get; init; } = 5;
    public int TopGridColumns { get; init; } = 6;
    public int TopGridRows { get; init; } = 1;
    public double CellWidth { get; init; } = 88;
    public double CellHeight { get; init; } = 90;
    public IconSizeOption IconSize { get; init; } = IconSizeOption.Medium;
    public int TileShape { get; init; }
    public int MonitorHeight { get; init; } = 380;
    public int MonitorWidth { get; init; } = 370;
    public int SummaryColumns { get; init; }
    public int GpuColumns { get; init; }
    public bool IconsFirst { get; init; }
    public string[] WidgetOrder { get; init; } = ["summary","gpus","network"];
    public bool DynamicMaterial { get; init; } = true;
    public bool MetallicEnabled { get; init; } = true;
    public bool NeonEnabled { get; init; } = true;
    public MotionMode Motion {get;init;}=MotionMode.Smooth;
    public double Glow { get; init; } = .22;
    public double Opacity { get; init; } = .98;
    public static LayoutProfile Capture(AppSettings s) => new()
    {
        Edge=s.EdgePosition,Theme=s.Theme,Accent=s.Accent,CustomAccent=s.CustomAccent,
        Width=s.PanelWidth,TopWidth=s.TopPanelWidth,TopHeight=s.PanelHeight,
        WidthPercent=s.PanelWidthPercent,TopWidthPercent=s.TopPanelWidthPercent,TopHeightPercent=s.PanelHeightPercent,
        GridColumns=s.GridColumns,GridRows=s.GridRows,TopGridColumns=s.TopGridColumns,TopGridRows=s.TopGridRows,
        CellWidth=s.GridCellWidth,CellHeight=s.GridCellHeight,IconSize=s.IconSize,TileShape=s.IconTileShape,
        MonitorHeight=s.MonitorAreaHeight,MonitorWidth=s.MonitorAreaWidth,SummaryColumns=s.SummaryColumns,GpuColumns=s.GpuColumns,
        IconsFirst=s.IconsFirst,WidgetOrder=(string[])s.WidgetOrder.Clone(),DynamicMaterial=s.DynamicMaterial,MetallicEnabled=s.MetallicEnabled,NeonEnabled=s.NeonEnabled,Motion=s.Motion,Glow=s.GlowStrength,Opacity=s.PanelOpacity
    };
    public AppSettings ApplyTo(AppSettings source)
    {
        if(Version!=1) throw new NotSupportedException("Unsupported layout version.").WithUserMessage("LayoutVersionError");
        var result=source.Clone();
        result.EdgePosition=Edge; result.Theme=Theme; result.Accent=Accent; result.CustomAccent=CustomAccent;
        result.PanelWidth=Width; result.TopPanelWidth=TopWidth; result.PanelHeight=TopHeight; result.LayoutVersion=1;
        result.PanelWidthPercent=WidthPercent; result.TopPanelWidthPercent=TopWidthPercent; result.PanelHeightPercent=TopHeightPercent;
        result.GridColumns=GridColumns; result.GridRows=GridRows;
        result.TopGridColumns=TopGridColumns; result.TopGridRows=TopGridRows;
        result.GridCellWidth=CellWidth; result.GridCellHeight=CellHeight; result.IconSize=IconSize; result.IconTileShape=TileShape;
        result.MonitorAreaHeight=MonitorHeight; result.MonitorAreaWidth=MonitorWidth;
        result.SummaryColumns=SummaryColumns; result.GpuColumns=GpuColumns; result.IconsFirst=IconsFirst;
        result.WidgetOrder=(string[])(WidgetOrder ?? []).Clone();
        result.DynamicMaterial=DynamicMaterial; result.MetallicEnabled=MetallicEnabled; result.Motion=Motion; result.GlowStrength=Glow; result.PanelOpacity=Opacity;
        result.NeonEnabled=NeonEnabled;
        result.Validate(); return result;
    }
    public string ToJson() => JsonSerializer.Serialize(this,new JsonSerializerOptions { WriteIndented=true });
    internal static bool SameLayout(AppSettings left,AppSettings right)
    {
        var a=Capture(left); var b=Capture(right);
        // Record equality alone compares array references, not their contents.
        return a==(b with {WidgetOrder=a.WidgetOrder})
            && a.WidgetOrder.SequenceEqual(b.WidgetOrder,StringComparer.Ordinal)
            && left.IconOrder.SequenceEqual(right.IconOrder,StringComparer.OrdinalIgnoreCase);
    }
    public static LayoutProfile Parse(string json)
    {
        if(System.Text.Encoding.UTF8.GetByteCount(json)>65536) throw TooLarge();
        using var doc=JsonDocument.Parse(json);
        if(doc.RootElement.ValueKind!=JsonValueKind.Object || !doc.RootElement.TryGetProperty(nameof(Version),out var version)
            || !version.TryGetInt32(out var value) || value!=1)
            throw new NotSupportedException("Not a supported Nepply layout.").WithUserMessage("LayoutFormatError");
        return doc.RootElement.Deserialize<LayoutProfile>() ?? throw new JsonException("Empty layout file.").WithUserMessage("LayoutEmptyError");
    }
    public static LayoutProfile Parse(System.IO.Stream stream)
    {
        // Read at most the limit plus one byte. Length alone is insufficient for
        // streams without seeking or a file that changes while it is being read.
        byte[] bytes=new byte[65537];
        int length=0,read;
        while(length<bytes.Length && (read=stream.Read(bytes,length,bytes.Length-length))>0) length+=read;
        if(length>65536) throw TooLarge();
        using var buffer=new System.IO.MemoryStream(bytes,0,length,false);
        using var reader=new System.IO.StreamReader(buffer,new System.Text.UTF8Encoding(false,true),detectEncodingFromByteOrderMarks:true);
        return Parse(reader.ReadToEnd());
    }
    private static System.IO.InvalidDataException TooLarge()
        => new System.IO.InvalidDataException("Layout file exceeds 64 KiB.").WithUserMessage("LayoutTooLargeError");
}
