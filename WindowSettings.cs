namespace MessengerApp;

public class WindowSettings
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 850;
    public bool IsMaximized { get; set; } = false;
    public bool HideFullBanner { get; set; } = true;
}
