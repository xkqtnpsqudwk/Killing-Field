namespace My2DEngine.Game.Core
{
    public sealed class GameSettings
    {
        public float FovDegrees            { get; set; } = 80f;
        public float MouseSensitivity      { get; set; } = 0.003f;
        public int   BgmVolume             { get; set; } = 100;
        public int   SfxVolume             { get; set; } = 100;
        public int   WindowSizePresetIndex { get; set; } = 1;

        public void Sanitize()
        {
            if (FovDegrees < 60f)  FovDegrees = 60f;
            if (FovDegrees > 90f)  FovDegrees = 90f;
            if (MouseSensitivity < 0.001f) MouseSensitivity = 0.001f;
            if (MouseSensitivity > 0.01f)  MouseSensitivity = 0.01f;
            if (BgmVolume < 0)   BgmVolume = 0;
            if (BgmVolume > 100) BgmVolume = 100;
            if (SfxVolume < 0)   SfxVolume = 0;
            if (SfxVolume > 100) SfxVolume = 100;
            if (WindowSizePresetIndex < 0) WindowSizePresetIndex = 0;
            if (WindowSizePresetIndex > 3) WindowSizePresetIndex = 3;
        }
    }
}
