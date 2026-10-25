namespace RWDE.Shell.Terrain
{
    /// <summary>
    /// Reads the current <see cref="DesktopLayout"/>. <see cref="Win32DesktopLayoutSource"/> is the
    /// real desktop; tests substitute fixed layouts.
    /// </summary>
    public interface IDesktopLayoutSource
    {
        DesktopLayout Read();
    }
}
