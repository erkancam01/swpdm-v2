using System;
using Xunit;

namespace SwPdm.Cekirdek.Testler;

/// <summary>
/// Yalnizca Windows'ta kosan olgu. Diger platformlarda SESSIZCE gecmez -
/// "atlandi" olarak ve SEBEBIYLE raporlanir (CLAUDE.md 3).
/// </summary>
public sealed class WindowsOlgusu : FactAttribute
{
    public WindowsOlgusu()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Yalnizca Windows'ta anlamli: gercek Path davranisiyla karsilastiriyor.";
        }
    }
}

/// <summary>
/// Yalnizca Windows DISINDA kosan olgu.
/// </summary>
public sealed class WindowsDisiOlgusu : FactAttribute
{
    public WindowsDisiOlgusu()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Yalnizca Windows disinda anlamli: Path'in bozuk davranisini belgeliyor.";
        }
    }
}
