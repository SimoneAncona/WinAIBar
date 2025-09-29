using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIBar.Utils;

public static class GetProcessName
{
    public readonly static Dictionary<string, string> Processes = new()
    {
        { "settings", "ms-settings:" },
        { "browser", "www.google.com" }
    };
}
