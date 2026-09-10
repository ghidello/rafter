namespace Sotsera.Rafter;

/// <summary>Identifies the redirected stream affected by a process-output failure.</summary>
public enum ProcessOutputStream
{
    /// <summary>Standard output.</summary>
    StandardOutput,

    /// <summary>Standard error.</summary>
    StandardError,

    /// <summary>Both redirected streams.</summary>
    Both,
}
