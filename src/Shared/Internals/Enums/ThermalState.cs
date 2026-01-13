namespace DrawnUi.Draw
{
    public enum ThermalState
    {
        Nominal,

        /// <summary>
        /// Optional: slightly reduce FPS/resolution
        /// </summary>
        Fair,

        /// <summary>
        /// iPhone is overheating — frame rate reduced to cool down the device. This is normal and not an app bug.
        /// </summary>
        Serious,

        /// <summary>
        /// iPhone is overheating — frame rate reduced to cool down the device. This is normal and not an app bug.
        /// </summary>
        Critical,

        Unknown
    }
}
