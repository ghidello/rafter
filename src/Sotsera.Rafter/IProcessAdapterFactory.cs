using System.Diagnostics;

namespace Sotsera.Rafter;

internal interface IProcessAdapterFactory
{
    IProcessAdapter Create(ProcessStartInfo startInfo);
}
