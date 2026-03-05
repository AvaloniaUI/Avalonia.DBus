namespace Avalonia.DBus.SourceGen;

public class AvTypeContainer
{
    public AvStructType? Struct { get; set; }

    public AvListType? List { get; set; }

    public AvDictionaryType? Dictionary { get; set; }

    public AvBitFlagsType? BitFlags { get; set; }
}
